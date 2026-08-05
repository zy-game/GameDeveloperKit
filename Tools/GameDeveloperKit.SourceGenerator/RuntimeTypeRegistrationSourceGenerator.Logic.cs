using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GameDeveloperKit.SourceGenerator;

public sealed partial class RuntimeTypeRegistrationSourceGenerator
{
    private const string LogicNodeAttributeName = "GameDeveloperKit.Story.Logic.LogicNodeAttribute";
    private const string LogicNodeInterfaceName = "GameDeveloperKit.Story.Logic.ILogicNode";
    private const string OutputPortAttributeName = "GameDeveloperKit.Story.Logic.OutputPortAttribute";
    private const string LogicParameterAttributeName = "GameDeveloperKit.Story.Logic.LogicParameterAttribute";

    private static readonly DiagnosticDescriptor InvalidLogicTypeRule = new DiagnosticDescriptor(
        "GDK_GEN101",
        "Logic node type is invalid",
        "Logic node '{0}' must be an accessible concrete non-generic ILogicNode with a public parameterless constructor",
        "GameDeveloperKit.Generation",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidLogicIdentityRule = new DiagnosticDescriptor(
        "GDK_GEN102",
        "Logic node identity is invalid",
        "Logic node '{0}' must declare a non-empty LogicId and display name",
        "GameDeveloperKit.Generation",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateLogicIdentityRule = new DiagnosticDescriptor(
        "GDK_GEN103",
        "Logic node identity is duplicated",
        "Logic node ID '{0}' is declared by both '{1}' and '{2}'",
        "GameDeveloperKit.Generation",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidLogicOutputRule = new DiagnosticDescriptor(
        "GDK_GEN104",
        "Logic node output is invalid",
        "Logic node '{0}' has invalid output metadata: {1}",
        "GameDeveloperKit.Generation",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidLogicParameterRule = new DiagnosticDescriptor(
        "GDK_GEN105",
        "Logic node parameter is invalid",
        "Logic node '{0}' has invalid parameter metadata: {1}",
        "GameDeveloperKit.Generation",
        DiagnosticSeverity.Error,
        true);

    private static IReadOnlyList<LogicNodeRegistration> CollectLogicNodes(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol> candidates)
    {
        var logicNodeInterface = compilation.GetTypeByMetadataName(LogicNodeInterfaceName);
        var logicNodeAttribute = compilation.GetTypeByMetadataName(LogicNodeAttributeName);
        var outputPortAttribute = compilation.GetTypeByMetadataName(OutputPortAttributeName);
        var parameterAttribute = compilation.GetTypeByMetadataName(LogicParameterAttributeName);
        if (logicNodeInterface == null || logicNodeAttribute == null ||
            outputPortAttribute == null || parameterAttribute == null)
        {
            return Array.Empty<LogicNodeRegistration>();
        }

        var registrations = new List<LogicNodeRegistration>();
        var byId = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        foreach (var candidate in DistinctTypes(candidates)
                     .OrderBy(static symbol => symbol.ToDisplayString(), StringComparer.Ordinal))
        {
            var attribute = candidate.GetAttributes().FirstOrDefault(item =>
                SymbolEqualityComparer.Default.Equals(item.AttributeClass, logicNodeAttribute));
            if (attribute == null)
            {
                continue;
            }

            var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation();
            if (!IsConcrete(candidate) || !IsAccessible(candidate) ||
                !HasPublicParameterlessConstructor(candidate) ||
                !candidate.AllInterfaces.Any(item => SymbolEqualityComparer.Default.Equals(item, logicNodeInterface)))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidLogicTypeRule,
                    location,
                    candidate.ToDisplayString()));
                continue;
            }

            var logicId = ConstructorString(attribute, 0);
            var displayName = ConstructorString(attribute, 1);
            if (string.IsNullOrWhiteSpace(logicId) || string.IsNullOrWhiteSpace(displayName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidLogicIdentityRule,
                    location,
                    candidate.ToDisplayString()));
                continue;
            }

            logicId = logicId!.Trim();
            if (byId.TryGetValue(logicId, out var existing))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateLogicIdentityRule,
                    location,
                    logicId,
                    existing.ToDisplayString(),
                    candidate.ToDisplayString()));
                continue;
            }

            if (!ValidateOutputs(candidate, outputPortAttribute, out var outputError))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidLogicOutputRule,
                    location,
                    candidate.ToDisplayString(),
                    outputError));
                continue;
            }

            if (!ValidateParameters(candidate, parameterAttribute, out var parameterError))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidLogicParameterRule,
                    location,
                    candidate.ToDisplayString(),
                    parameterError));
                continue;
            }

            byId.Add(logicId, candidate);
            registrations.Add(new LogicNodeRegistration(logicId, candidate));
        }

        return registrations;
    }

    private static bool ValidateOutputs(
        INamedTypeSymbol candidate,
        INamedTypeSymbol outputPortAttribute,
        out string error)
    {
        var outputs = candidate.GetAttributes()
            .Where(item => SymbolEqualityComparer.Default.Equals(item.AttributeClass, outputPortAttribute))
            .ToArray();
        if (outputs.Length == 0)
        {
            error = "at least one OutputPortAttribute is required";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < outputs.Length; i++)
        {
            var portId = ConstructorString(outputs[i], 0)?.Trim();
            if (string.IsNullOrWhiteSpace(portId))
            {
                error = $"output at index {i} has an empty ID";
                return false;
            }

            if (string.Equals(portId, "in", StringComparison.Ordinal) || !ids.Add(portId!))
            {
                error = $"output ID '{portId}' is reserved or duplicated";
                return false;
            }
        }

        error = null!;
        return true;
    }

    private static bool ValidateParameters(
        INamedTypeSymbol candidate,
        INamedTypeSymbol parameterAttribute,
        out string error)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var attribute in candidate.GetAttributes().Where(item =>
                     SymbolEqualityComparer.Default.Equals(item.AttributeClass, parameterAttribute)))
        {
            var key = ConstructorString(attribute, 0)?.Trim();
            if (string.IsNullOrWhiteSpace(key) ||
                string.Equals(key, "logicId", StringComparison.Ordinal) ||
                string.Equals(key, "__logicNode", StringComparison.Ordinal) ||
                !keys.Add(key!))
            {
                error = $"parameter key '{key}' is empty, reserved, or duplicated";
                return false;
            }

            var valueType = ConstructorInt(attribute, 2, 0);
            if (valueType is < 0 or > 4)
            {
                error = $"parameter '{key}' has an unsupported value type";
                return false;
            }

            if (valueType == 3 && !HasUniqueNonEmptyOptions(attribute))
            {
                error = $"option parameter '{key}' requires non-empty unique Options";
                return false;
            }

            if (valueType == 4 && string.IsNullOrWhiteSpace(NamedString(attribute, "ResourceType")))
            {
                error = $"asset parameter '{key}' requires ResourceType";
                return false;
            }
        }

        error = null!;
        return true;
    }

    private static bool HasUniqueNonEmptyOptions(AttributeData attribute)
    {
        var options = NamedArray(attribute, "Options");
        if (options.IsDefaultOrEmpty)
        {
            return false;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var option in options)
        {
            if (option.Value is not string value ||
                string.IsNullOrWhiteSpace(value) ||
                !seen.Add(value))
            {
                return false;
            }
        }

        return true;
    }

    private static string? ConstructorString(AttributeData attribute, int index)
    {
        return attribute.ConstructorArguments.Length > index
            ? attribute.ConstructorArguments[index].Value as string
            : null;
    }

    private static int ConstructorInt(AttributeData attribute, int index, int fallback)
    {
        if (attribute.ConstructorArguments.Length <= index)
        {
            return fallback;
        }

        return attribute.ConstructorArguments[index].Value is int value ? value : fallback;
    }

    private static string? NamedString(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (string.Equals(argument.Key, name, StringComparison.Ordinal))
            {
                return argument.Value.Value as string;
            }
        }

        return null;
    }

    private static ImmutableArray<TypedConstant> NamedArray(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (string.Equals(argument.Key, name, StringComparison.Ordinal))
            {
                return argument.Value.Values;
            }
        }

        return ImmutableArray<TypedConstant>.Empty;
    }

    private readonly struct LogicNodeRegistration
    {
        public LogicNodeRegistration(string logicId, INamedTypeSymbol type)
        {
            LogicId = logicId;
            Type = type;
        }

        public string LogicId { get; }

        public INamedTypeSymbol Type { get; }
    }
}
