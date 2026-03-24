using System.Collections.Generic;
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
            context.AddSource($"{binding.Event.Type.Name}.EventBindings.g.cs", SourceText.From(source, Encoding.UTF8));
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
        var keyExpr = binding.Event.Key.KeyExpression;

        sb.Append(indent).Append("public static class ").Append(eventName).AppendLine("GeneratedExtensions");
        sb.Append(indent).AppendLine("{");

        // BindingProvider
        sb.Append(indent).Append("    private sealed class ").Append(eventName).AppendLine("BindingProvider : global::GameDeveloperKit.Runtime.IEventBindingProvider");
        sb.Append(indent).AppendLine("    {");
        sb.Append(indent).AppendLine("        public void Register(global::GameDeveloperKit.Runtime.EventModule module)");
        sb.Append(indent).AppendLine("        {");

        foreach (var handler in binding.SyncHandlers)
            AppendRegister(sb, indent, keyExpr, binding.Event.Key.Kind, handler.ToDisplayString(), false);
        foreach (var handler in binding.AsyncHandlers)
            AppendRegister(sb, indent, keyExpr, binding.Event.Key.Kind, handler.ToDisplayString(), true);

        sb.Append(indent).AppendLine("        }");
        sb.Append(indent).AppendLine("    }");

        // Raise extension
        sb.Append(indent).Append("    public static void Raise").Append(eventName).AppendLine("(this global::GameDeveloperKit.Runtime.EventModule module, object sender = null, params object[] args)");
        sb.Append(indent).AppendLine("    {");
        sb.Append(indent).Append("        module.Raise(").Append(keyExpr).AppendLine(", sender, args);");
        sb.Append(indent).AppendLine("    }");

        // RaiseAsync extension
        sb.Append(indent).Append("    public static UniTask Raise").Append(eventName).AppendLine("Async(this global::GameDeveloperKit.Runtime.EventModule module, object sender = null, params object[] args)");
        sb.Append(indent).AppendLine("    {");
        sb.Append(indent).Append("        return module.RaiseAsync(").Append(keyExpr).AppendLine(", sender, args);");
        sb.Append(indent).AppendLine("    }");

        sb.Append(indent).AppendLine("}");

        if (ns != null)
            sb.AppendLine("}");

        return sb.ToString();
    }

    private static void AppendRegister(StringBuilder sb, string indent, string keyExpr, EventKeyKind keyKind, string handlerTypeName, bool isAsync)
    {
        sb.Append(indent).Append("        module.").Append(isAsync ? "RegisterAsync" : "Register");
        sb.Append("<global::").Append(handlerTypeName).Append('>');
        sb.Append('(').Append(keyExpr).AppendLine(");");
    }

    private static string Quote(string value)
        => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(value, true);

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
