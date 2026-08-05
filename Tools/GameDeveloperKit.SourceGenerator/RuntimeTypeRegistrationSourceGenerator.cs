using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace GameDeveloperKit.SourceGenerator;

[Generator]
public sealed partial class RuntimeTypeRegistrationSourceGenerator : IIncrementalGenerator
{
    private const string ProcedureBaseName = "GameDeveloperKit.Procedure.ProcedureBase";
    private const string MessageBaseName = "GameDeveloperKit.Network.Message";
    private const string OpcodeAttributeName = "GameDeveloperKit.Network.OpcodeAttribute";

    private static readonly DiagnosticDescriptor InvalidOpcodeRule = new DiagnosticDescriptor(
        "GDK_GEN001",
        "Network opcode is invalid",
        "Network message '{0}' must use an opcode greater than zero",
        "GameDeveloperKit.Generation",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateOpcodeRule = new DiagnosticDescriptor(
        "GDK_GEN002",
        "Network opcode is duplicated",
        "Network opcode '{0}' is declared by both '{1}' and '{2}'",
        "GameDeveloperKit.Generation",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InaccessibleMessageRule = new DiagnosticDescriptor(
        "GDK_GEN003",
        "Network message cannot be registered",
        "Network message '{0}' must be accessible from generated code and must be a concrete non-generic Message",
        "GameDeveloperKit.Generation",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var procedureCandidates = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax,
                static (syntaxContext, _) =>
                    syntaxContext.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)syntaxContext.Node) as INamedTypeSymbol)
            .Where(static symbol => symbol != null)
            .Select(static (symbol, _) => symbol!);
        var messageCandidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            OpcodeAttributeName,
            static (_, _) => true,
            static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol);
        var logicNodeCandidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            LogicNodeAttributeName,
            static (_, _) => true,
            static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol);
        var input = context.CompilationProvider
            .Combine(procedureCandidates.Collect())
            .Combine(messageCandidates.Collect())
            .Combine(logicNodeCandidates.Collect());

        context.RegisterSourceOutput(input, static (productionContext, value) =>
            Generate(
                productionContext,
                value.Left.Left.Left,
                value.Left.Left.Right,
                value.Left.Right,
                value.Right));
    }

    private static void Generate(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol> procedureCandidates,
        ImmutableArray<INamedTypeSymbol> messageCandidates,
        ImmutableArray<INamedTypeSymbol> logicNodeCandidates)
    {
        var procedureBase = compilation.GetTypeByMetadataName(ProcedureBaseName);
        var messageBase = compilation.GetTypeByMetadataName(MessageBaseName);
        var opcodeAttribute = compilation.GetTypeByMetadataName(OpcodeAttributeName);
        if (procedureBase == null || messageBase == null || opcodeAttribute == null)
        {
            return;
        }

        var procedures = DistinctTypes(procedureCandidates)
            .Where(symbol => IsConcrete(symbol) &&
                             IsOrDerivesFrom(symbol, procedureBase) &&
                             IsAccessible(symbol) &&
                             HasPublicParameterlessConstructor(symbol))
            .OrderBy(static symbol => symbol.ToDisplayString(), StringComparer.Ordinal)
            .ToArray();

        var messages = new List<MessageRegistration>();
        var messagesByOpcode = new Dictionary<int, INamedTypeSymbol>();
        foreach (var message in DistinctTypes(messageCandidates))
        {
            var attribute = message.GetAttributes().FirstOrDefault(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, opcodeAttribute));
            if (attribute == null || attribute.ConstructorArguments.Length != 1 ||
                attribute.ConstructorArguments[0].Value is not int opcode)
            {
                continue;
            }

            var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation();
            if (!IsConcrete(message) || !IsOrDerivesFrom(message, messageBase) || !IsAccessible(message))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InaccessibleMessageRule,
                    location,
                    message.ToDisplayString()));
                continue;
            }

            if (opcode <= 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidOpcodeRule,
                    location,
                    message.ToDisplayString()));
                continue;
            }

            if (messagesByOpcode.TryGetValue(opcode, out var existing))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateOpcodeRule,
                    location,
                    opcode,
                    existing.ToDisplayString(),
                    message.ToDisplayString()));
                continue;
            }

            messagesByOpcode.Add(opcode, message);
            messages.Add(new MessageRegistration(opcode, message));
        }

        var logicNodes = CollectLogicNodes(context, compilation, logicNodeCandidates);

        if (procedures.Length == 0 && messages.Count == 0 && logicNodes.Count == 0)
        {
            return;
        }

        context.AddSource(
            "RuntimeTypeRegistration.g.cs",
            SourceText.From(GenerateSource(procedures, messages, logicNodes), Encoding.UTF8));
    }

    private static string GenerateSource(
        IReadOnlyList<INamedTypeSymbol> procedures,
        IReadOnlyList<MessageRegistration> messages,
        IReadOnlyList<LogicNodeRegistration> logicNodes)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("namespace GameDeveloperKit.Generated");
        source.AppendLine("{");
        source.AppendLine("    internal static class RuntimeTypeRegistrationGenerated");
        source.AppendLine("    {");
        source.AppendLine("        [global::UnityEngine.RuntimeInitializeOnLoadMethodAttribute(global::UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]");
        source.AppendLine("        private static void Register()");
        source.AppendLine("        {");

        foreach (var procedure in procedures)
        {
            source.Append("            global::GameDeveloperKit.Procedure.ProcedureRegistry.RegisterGenerated<")
                .Append(ToGlobalTypeName(procedure))
                .AppendLine(">();");
        }

        foreach (var message in messages.OrderBy(static item => item.Opcode))
        {
            source.Append("            global::GameDeveloperKit.Network.NetworkMessageRegistry.RegisterGenerated<")
                .Append(ToGlobalTypeName(message.Type))
                .Append(">(")
                .Append(message.Opcode)
                .AppendLine(");");
        }

        foreach (var logicNode in logicNodes.OrderBy(static item => item.LogicId, StringComparer.Ordinal))
        {
            source.Append("            global::GameDeveloperKit.Story.Logic.LogicNodeRegistry.RegisterGenerated<")
                .Append(ToGlobalTypeName(logicNode.Type))
                .Append(">(")
                .Append(SymbolDisplay.FormatLiteral(logicNode.LogicId, true))
                .AppendLine(");");
        }

        source.AppendLine("        }");
        source.AppendLine("    }");
        source.AppendLine("}");
        return source.ToString();
    }

    private static bool IsConcrete(INamedTypeSymbol symbol)
        => symbol.TypeKind == TypeKind.Class && !symbol.IsAbstract && !symbol.IsGenericType;

    private static IEnumerable<INamedTypeSymbol> DistinctTypes(
        IEnumerable<INamedTypeSymbol> symbols)
    {
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var symbol in symbols)
        {
            if (symbol != null && seen.Add(symbol))
            {
                yield return symbol;
            }
        }
    }

    private static bool IsOrDerivesFrom(INamedTypeSymbol symbol, INamedTypeSymbol baseType)
    {
        for (var current = symbol; current != null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAccessible(INamedTypeSymbol symbol)
    {
        for (var current = symbol; current != null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is not Accessibility.Public and not Accessibility.Internal)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasPublicParameterlessConstructor(INamedTypeSymbol symbol)
        => symbol.Constructors.Any(static constructor =>
            constructor.DeclaredAccessibility == Accessibility.Public && constructor.Parameters.Length == 0);

    private static string ToGlobalTypeName(INamedTypeSymbol symbol)
        => symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private readonly struct MessageRegistration
    {
        public MessageRegistration(int opcode, INamedTypeSymbol type)
        {
            Opcode = opcode;
            Type = type;
        }

        public int Opcode { get; }

        public INamedTypeSymbol Type { get; }
    }
}
