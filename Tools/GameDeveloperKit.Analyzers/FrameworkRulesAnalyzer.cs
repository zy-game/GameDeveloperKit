using System;
using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GameDeveloperKit.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class FrameworkRulesAnalyzer : DiagnosticAnalyzer
    {
        private static readonly ImmutableHashSet<string> NamingBaseline = ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "Assets/GameDeveloperKit/Runtime/App.cs|_registry",
            "Assets/GameDeveloperKit/Runtime/App.cs|_lifecycle",
            "Assets/GameDeveloperKit/Runtime/Core/ModuleLifecycle.cs|_registry",
            "Assets/GameDeveloperKit/Runtime/Core/ModuleLifecycle.cs|_state",
            "Assets/GameDeveloperKit/Runtime/Core/ModuleLifecycle.cs|_startupCompletion",
            "Assets/GameDeveloperKit/Runtime/Core/ModuleLifecycle.cs|_shutdownCompletion",
            "Assets/GameDeveloperKit/Runtime/Core/ModuleRegistry.cs|_modules",
            "Assets/GameDeveloperKit/Runtime/Core/ModuleRegistry.cs|_moduleOrder",
            "Assets/GameDeveloperKit/Runtime/Core/ModuleRegistry.cs|_assignableCache",
            "Assets/GameDeveloperKit/Runtime/Core/ModuleRegistry.cs|_assignableCacheDirty",
            "Assets/GameDeveloperKit/Runtime/Core/ModuleRegistry.cs|_isShuttingDown",
            "Assets/GameDeveloperKit/Runtime/Data/Serializers/JsonDataSerializer.cs|Settings",
            "Assets/GameDeveloperKit/Runtime/Debug/Console/DebugGuiDriver.cs|GuiTabs",
            "Assets/GameDeveloperKit/Runtime/Debug/DebugModule.cs|UnityTags",
            "Assets/GameDeveloperKit/Runtime/Debug/Profiles/DebugRedactionUtility.cs|SensitiveTokens",
            "Assets/GameDeveloperKit/Runtime/Debug/Profiles/MemoryProfileHandle.cs|BarColor",
            "Assets/GameDeveloperKit/Runtime/Operation/OperationHandle.cs|_progress",
            "Assets/GameDeveloperKit/Runtime/Operation/OperationHandle.cs|_error",
            "Assets/GameDeveloperKit/Runtime/Operation/OperationHandle.cs|_status",
            "Assets/GameDeveloperKit/Runtime/Operation/OperationHandle.cs|_progressHandle",
            "Assets/GameDeveloperKit/Runtime/Operation/OperationHandle.cs|_cts",
            "Assets/GameDeveloperKit/Runtime/Operation/OperationHandle.Generic.cs|_value",
            "Assets/GameDeveloperKit/Runtime/Resource/Provider/BuiltinAssetProvider.cs|_bundle",
            "Assets/GameDeveloperKit/Runtime/Resource/Provider/BundleAssetProvider.cs|_bundle",
            "Assets/GameDeveloperKit/Runtime/Resource/Provider/BundleAssetProvider.cs|_mode",
            "Assets/GameDeveloperKit/Runtime/Resource/Provider/BundleAssetProvider.cs|_manifestVersion",
            "Assets/GameDeveloperKit/Runtime/Resource/Provider/BundleAssetProvider.cs|_isRemote",
            "Assets/GameDeveloperKit/Runtime/Resource/Provider/EditorAssetProvider.cs|_bundle",
            "Assets/GameDeveloperKit/Runtime/Resource/Provider/NetworkAssetProvider.cs|ImageExtensions",
            "Assets/GameDeveloperKit/Runtime/Resource/ProviderBase.cs|_assets",
            "Assets/GameDeveloperKit/Runtime/Resource/ProviderBase.cs|_pendingUnloadAssets",
            "Assets/GameDeveloperKit/Runtime/Resource/ProviderBase.cs|_sceneUnloadEntries",
            "Assets/GameDeveloperKit/Runtime/Resource/ProviderBase.cs|_pendingAssetLoads",
            "Assets/GameDeveloperKit/Runtime/Resource/ProviderBase.cs|_pendingRawAssetLoads",
            "Assets/GameDeveloperKit/Runtime/Resource/ProviderBase.cs|_pendingSceneAssetLoads",
            "Assets/GameDeveloperKit/Runtime/Resource/ProviderBase.cs|_referenceCount",
            "Assets/GameDeveloperKit/Runtime/Resource/ProviderBase.cs|_acceptLoads",
            "Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs|_manifestIndex",
            "Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs|_setting",
            "Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs|_mode",
            "Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs|_providers",
            "Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs|_packageSessions",
            "Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs|_packageLifecycleGate",
            "Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs|_network",
            "Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs|_initializeState",
            "Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs|_initializeCompletion",
            "Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs|_initializeError",
            "Assets/GameDeveloperKit/Runtime/Resource/ResourceModule.cs|_startupError",
            "Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs|_timer",
            "Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs|_handles",
            "Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs|_dispatchBuffer",
            "Assets/GameDeveloperKit/Runtime/Timer/TimerModule.cs|_callbackHandles",
            "Assets/GameDeveloperKit/Runtime/UI/UIDocument.cs|fullScreenRoot",
            "Assets/GameDeveloperKit/Runtime/UI/UIDocument.cs|layerOrder",
            "Assets/GameDeveloperKit/Runtime/UI/UIDocument.cs|mappings",
            "Assets/GameDeveloperKit/Runtime/UI/UIDocument.cs|localizedTexts",
            "Assets/GameDeveloperKit/Runtime/UI/UIModule.cs|ReferenceResolution",
            "Assets/GameDeveloperKit/Runtime/UI/UIModule.cs|LayerOrder",
            "Assets/GameDeveloperKit/Runtime/Utility/AssetUtility.cs|_handles",
            "Assets/GameDeveloperKit/Editor/CodeAnalysis/AnalyzerDeploymentManifest.cs|schemaVersion",
            "Assets/GameDeveloperKit/Editor/CodeAnalysis/AnalyzerDeploymentManifest.cs|components",
            "Assets/GameDeveloperKit/Editor/CodeAnalysis/AnalyzerDeploymentManifest.cs|externalComponents",
            "Assets/GameDeveloperKit/Editor/CodeAnalysis/AnalyzerDeploymentManifest.cs|name",
            "Assets/GameDeveloperKit/Editor/CodeAnalysis/AnalyzerDeploymentManifest.cs|project",
            "Assets/GameDeveloperKit/Editor/CodeAnalysis/AnalyzerDeploymentManifest.cs|artifact",
            "Assets/GameDeveloperKit/Editor/CodeAnalysis/AnalyzerDeploymentManifest.cs|unityAsset");

        private static readonly ImmutableHashSet<string> PersistentDataMethods = ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "LoadDataAsync",
            "LoadVersionAsync",
            "SaveDataAsync",
            "RollbackDataAsync",
            "GetVersionsAsync",
            "DeleteDataAsync",
            "RegisterMigration");

        public const string FieldNamingId = "GDK_NAM001";
        public const string AsyncVoidId = "GDK_ASY001";
        public const string ForgetHandlerId = "GDK_ASY002";
        public const string EmptyCatchId = "GDK_EXC001";
        public const string RethrowId = "GDK_EXC002";
        public const string ModuleNameId = "GDK_MOD001";
        public const string RuntimeEditorApiId = "GDK_EDI001";
        public const string PhysicalIoId = "GDK_IO001";
        public const string DataPersistenceContractId = "GDK_DAT001";

        private static readonly DiagnosticDescriptor FieldNamingRule = CreateRule(
            FieldNamingId,
            "Field name does not follow GameDeveloperKit conventions",
            "Field '{0}' must use the '{1}' naming form");

        private static readonly DiagnosticDescriptor AsyncVoidRule = CreateRule(
            AsyncVoidId,
            "Fire-and-forget method has no observable completion",
            "Method '{0}' must return UniTask and expose or explicitly observe its completion");

        private static readonly DiagnosticDescriptor ForgetHandlerRule = CreateRule(
            ForgetHandlerId,
            "Forgotten UniTask has no exception handler",
            "UniTask.Forget must receive an exception handler");

        private static readonly DiagnosticDescriptor EmptyCatchRule = CreateRule(
            EmptyCatchId,
            "Catch block silently discards an exception",
            "Catch block must handle, report, wrap, or rethrow the exception");

        private static readonly DiagnosticDescriptor RethrowRule = CreateRule(
            RethrowId,
            "Rethrow resets the original stack trace",
            "Use 'throw;' instead of 'throw {0};'");

        private static readonly DiagnosticDescriptor ModuleNameRule = CreateRule(
            ModuleNameId,
            "Game module type has an invalid suffix",
            "Game module type '{0}' must end with 'Module'");

        private static readonly DiagnosticDescriptor RuntimeEditorApiRule = CreateRule(
            RuntimeEditorApiId,
            "Runtime source references UnityEditor",
            "Runtime source must not reference UnityEditor symbol '{0}'");

        private static readonly DiagnosticDescriptor PhysicalIoRule = CreateRule(
            PhysicalIoId,
            "Runtime source bypasses FileModule",
            "Runtime source must use FileModule instead of physical I/O type '{0}'");

        private static readonly DiagnosticDescriptor DataPersistenceContractRule = CreateRule(
            DataPersistenceContractId,
            "Persisted data type has no stable schema contract",
            "Persisted data type '{0}' must declare {1}");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            FieldNamingRule,
            AsyncVoidRule,
            ForgetHandlerRule,
            EmptyCatchRule,
            RethrowRule,
            ModuleNameRule,
            RuntimeEditorApiRule,
            PhysicalIoRule,
            DataPersistenceContractRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeLocalFunction, SyntaxKind.LocalFunctionStatement);
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeCatch, SyntaxKind.CatchClause);
            context.RegisterSyntaxNodeAction(AnalyzeThrow, SyntaxKind.ThrowStatement);
            context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeIdentifier, SyntaxKind.IdentifierName);
            context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        }

        private static void AnalyzeField(SyntaxNodeAnalysisContext context)
        {
            if (!ShouldAnalyze(context))
            {
                return;
            }

            var declaration = (FieldDeclarationSyntax)context.Node;
            foreach (var variable in declaration.Declaration.Variables)
            {
                var symbol = context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) as IFieldSymbol;
                if (symbol == null || !RequiresFrameworkFieldName(symbol))
                {
                    continue;
                }

                if (IsNamingBaseline(context, symbol.Name))
                {
                    continue;
                }

                var expected = symbol.IsConst ? "PascalCase" : symbol.IsStatic ? "s_PascalCase" : "m_PascalCase";
                if (HasExpectedFieldName(symbol.Name, symbol.IsConst, symbol.IsStatic))
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    FieldNamingRule,
                    variable.Identifier.GetLocation(),
                    symbol.Name,
                    expected));
            }
        }

        private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            if (!ShouldAnalyze(context))
            {
                return;
            }

            var method = (MethodDeclarationSyntax)context.Node;
            if (IsUnobservableAsync(context, method.Modifiers, method.ReturnType))
            {
                context.ReportDiagnostic(Diagnostic.Create(AsyncVoidRule, method.Identifier.GetLocation(), method.Identifier.ValueText));
            }
        }

        private static void AnalyzeLocalFunction(SyntaxNodeAnalysisContext context)
        {
            if (!ShouldAnalyze(context))
            {
                return;
            }

            var method = (LocalFunctionStatementSyntax)context.Node;
            if (IsUnobservableAsync(context, method.Modifiers, method.ReturnType))
            {
                context.ReportDiagnostic(Diagnostic.Create(AsyncVoidRule, method.Identifier.GetLocation(), method.Identifier.ValueText));
            }
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            if (!ShouldAnalyze(context))
            {
                return;
            }

            var invocation = (InvocationExpressionSyntax)context.Node;
            var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
            if (symbol == null)
            {
                return;
            }

            AnalyzeDataPersistenceInvocation(context, invocation, symbol);
            if (symbol.Name != "Forget")
            {
                return;
            }

            var namespaceName = symbol.ContainingNamespace?.ToDisplayString();
            if (namespaceName == null || !namespaceName.StartsWith("Cysharp.Threading.Tasks", StringComparison.Ordinal))
            {
                return;
            }

            var receiverType = symbol.ReducedFrom != null && symbol.ReducedFrom.Parameters.Length > 0
                ? symbol.ReducedFrom.Parameters[0].Type
                : symbol.Parameters.Length > 0
                    ? symbol.Parameters[0].Type
                    : null;
            if (receiverType?.Name == "UniTaskVoid")
            {
                return;
            }

            foreach (var parameter in symbol.Parameters)
            {
                if (parameter.Type.ToDisplayString() == "System.Action<System.Exception>")
                {
                    return;
                }
            }

            context.ReportDiagnostic(Diagnostic.Create(ForgetHandlerRule, invocation.GetLocation()));
        }

        private static void AnalyzeDataPersistenceInvocation(
            SyntaxNodeAnalysisContext context,
            InvocationExpressionSyntax invocation,
            IMethodSymbol method)
        {
            if (method.ContainingType?.ToDisplayString() != "GameDeveloperKit.Data.DataModule" ||
                !PersistentDataMethods.Contains(method.Name) ||
                method.TypeArguments.Length != 1 ||
                method.TypeArguments[0] is not INamedTypeSymbol dataType)
            {
                return;
            }

            var missingDataKey = !HasValidDataKey(dataType);
            var missingDataSchema = !HasValidDataSchema(dataType);
            if (!missingDataKey && !missingDataSchema)
            {
                return;
            }

            var missingContract = missingDataKey && missingDataSchema
                ? "valid DataKeyAttribute and DataSchemaAttribute"
                : missingDataKey
                    ? "a valid DataKeyAttribute"
                    : "a valid DataSchemaAttribute";
            context.ReportDiagnostic(Diagnostic.Create(
                DataPersistenceContractRule,
                invocation.GetLocation(),
                dataType.ToDisplayString(),
                missingContract));
        }

        private static void AnalyzeCatch(SyntaxNodeAnalysisContext context)
        {
            if (!ShouldAnalyze(context) || IsTestSource(context))
            {
                return;
            }

            var catchClause = (CatchClauseSyntax)context.Node;
            if (catchClause.Block.Statements.Count == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(EmptyCatchRule, catchClause.CatchKeyword.GetLocation()));
            }
        }

        private static void AnalyzeThrow(SyntaxNodeAnalysisContext context)
        {
            if (!ShouldAnalyze(context))
            {
                return;
            }

            var throwStatement = (ThrowStatementSyntax)context.Node;
            if (throwStatement.Expression is not IdentifierNameSyntax identifier)
            {
                return;
            }

            var catchClause = throwStatement.FirstAncestorOrSelf<CatchClauseSyntax>();
            if (catchClause?.Declaration?.Identifier.ValueText != identifier.Identifier.ValueText)
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(RethrowRule, throwStatement.GetLocation(), identifier.Identifier.ValueText));
        }

        private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
        {
            if (!ShouldAnalyze(context))
            {
                return;
            }

            var declaration = (ClassDeclarationSyntax)context.Node;
            var symbol = context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken);
            if (symbol == null || symbol.IsAbstract || !IsGameModule(symbol) || symbol.Name.EndsWith("Module", StringComparison.Ordinal))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(ModuleNameRule, declaration.Identifier.GetLocation(), symbol.Name));
        }

        private static void AnalyzeIdentifier(SyntaxNodeAnalysisContext context)
        {
            if (!ShouldAnalyze(context))
            {
                return;
            }

            var identifier = (IdentifierNameSyntax)context.Node;
            var symbol = context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol;
            var type = GetReferencedType(symbol);
            if (type == null)
            {
                return;
            }

            if (ShouldCheckEditorApi(context) && symbol is ITypeSymbol && IsUnityEditorType(type))
            {
                context.ReportDiagnostic(Diagnostic.Create(RuntimeEditorApiRule, identifier.GetLocation(), type.ToDisplayString()));
            }

            if (ShouldCheckPhysicalIo(context) && symbol is ITypeSymbol && IsPhysicalIoType(type))
            {
                context.ReportDiagnostic(Diagnostic.Create(PhysicalIoRule, identifier.GetLocation(), type.ToDisplayString()));
            }
        }

        private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
        {
            if (!ShouldAnalyze(context) || !ShouldCheckPhysicalIo(context))
            {
                return;
            }

            var creation = (ObjectCreationExpressionSyntax)context.Node;
            var type = context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type;
            if (type != null && IsPhysicalIoType(type))
            {
                context.ReportDiagnostic(Diagnostic.Create(PhysicalIoRule, creation.Type.GetLocation(), type.ToDisplayString()));
            }
        }

        private static DiagnosticDescriptor CreateRule(string id, string title, string message)
        {
            return new DiagnosticDescriptor(
                id,
                title,
                message,
                "GameDeveloperKit",
                DiagnosticSeverity.Error,
                true);
        }

        private static bool IsNamingBaseline(SyntaxNodeAnalysisContext context, string fieldName)
        {
            var path = NormalizePath(context.Node.SyntaxTree.FilePath);
            foreach (var entry in NamingBaseline)
            {
                var separator = entry.LastIndexOf('|');
                var suffix = entry.Substring(0, separator);
                var name = entry.Substring(separator + 1);
                if (string.Equals(name, fieldName, StringComparison.Ordinal) &&
                    path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldAnalyze(SyntaxNodeAnalysisContext context)
        {
            var path = NormalizePath(context.Node.SyntaxTree.FilePath);
            if (path.IndexOf("/Library/PackageCache/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/Packages/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/Assets/Plugins/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/Assets/GameDeveloperKit/Plugins/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!options.TryGetValue("gdk_analyzer_scope", out var scope))
            {
                return true;
            }

            return !string.Equals(scope, "excluded", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(scope, "generated", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(scope, "code_analysis", StringComparison.OrdinalIgnoreCase);
        }

        private static bool RequiresFrameworkFieldName(IFieldSymbol field)
        {
            if (field.IsImplicitlyDeclared)
            {
                return false;
            }

            return field.DeclaredAccessibility == Accessibility.Private ||
                   field.DeclaredAccessibility == Accessibility.Protected ||
                   field.DeclaredAccessibility == Accessibility.ProtectedAndInternal ||
                   field.DeclaredAccessibility == Accessibility.ProtectedOrInternal;
        }

        private static bool HasExpectedFieldName(string name, bool isConst, bool isStatic)
        {
            if (isConst)
            {
                return IsPascalCase(name);
            }

            var prefix = isStatic ? "s_" : "m_";
            return name.StartsWith(prefix, StringComparison.Ordinal) && IsPascalCase(name.Substring(prefix.Length));
        }

        private static bool IsPascalCase(string value)
        {
            return !string.IsNullOrEmpty(value) && char.IsUpper(value[0]);
        }

        private static bool IsUnobservableAsync(
            SyntaxNodeAnalysisContext context,
            SyntaxTokenList modifiers,
            TypeSyntax returnType)
        {
            var returnTypeSymbol = context.SemanticModel.GetTypeInfo(returnType, context.CancellationToken).Type;
            if (returnTypeSymbol?.Name == "UniTaskVoid" &&
                returnTypeSymbol.ContainingNamespace?.ToDisplayString() == "Cysharp.Threading.Tasks")
            {
                return true;
            }

            return modifiers.Any(SyntaxKind.AsyncKeyword) && returnType is PredefinedTypeSyntax predefined &&
                   predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);
        }

        private static bool IsGameModule(INamedTypeSymbol type)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                if (current.ToDisplayString() == "GameDeveloperKit.GameModuleBase")
                {
                    return true;
                }
            }

            foreach (var contract in type.AllInterfaces)
            {
                if (contract.ToDisplayString() == "GameDeveloperKit.IGameModule")
                {
                    return true;
                }
            }

            return false;
        }

        private static ITypeSymbol? GetReferencedType(ISymbol? symbol)
        {
            return symbol switch
            {
                ITypeSymbol type => type,
                IMethodSymbol method => method.ContainingType,
                IPropertySymbol property => property.ContainingType,
                IFieldSymbol field => field.ContainingType,
                IEventSymbol eventSymbol => eventSymbol.ContainingType,
                _ => null,
            };
        }

        private static bool ShouldCheckEditorApi(SyntaxNodeAnalysisContext context)
        {
            var path = NormalizePath(context.Node.SyntaxTree.FilePath);
            var assemblyName = context.Compilation.AssemblyName ?? string.Empty;
            var parseOptions = context.Node.SyntaxTree.Options as CSharpParseOptions;
            return HasPreprocessorSymbol(parseOptions, "UNITY_EDITOR") is false &&
                   !path.Contains("/Editor/") &&
                   !assemblyName.EndsWith(".Editor", StringComparison.Ordinal);
        }

        private static bool HasPreprocessorSymbol(CSharpParseOptions? options, string symbol)
        {
            if (options == null)
            {
                return false;
            }

            foreach (var value in options.PreprocessorSymbolNames)
            {
                if (string.Equals(value, symbol, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldCheckPhysicalIo(SyntaxNodeAnalysisContext context)
        {
            var path = NormalizePath(context.Node.SyntaxTree.FilePath);
            var assemblyName = context.Compilation.AssemblyName ?? string.Empty;
            if (path.Contains("/Editor/") || path.Contains("/Tests/") ||
                assemblyName.EndsWith(".Editor", StringComparison.Ordinal) ||
                assemblyName.EndsWith(".Tests", StringComparison.Ordinal))
            {
                return false;
            }

            var namespaceDeclaration = context.Node.FirstAncestorOrSelf<NamespaceDeclarationSyntax>();
            return namespaceDeclaration?.Name.ToString() != "GameDeveloperKit.File";
        }

        private static bool IsTestSource(SyntaxNodeAnalysisContext context)
        {
            var path = NormalizePath(context.Node.SyntaxTree.FilePath);
            var assemblyName = context.Compilation.AssemblyName ?? string.Empty;
            return path.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   assemblyName.EndsWith(".Tests", StringComparison.Ordinal);
        }

        private static bool IsUnityEditorType(ITypeSymbol type)
        {
            var namespaceName = type.ContainingNamespace?.ToDisplayString();
            return namespaceName != null &&
                   (namespaceName == "UnityEditor" || namespaceName.StartsWith("UnityEditor.", StringComparison.Ordinal));
        }

        private static bool IsPhysicalIoType(ITypeSymbol type)
        {
            if (type.ContainingNamespace?.ToDisplayString() != "System.IO")
            {
                return false;
            }

            return type.Name == "File" || type.Name == "Directory" || type.Name == "FileStream" ||
                   type.Name == "FileInfo" || type.Name == "DirectoryInfo";
        }

        private static bool HasValidDataKey(INamedTypeSymbol type)
        {
            var attribute = GetAttribute(type, "GameDeveloperKit.Data.DataKeyAttribute");
            return attribute != null &&
                   attribute.ConstructorArguments.Length == 1 &&
                   attribute.ConstructorArguments[0].Value is string key &&
                   !string.IsNullOrWhiteSpace(key);
        }

        private static bool HasValidDataSchema(INamedTypeSymbol type)
        {
            var attribute = GetAttribute(type, "GameDeveloperKit.Data.DataSchemaAttribute");
            return attribute != null &&
                   attribute.ConstructorArguments.Length == 1 &&
                   attribute.ConstructorArguments[0].Value is int version &&
                   version > 0;
        }

        private static AttributeData? GetAttribute(INamedTypeSymbol type, string attributeTypeName)
        {
            foreach (var attribute in type.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == attributeTypeName)
                {
                    return attribute;
                }
            }

            return null;
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }
    }
}
