using System;
using System.IO;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源包上下文，封装资源包定义、运行模式及更新状态信息。
    /// </summary>
    public sealed class ResourcePackageContext
    {
        /// <summary>
        /// 初始化 <see cref="ResourcePackageContext"/> 类的新实例。
        /// </summary>
        /// <param name="playMode">资源包运行模式。</param>
        /// <param name="definition">资源包定义。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="definition"/> 为 <c>null</c> 时抛出。</exception>
        public ResourcePackageContext(ResourcePlayMode playMode, ResourcePackageDefinition definition)
        {
            PlayMode = playMode;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        /// <summary>
        /// 获取资源包运行模式。
        /// </summary>
        public ResourcePlayMode PlayMode { get; }

        /// <summary>
        /// 获取资源包定义。
        /// </summary>
        public ResourcePackageDefinition Definition { get; }

        /// <summary>
        /// 获取资源包名称。
        /// </summary>
        public string PackageName => Definition.PackageName;

        /// <summary>
        /// 获取资源包角色。
        /// </summary>
        public ResourcePackageRole Role => Definition.Role;

        /// <summary>
        /// 获取 StreamingAssets 根目录。
        /// </summary>
        public string StreamingAssetsRoot => ResolveRoot(Application.streamingAssetsPath, Definition.ResolveStreamingAssetsRelativeRoot());

        /// <summary>
        /// 获取持久化目录根路径。
        /// </summary>
        public string PersistentRoot => ResolveRoot(Application.persistentDataPath, Definition.ResolvePersistentRelativeRoot());

        /// <summary>
        /// 获取远端资源基础地址。
        /// </summary>
        public string RemoteBaseUrl => Definition.RemoteBaseUrl;

        /// <summary>
        /// 获取资源清单相对路径。
        /// </summary>
        public string ManifestRelativePath => Definition.ResolveManifestRelativePath();

        /// <summary>
        /// 获取最近一次资源更新报告。
        /// </summary>
        public ResourceUpdateReport LastUpdateReport { get; internal set; } = new ResourceUpdateReport
        {
            State = ResourceUpdateState.Idle
        };

        /// <summary>
        /// 重置更新报告并切换到初始更新状态。
        /// </summary>
        /// <param name="initialState">重置后要进入的初始状态。</param>
        /// <param name="stage">当前操作阶段。</param>
        /// <param name="localManifestVersion">本地清单版本号。</param>
        /// <param name="remoteManifestVersion">远端清单版本号。</param>
        /// <param name="message">状态切换附加消息。</param>
        public void ResetUpdateReport(ResourceUpdateState initialState, string stage, string localManifestVersion = null, string remoteManifestVersion = null, string message = null)
        {
            LastUpdateReport = new ResourceUpdateReport
            {
                State = ResourceUpdateState.Idle,
                Stage = stage,
                LocalManifestVersion = localManifestVersion,
                RemoteManifestVersion = remoteManifestVersion
            };

            TransitionUpdateState(initialState, stage, message);
        }

        /// <summary>
        /// 记录资源更新状态切换信息。
        /// </summary>
        /// <param name="state">目标更新状态。</param>
        /// <param name="stage">当前操作阶段。</param>
        /// <param name="message">状态切换附加消息。</param>
        /// <param name="error">关联的错误信息。</param>
        public void TransitionUpdateState(ResourceUpdateState state, string stage, string message = null, GameFrameworkException error = null)
        {
            LastUpdateReport ??= new ResourceUpdateReport
            {
                State = ResourceUpdateState.Idle
            };

            var previousState = LastUpdateReport.State;
            LastUpdateReport.PreviousState = previousState;
            LastUpdateReport.State = state;
            LastUpdateReport.Stage = stage;
            if (error != null)
            {
                LastUpdateReport.Error = error;
                LastUpdateReport.ErrorMessage = error.Message;
            }

            LastUpdateReport.Transitions.Add(new ResourceUpdateTransition
            {
                PreviousState = previousState,
                State = state,
                Stage = stage,
                Message = message ?? string.Empty,
                TimestampUtc = DateTime.UtcNow.ToString("O")
            });
        }

        /// <summary>
        /// 解析 StreamingAssets 中的资源路径。
        /// </summary>
        /// <param name="relativePath">资源相对路径。</param>
        /// <returns>解析后的完整路径。</returns>
        public string ResolveStreamingAssetsPath(string relativePath)
        {
            return Combine(StreamingAssetsRoot, relativePath);
        }

        /// <summary>
        /// 解析持久化目录中的资源路径。
        /// </summary>
        /// <param name="relativePath">资源相对路径。</param>
        /// <returns>解析后的完整路径。</returns>
        public string ResolvePersistentPath(string relativePath)
        {
            return Combine(PersistentRoot, relativePath);
        }

        /// <summary>
        /// 解析远端资源访问地址。
        /// </summary>
        /// <param name="relativePath">资源相对路径。</param>
        /// <returns>解析后的远端资源地址。</returns>
        public string ResolveRemoteUrl(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(RemoteBaseUrl))
            {
                return relativePath ?? string.Empty;
            }

            return $"{RemoteBaseUrl.TrimEnd('/')}/{relativePath?.Replace('\\', '/').TrimStart('/')}";
        }

        private static string Combine(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return root ?? string.Empty;
            }

            if (Path.IsPathRooted(relativePath))
            {
                return relativePath;
            }

            return string.IsNullOrWhiteSpace(root) ? relativePath : Path.Combine(root, relativePath);
        }

        private static string ResolveRoot(string defaultRoot, string configuredRoot)
        {
            if (string.IsNullOrWhiteSpace(configuredRoot))
            {
                return defaultRoot ?? string.Empty;
            }

            if (Path.IsPathRooted(configuredRoot))
            {
                return configuredRoot;
            }

            return string.IsNullOrWhiteSpace(defaultRoot) ? configuredRoot : Path.Combine(defaultRoot, configuredRoot);
        }
    }
}



