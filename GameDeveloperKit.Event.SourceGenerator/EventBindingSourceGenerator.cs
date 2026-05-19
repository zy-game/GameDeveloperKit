using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace GameDeveloperKit.Event.SourceGenerator;

[Generator]
public sealed class EventBindingSourceGenerator : ISourceGenerator
{
    private const string EventBindingAttributeName = "GameDeveloperKit.EventBindingAttribute";
    private const string EventHandlerName = "GameDeveloperKit.IEventHandler";

    private static readonly DiagnosticDescriptor EmptyKeyRule = new DiagnosticDescriptor(
        "GDK_EVT001",
        "Event key is empty",
        "EventBindingAttribute key cannot be empty",
        "GameDeveloperKit.Event",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateHandlerRule = new DiagnosticDescriptor(
        "GDK_EVT002",
        "Duplicate event handler binding",
        "Handler '{0}' is bound to event key '{1}' more than once",
        "GameDeveloperKit.Event",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateEventRule = new DiagnosticDescriptor(
        "GDK_EVT004",
        "Duplicate event declaration",
        "Event key '{0}' is declared by both '{1}' and '{2}'",
        "GameDeveloperKit.Event",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingConstructorRule = new DiagnosticDescriptor(
        "GDK_EVT005",
        "Event handler cannot be auto registered",
        "Handler '{0}' must have a public parameterless constructor to be registered by the source generator",
        "GameDeveloperKit.Event",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(GeneratorInitializationContext context)
    {
    }

    public void Execute(GeneratorExecutionContext context)
    {
        var attributeSymbol = context.Compilation.GetTypeByMetadataName(EventBindingAttributeName);
        var handlerSymbol = context.Compilation.GetTypeByMetadataName(EventHandlerName);
        if (attributeSymbol == null || handlerSymbol == null)
            return;

        var allTypes = new List<INamedTypeSymbol>();
        CollectTypes(context.Compilation.Assembly.GlobalNamespace, allTypes);

        var eventDeclarations = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        var handlers = new Dictionary<string, List<INamedTypeSymbol>>(StringComparer.Ordinal);
        var handlerBindings = new HashSet<string>(StringComparer.Ordinal);

        foreach (var typeSymbol in allTypes)
        {
            foreach (var attribute in typeSymbol.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol))
                    continue;

                var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation();
                if (!TryGetKey(attribute, out var key) || string.IsNullOrWhiteSpace(key))
                {
                    context.ReportDiagnostic(Diagnostic.Create(EmptyKeyRule, location));
                    continue;
                }

                if (Implements(typeSymbol, handlerSymbol))
                {
                    var bindingId = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "|" + key;
                    if (!handlerBindings.Add(bindingId))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(DuplicateHandlerRule, location, typeSymbol.ToDisplayString(), key));
                        continue;
                    }

                    if (!HasPublicParameterlessConstructor(typeSymbol))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(MissingConstructorRule, location, typeSymbol.ToDisplayString()));
                        continue;
                    }

                    if (!handlers.TryGetValue(key, out var keyHandlers))
                    {
                        keyHandlers = new List<INamedTypeSymbol>();
                        handlers.Add(key, keyHandlers);
                    }

                    keyHandlers.Add(typeSymbol);
                }
                else
                {
                    if (eventDeclarations.TryGetValue(key, out var existing))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(DuplicateEventRule, location, key, existing.ToDisplayString(), typeSymbol.ToDisplayString()));
                        continue;
                    }

                    eventDeclarations.Add(key, typeSymbol);
                }
            }
        }

        context.AddSource("EventBindingGenerated.g.cs", SourceText.From(GenerateBindingSource(handlers), Encoding.UTF8));

        foreach (var declaration in eventDeclarations.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            context.AddSource($"{GetHintName(declaration.Value)}.EventBinding.g.cs", SourceText.From(GenerateEventSource(declaration.Key, declaration.Value), Encoding.UTF8));
        }
    }

    private static void CollectTypes(INamespaceSymbol ns, List<INamedTypeSymbol> result)
    {
        foreach (var member in ns.GetMembers())
        {
            if (member is INamespaceSymbol childNs)
            {
                CollectTypes(childNs, result);
            }
            else if (member is INamedTypeSymbol type)
            {
                CollectTypes(type, result);
            }
        }
    }

    private static void CollectTypes(INamedTypeSymbol type, List<INamedTypeSymbol> result)
    {
        result.Add(type);
        foreach (var nested in type.GetTypeMembers())
            CollectTypes(nested, result);
    }

    private static bool TryGetKey(AttributeData attribute, out string key)
    {
        if (attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string value)
        {
            key = value;
            return true;
        }

        key = null!;
        return false;
    }

    private static bool Implements(INamedTypeSymbol typeSymbol, INamedTypeSymbol interfaceSymbol)
    {
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, interfaceSymbol))
                return true;
        }

        return false;
    }

    private static bool HasPublicParameterlessConstructor(INamedTypeSymbol typeSymbol)
    {
        foreach (var ctor in typeSymbol.Constructors)
        {
            if (ctor.Parameters.Length == 0 && ctor.DeclaredAccessibility == Accessibility.Public)
                return true;
        }

        return false;
    }

    private static string GenerateBindingSource(Dictionary<string, List<INamedTypeSymbol>> handlers)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("namespace GameDeveloperKit");
        sb.AppendLine("{");
        sb.AppendLine("    internal static partial class EventBindingGenerated");
        sb.AppendLine("    {");
        sb.AppendLine("        static partial void RegisterAllGenerated(EventManager manager)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (manager == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                throw new global::System.ArgumentNullException(nameof(manager));");
        sb.AppendLine("            }");

        foreach (var pair in handlers.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            foreach (var handler in pair.Value.Select(static handler => handler.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).OrderBy(static name => name, StringComparer.Ordinal))
            {
                sb.Append("            manager.Register<").Append(ToGlobalTypeName(handler)).Append(">(").Append(Quote(pair.Key)).AppendLine(");");
            }
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateEventSource(string key, INamedTypeSymbol eventType)
    {
        var sb = new StringBuilder();
        var ns = eventType.ContainingNamespace.IsGlobalNamespace ? null : eventType.ContainingNamespace.ToDisplayString();
        var indent = ns == null ? string.Empty : "    ";
        var typeName = eventType.Name;
        var generatedTypeName = typeName + "Generated";
        var raiseName = "Raise" + typeName;

        sb.AppendLine("// <auto-generated />");
        if (ns != null)
        {
            sb.Append("namespace ").AppendLine(ns);
            sb.AppendLine("{");
        }

        sb.Append(indent).Append("public static partial class ").Append(generatedTypeName).AppendLine();
        sb.Append(indent).AppendLine("{");
        sb.Append(indent).Append("    public const string EventKey = ").Append(Quote(key)).AppendLine(";");
        sb.AppendLine();
        AppendRaiseMethod(sb, indent, raiseName, "object sender = null", "sender");
        sb.AppendLine();
        AppendRaiseMethod(sb, indent, raiseName, "object sender, object arg0", "sender, arg0");
        sb.AppendLine();
        AppendRaiseMethod(sb, indent, raiseName, "object sender, object arg0, object arg1", "sender, arg0, arg1");
        sb.AppendLine();
        AppendRaiseMethod(sb, indent, raiseName, "object sender = null, params object[] args", "sender, args");
        sb.Append(indent).AppendLine("}");

        if (ns != null)
            sb.AppendLine("}");

        return sb.ToString();
    }

    private static void AppendRaiseMethod(StringBuilder sb, string indent, string raiseName, string parameters, string invocationParameters)
    {
        sb.Append(indent).Append("    public static void ").Append(raiseName);
        sb.Append("(this global::GameDeveloperKit.EventManager manager, ").Append(parameters).AppendLine(")");
        sb.Append(indent).AppendLine("    {");
        sb.Append(indent).Append("        manager.Raise(EventKey, ").Append(invocationParameters).AppendLine(");");
        sb.Append(indent).AppendLine("    }");
    }

    private static string Quote(string value)
        => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(value, true);

    private static string GetHintName(INamedTypeSymbol typeSymbol)
    {
        var fullName = ToGlobalTypeName(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        var builder = new StringBuilder(fullName.Length);
        for (var i = 0; i < fullName.Length; i++)
        {
            var ch = fullName[i];
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        return builder.ToString();
    }

    private static string ToGlobalTypeName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return string.Empty;

        return typeName.StartsWith("global::") ? typeName : $"global::{typeName}";
    }
}
