using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace GameDeveloperKit.Resource
{
    internal sealed class WechatWebAssetBundleStrategy : ReflectedMiniGameWebAssetBundleStrategy
    {
        internal WechatWebAssetBundleStrategy()
            : base(
                "WeChat",
                "WeChatWASM.WXAssetBundle",
                "WeChatWASM.DownloadHandlerWXAssetBundle",
                "GetAssetBundle",
                "WXUnload")
        {
        }
    }

    internal sealed class TiktokWebAssetBundleStrategy : ReflectedMiniGameWebAssetBundleStrategy
    {
        internal TiktokWebAssetBundleStrategy()
            : base(
                "Douyin",
                "TTSDK.TTAssetBundle",
                "TTSDK.DownloadHandlerTTAssetBundle",
                "GetAssetBundle",
                "TTUnload")
        {
        }
    }

    /// <summary>
    /// Keeps optional mini-game SDK assemblies outside the framework asmdef contract while
    /// invoking the same request and unload APIs used by YooAsset's platform strategies.
    /// </summary>
    internal abstract class ReflectedMiniGameWebAssetBundleStrategy : IWebAssetBundleStrategy
    {
        private const BindingFlags StaticFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags InstanceFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private readonly string m_PlatformName;
        private readonly string m_LoaderTypeName;
        private readonly string m_DownloadHandlerTypeName;
        private readonly string m_CreateRequestMethodName;
        private readonly string m_UnloadMethodName;
        private Type m_LoaderType;
        private Type m_DownloadHandlerType;
        private MethodInfo m_CreateRequestMethod;
        private MethodInfo m_UnloadMethod;
        private PropertyInfo m_AssetBundleProperty;

        protected ReflectedMiniGameWebAssetBundleStrategy(
            string platformName,
            string loaderTypeName,
            string downloadHandlerTypeName,
            string createRequestMethodName,
            string unloadMethodName)
        {
            m_PlatformName = platformName;
            m_LoaderTypeName = loaderTypeName;
            m_DownloadHandlerTypeName = downloadHandlerTypeName;
            m_CreateRequestMethodName = createRequestMethodName;
            m_UnloadMethodName = unloadMethodName;
        }

        public UnityWebRequest CreateAssetBundleRequest(WebAssetBundleRequestOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.Url))
            {
                throw new ArgumentException("AssetBundle URL cannot be empty.", nameof(options));
            }

            var method = m_CreateRequestMethod ??= ResolveCreateRequestMethod();
            var result = Invoke(method, null, new object[] { options.Url }, "create an AssetBundle request");
            if (result is not UnityWebRequest request)
            {
                throw CreateSdkException(
                    $"{m_LoaderTypeName}.{m_CreateRequestMethodName}(string) returned no UnityWebRequest.");
            }

            request.disposeDownloadHandlerOnDispose = true;
            return request;
        }

        public AssetBundle ExtractAssetBundle(UnityWebRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var handler = request.downloadHandler ??
                          throw CreateSdkException("AssetBundle request has no download handler.");
            var handlerType = m_DownloadHandlerType ??= ResolveType(m_DownloadHandlerTypeName);
            if (!handlerType.IsInstanceOfType(handler))
            {
                throw CreateSdkException(
                    $"AssetBundle request uses '{handler.GetType().FullName}', expected " +
                    $"'{m_DownloadHandlerTypeName}'.");
            }

            var property = m_AssetBundleProperty ??= handlerType.GetProperty("assetBundle", InstanceFlags);
            if (property == null || !typeof(AssetBundle).IsAssignableFrom(property.PropertyType))
            {
                throw CreateSdkException(
                    $"Download handler '{handler.GetType().FullName}' does not expose an AssetBundle result.");
            }

            return Invoke(property.GetMethod, handler, null, "extract the AssetBundle") as AssetBundle;
        }

        public void UnloadAssetBundle(AssetBundle assetBundle, bool unloadAllLoadedObjects)
        {
            if (assetBundle == null)
            {
                return;
            }

            var method = m_UnloadMethod ??= ResolveUnloadMethod();
            Invoke(
                method,
                null,
                new object[] { assetBundle, unloadAllLoadedObjects },
                "unload the AssetBundle");
        }

        private MethodInfo ResolveCreateRequestMethod()
        {
            var loaderType = ResolveLoaderType();
            return loaderType.GetMethod(
                       m_CreateRequestMethodName,
                       StaticFlags,
                       null,
                       new[] { typeof(string) },
                       null) ??
                   throw CreateSdkException(
                       $"{m_LoaderTypeName}.{m_CreateRequestMethodName}(string) was not found.");
        }

        private MethodInfo ResolveUnloadMethod()
        {
            var loaderAssembly = ResolveLoaderType().Assembly;
            foreach (var type in GetLoadableTypes(loaderAssembly))
            {
                if (type == null || type.Namespace != ResolveNamespace(m_LoaderTypeName))
                {
                    continue;
                }

                var method = type.GetMethod(
                    m_UnloadMethodName,
                    StaticFlags,
                    null,
                    new[] { typeof(AssetBundle), typeof(bool) },
                    null);
                if (method != null)
                {
                    return method;
                }
            }

            throw CreateSdkException(
                $"Static {m_UnloadMethodName}(AssetBundle, bool) was not found in " +
                $"SDK assembly '{loaderAssembly.GetName().Name}'.");
        }

        private Type ResolveLoaderType()
        {
            if (m_LoaderType != null)
            {
                return m_LoaderType;
            }

            m_LoaderType = ResolveType(m_LoaderTypeName);
            return m_LoaderType;
        }

        private Type ResolveType(string typeName)
        {
            var resolved = Type.GetType(typeName, false);
            if (resolved != null)
            {
                return resolved;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                resolved = assembly.GetType(typeName, false);
                if (resolved != null)
                {
                    return resolved;
                }
            }

            throw CreateSdkException(
                $"SDK type '{typeName}' is unavailable. Install and enable the " +
                $"{m_PlatformName} mini-game conversion SDK for this build target.");
        }

        private object Invoke(MethodInfo method, object target, object[] arguments, string operation)
        {
            if (method == null)
            {
                throw CreateSdkException($"Unable to {operation}: SDK method is unavailable.");
            }

            try
            {
                return method.Invoke(target, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw CreateSdkException(
                    $"Failed to {operation}.",
                    exception.InnerException ?? exception);
            }
            catch (Exception exception)
            {
                throw CreateSdkException($"Failed to {operation}.", exception);
            }
        }

        private GameException CreateSdkException(string message, Exception innerException = null)
        {
            return innerException == null
                ? new GameException($"{m_PlatformName} mini-game AssetBundle SDK error: {message}")
                : new GameException(
                    $"{m_PlatformName} mini-game AssetBundle SDK error: {message}",
                    innerException);
        }

        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types;
            }
        }

        private static string ResolveNamespace(string typeName)
        {
            var separator = typeName.LastIndexOf('.');
            return separator < 0 ? string.Empty : typeName.Substring(0, separator);
        }
    }
}
