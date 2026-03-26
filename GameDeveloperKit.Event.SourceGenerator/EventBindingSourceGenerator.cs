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
    private const string GameEventAttributeName = "GameDeveloperKit.Runtime.GameEventAttribute";
    private const string EventHandleName = "GameDeveloperKit.Runtime.IEventHandle";
    private const string AsyncEventHandleName = "GameDeveloperKit.Runtime.IAsyncEventHandle";

    public void Initialize(GeneratorInitializationContext context)
    {
    }

    public void Execute(GeneratorExecutionContext context)
    {
        var eventAttributeSymbol = context.Compilation.GetTypeByMetadataName(GameEventAttributeName);
        var eventHandleSymbol = context.Compilation.GetTypeByMetadataName(EventHandleName);
        var asyncEventHandleSymbol = context.Compilation.GetTypeByMetadataName(AsyncEventHandleName);

        if (eventAttributeSymbol == null || eventHandleSymbol == null || asyncEventHandleSymbol == null)
            return;

        var allTypes = new List<INamedTypeSymbol>();
        CollectTypes(context.Compilation.Assembly.GlobalNamespace, allTypes);

        // First pass: collect event declarations (string/int key)
        var eventDeclarations = new Dictionary<INamedTypeSymbol, EventDescriptor>(SymbolEqualityComparer.Default);
        foreach (var typeSymbol in allTypes)
        {
            if (TryCreateEventDescriptor(typeSymbol, eventAttributeSymbol, out var descriptor))
                eventDeclarations[typeSymbol] = descriptor;
        }

        if (eventDeclarations.Count == 0)
            return;

        var bindings = new Dictionary<INamedTypeSymbol, EventBindings>(SymbolEqualityComparer.Default);
        foreach (var pair in eventDeclarations)
            bindings[pair.Key] = new EventBindings(pair.Value);

        // Second pass: collect handlers ([GameEvent(typeof(EventClass))])
        foreach (var typeSymbol in allTypes)
        {
            foreach (var attribute in typeSymbol.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, eventAttributeSymbol))
                    continue;

                if (attribute.ConstructorArguments.Length != 1)
                    continue;

                var arg = attribute.ConstructorArguments[0];
                if (arg.Kind != TypedConstantKind.Type)
                    continue;

                var eventType = arg.Value as INamedTypeSymbol;
                if (eventType == null || !bindings.TryGetValue(eventType, out var eventBindings))
                    continue;

                if (Implements(typeSymbol, eventHandleSymbol))
                    eventBindings.SyncHandlers.Add(typeSymbol);

                if (Implements(typeSymbol, asyncEventHandleSymbol))
                    eventBindings.AsyncHandlers.Add(typeSymbol);
            }
        }

        foreach (var binding in bindings.Values)
        {
            var source = GenerateSource(binding);
            context.AddSource($"{GetHintName(binding.Event.Type)}.EventBindings.g.cs", SourceText.From(source, Encoding.UTF8));
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

    private static bool TryCreateEventDescriptor(INamedTypeSymbol typeSymbol, INamedTypeSymbol eventAttributeSymbol, out EventDescriptor descriptor)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, eventAttributeSymbol))
                continue;

            if (attribute.ConstructorArguments.Length != 1)
                continue;

            var arg = attribute.ConstructorArguments[0];
            if (arg.Kind != TypedConstantKind.Primitive)
                continue;

            EventKeyDescriptor key;
            if (arg.Value is string stringValue)
                key = new EventKeyDescriptor(EventKeyKind.String, Quote(stringValue));
            else if (arg.Value is int intValue)
                key = new EventKeyDescriptor(EventKeyKind.Int, intValue.ToString());
            else
                continue;

            descriptor = new EventDescriptor(typeSymbol, key);
            return true;
        }

        descriptor = null!;
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

    private static string GenerateSource(EventBindings binding)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using Cysharp.Threading.Tasks;");

        var ns = binding.Event.Type.ContainingNamespace.IsGlobalNamespace
            ? null
            : binding.Event.Type.ContainingNamespace.ToDisplayString();

        var indent = ns != null ? "    " : "";

        if (ns != null)
        {
            sb.Append("namespace ").AppendLine(ns);
            sb.AppendLine("{");
        }

        var eventName = binding.Event.Type.Name;
        var providerName = $"{eventName}GeneratedBindingProvider";
        var generatedTypeName = $"{eventName}Generated";
        var syncHandlers = binding.SyncHandlers
            .Select(static handler => handler.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .OrderBy(static handler => handler, StringComparer.Ordinal)
            .ToList();
        var asyncHandlers = binding.AsyncHandlers
            .Select(static handler => handler.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .OrderBy(static handler => handler, StringComparer.Ordinal)
            .ToList();

        sb.Append(indent).Append("public sealed class ").Append(providerName).AppendLine(" : global::GameDeveloperKit.Runtime.IEventBindingProvider");
        sb.Append(indent).AppendLine("    {");
        sb.Append(indent).AppendLine("        public void Register(global::GameDeveloperKit.Runtime.EventModule module)");
        sb.Append(indent).AppendLine("        {");
        sb.Append(indent).Append("            ").Append(generatedTypeName).AppendLine(".RegisterHandlers(module);");
        sb.Append(indent).AppendLine("        }");
        sb.Append(indent).AppendLine("    }");

        sb.AppendLine();
        sb.Append(indent).Append("public static partial class ").Append(generatedTypeName).AppendLine();
        sb.Append(indent).AppendLine("{");
        AppendEventKeyDefinition(sb, indent, binding.Event.Key);
        sb.AppendLine();
        AppendRegisterMethod(sb, indent, syncHandlers, asyncHandlers);
        sb.AppendLine();
        AppendRegisterGroupMethod(sb, indent, syncHandlers, false);
        sb.AppendLine();
        AppendRegisterGroupMethod(sb, indent, asyncHandlers, true);
        sb.AppendLine();
        AppendRaiseMethods(sb, indent, eventName);
        sb.Append(indent).AppendLine("}");

        if (ns != null)
            sb.AppendLine("}");

        return sb.ToString();
    }

    private static void AppendRegister(StringBuilder sb, string indent, string handlerTypeName, bool isAsync)
    {
        sb.Append(indent).Append("            module.").Append(isAsync ? "RegisterAsync" : "Register");
        sb.Append('<').Append(ToGlobalTypeName(handlerTypeName)).Append('>');
        sb.Append("(EventKey);").AppendLine();
    }

    private static void AppendEventKeyDefinition(StringBuilder sb, string indent, EventKeyDescriptor key)
    {
        var keyTypeName = key.Kind == EventKeyKind.String ? "string" : "int";
        sb.Append(indent).Append("    public const ").Append(keyTypeName).Append(" EventKey = ").Append(key.KeyExpression).AppendLine(";");
    }

    private static void AppendRegisterMethod(StringBuilder sb, string indent, IReadOnlyList<string> syncHandlers, IReadOnlyList<string> asyncHandlers)
    {
        sb.Append(indent).AppendLine("    public static void RegisterHandlers(global::GameDeveloperKit.Runtime.EventModule module)");
        sb.Append(indent).AppendLine("    {");
        sb.Append(indent).AppendLine("        if (module == null)");
        sb.Append(indent).AppendLine("        {");
        sb.Append(indent).AppendLine("            throw new global::System.ArgumentNullException(nameof(module));");
        sb.Append(indent).AppendLine("        }");
        sb.AppendLine();
        sb.Append(indent).AppendLine("        RegisterSyncHandlers(module);");
        sb.Append(indent).AppendLine("        RegisterAsyncHandlers(module);");
        sb.Append(indent).AppendLine("    }");
    }

    private static void AppendRegisterGroupMethod(StringBuilder sb, string indent, IReadOnlyList<string> handlers, bool isAsync)
    {
        sb.Append(indent).Append("    public static void ").Append(isAsync ? "RegisterAsyncHandlers" : "RegisterSyncHandlers").AppendLine("(global::GameDeveloperKit.Runtime.EventModule module)");
        sb.Append(indent).AppendLine("    {");
        sb.Append(indent).AppendLine("        if (module == null)");
        sb.Append(indent).AppendLine("        {");
        sb.Append(indent).AppendLine("            throw new global::System.ArgumentNullException(nameof(module));");
        sb.Append(indent).AppendLine("        }");

        if (handlers.Count == 0)
        {
            sb.Append(indent).AppendLine("    }");
            return;
        }

        sb.AppendLine();
        foreach (var handler in handlers)
            AppendRegister(sb, indent, handler, isAsync);
        sb.Append(indent).AppendLine("    }");
    }

    private static void AppendRaiseMethods(StringBuilder sb, string indent, string eventName)
    {
        AppendRaiseMethod(sb, indent, eventName, false, "object sender = null", "sender");
        sb.AppendLine();
        AppendRaiseMethod(sb, indent, eventName, false, "object sender, object arg0", "sender, arg0");
        sb.AppendLine();
        AppendRaiseMethod(sb, indent, eventName, false, "object sender, object arg0, object arg1", "sender, arg0, arg1");
        sb.AppendLine();
        AppendRaiseMethod(sb, indent, eventName, false, "object sender = null, params object[] args", "sender, args");
        sb.AppendLine();
        AppendRaiseMethod(sb, indent, eventName, true, "object sender = null", "sender");
        sb.AppendLine();
        AppendRaiseMethod(sb, indent, eventName, true, "object sender, object arg0", "sender, arg0");
        sb.AppendLine();
        AppendRaiseMethod(sb, indent, eventName, true, "object sender, object arg0, object arg1", "sender, arg0, arg1");
        sb.AppendLine();
        AppendRaiseMethod(sb, indent, eventName, true, "object sender = null, params object[] args", "sender, args");
    }

    private static void AppendRaiseMethod(StringBuilder sb, string indent, string eventName, bool isAsync, string parameters, string invocationParameters)
    {
        sb.Append(indent).Append("    public static ");
        sb.Append(isAsync ? "UniTask " : "void ");
        sb.Append("Raise").Append(eventName).Append(isAsync ? "Async" : string.Empty);
        sb.Append("(this global::GameDeveloperKit.Runtime.EventModule module, ").Append(parameters).AppendLine(")");
        sb.Append(indent).AppendLine("    {");
        if (isAsync)
        {
            sb.Append(indent).Append("        return module.RaiseAsync(EventKey, ").Append(invocationParameters).AppendLine(");");
        }
        else
        {
            sb.Append(indent).Append("        module.Raise(EventKey, ").Append(invocationParameters).AppendLine(");");
        }
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

    private sealed class EventDescriptor
    {
        public EventDescriptor(INamedTypeSymbol type, EventKeyDescriptor key) { Type = type; Key = key; }
        public INamedTypeSymbol Type { get; }
        public EventKeyDescriptor Key { get; }
    }

    private sealed class EventKeyDescriptor
    {
        public EventKeyDescriptor(EventKeyKind kind, string keyExpression) { Kind = kind; KeyExpression = keyExpression; }
        public EventKeyKind Kind { get; }
        public string KeyExpression { get; }
    }

    private enum EventKeyKind { String, Int }

    private sealed class EventBindings
    {
        public EventBindings(EventDescriptor @event) { Event = @event; }
        public EventDescriptor Event { get; }
        public List<INamedTypeSymbol> SyncHandlers { get; } = new List<INamedTypeSymbol>();
        public List<INamedTypeSymbol> AsyncHandlers { get; } = new List<INamedTypeSymbol>();
    }
}
