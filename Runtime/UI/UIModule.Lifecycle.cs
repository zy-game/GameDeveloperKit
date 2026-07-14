using System;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Cache;
using GameDeveloperKit.Debugger;
using GameDeveloperKit.Resource;
using GameDeveloperKit.UI.Internal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.UI
{
    public sealed partial class UIModule : GameModuleBase
    {
        /// <summary>
        /// 创建窗口缓存。
        /// </summary>
        private void CreateWindowCache()
        {
            if (App.TryGetRegistered<CacheModule>(out var cache) is false)
            {
                return;
            }

            m_WindowCache = cache.GetOrCreateBucket(new CacheBucketOptions<Type, UIWindowRecord>
            {
                Name = WindowCacheName,
                EvictionMode = CacheEvictionMode.Heat,
                TimeToLive = 30f,
                TimeToLiveProvider = GetWindowCacheTimeToLive,
                Capacity = 64,
                Finalizer = FinalizeCachedWindowAsync,
            });
        }

        /// <summary>
        /// 卸载 Asset Async。
        /// </summary>
        private static async UniTask UnloadAssetAsync(AssetHandle handle)
        {
            if (handle == null || handle.Info == null)
            {
                return;
            }

            await App.Resource.UnloadAsset(handle);
        }

        /// <summary>
        /// 执行 Close Record Async。
        /// </summary>
        private async UniTask CloseRecordAsync(UIWindowRecord record)
        {
            if (record == null || record.Status == UIWindowStatus.Closing)
            {
                return;
            }

            record.Status = UIWindowStatus.Closing;
            RemoveActiveRecord(record);
            if (ShouldCacheRecord(record))
            {
                await CloseRecordToCacheAsync(record);
                return;
            }

            await FinalReleaseRecordAsync(record);
        }

        /// <summary>
        /// 最终释放缓存窗口。
        /// </summary>
        private async UniTask FinalizeCachedWindowAsync(Type windowType, UIWindowRecord record)
        {
            if (record == null)
            {
                return;
            }

            if (record.Window != null && (record.Window.Document != null || record.Window.GameObject != null))
            {
                record.Window.Release();
            }

            DestroyGameObject(record.Instance);
            await UnloadAssetAsync(record.AssetHandle);
            record.Window = null;
            record.Document = null;
            record.Instance = null;
            record.AssetHandle = null;
        }

        /// <summary>
        /// 同步关闭窗口记录。
        /// </summary>
        private void CloseRecordImmediate(UIWindowRecord record)
        {
            if (record == null || record.Status == UIWindowStatus.Closing)
            {
                return;
            }

            record.Status = UIWindowStatus.Closing;
            RemoveActiveRecord(record);
            FinalReleaseRecordImmediate(record);
        }

        /// <summary>
        /// 最终释放窗口记录。
        /// </summary>
        private async UniTask FinalReleaseRecordAsync(UIWindowRecord record)
        {
            record.Window?.OnDisable();
            record.Window?.Release();
            DestroyGameObject(record.Instance);
            await UnloadAssetAsync(record.AssetHandle);

            record.Window = null;
            record.Document = null;
            record.Instance = null;
            record.AssetHandle = null;
        }

        /// <summary>
        /// 同步最终释放窗口记录。
        /// </summary>
        private void FinalReleaseRecordImmediate(UIWindowRecord record)
        {
            record.Window?.OnDisable();
            record.Window?.Release();
            DestroyGameObject(record.Instance);
            record.AssetHandle?.Release();

            record.Window = null;
            record.Document = null;
            record.Instance = null;
            record.AssetHandle = null;
        }

        /// <summary>
        /// 关闭窗口到缓存。
        /// </summary>
        private async UniTask CloseRecordToCacheAsync(UIWindowRecord record)
        {
            record.Window?.OnDisable();
            record.Window?.Release();
            MoveRecordToCache(record);
            record.Status = UIWindowStatus.Cached;
            if (m_WindowCache.TryPut(record.WindowType, record))
            {
                return;
            }

            await FinalizeCachedWindowAsync(record.WindowType, record);
        }

        /// <summary>
        /// 执行 Close After Pending Async。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        private async UniTask CloseAfterPendingAsync<T>(UniTask<UIWindow> pending) where T : UIWindow
        {
            try
            {
                await pending;
            }
            catch
            {
                return;
            }

            if (m_Records.TryGetValue(typeof(T), out var record))
            {
                await CloseRecordAsync(record);
            }
        }

        /// <summary>
        /// 尝试取出缓存窗口记录。
        /// </summary>
        private bool TryTakeCachedRecord(Type windowType, out UIWindowRecord record)
        {
            if (m_WindowCache != null && m_WindowCache.TryTake(windowType, out record))
            {
                return true;
            }

            record = null;
            return false;
        }

        /// <summary>
        /// 打开缓存窗口。
        /// </summary>
        private async UniTask<T> OpenCachedAsync<T>(UIWindowRecord record) where T : UIWindow
        {
            if (record == null || record.Window is not T window || record.Document == null || record.Instance == null)
            {
                await FinalizeCachedWindowAsync(typeof(T), record);
                throw new GameException($"Cached UI window '{typeof(T).Name}' is invalid.");
            }

            try
            {
                record.Status = UIWindowStatus.Loading;
                window.Initialize(record.Document, record.Instance, record.Layer);
                record.Instance.transform.SetParent(m_Layers[record.Layer], false);
                ApplyWindowRootLayout(record.Instance);
                ApplyWindowSorting(record.Instance, record.Layer);
                record.Instance.SetActive(true);
                RegisterDocument(record.Document);
                await window.OnAwakeAsync();
                DisableTopBeforePush(record);
                await window.OnOpenAsync();
                window.OnEnable();

                record.Status = UIWindowStatus.Opened;
                m_Records.Add(record.WindowType, record);
                m_LayerStacks[record.Layer].Push(record);
                PushBackStack(record);
                return window;
            }
            catch
            {
                UnregisterDocument(record.Document);
                record.Window?.Release();
                await FinalizeCachedWindowAsync(record.WindowType, record);
                throw;
            }
        }

        /// <summary>
        /// 执行 Close 并上报后台关闭异常。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        private async UniTaskVoid CloseAndReportAsync<T>() where T : UIWindow
        {
            try
            {
                await CloseAsync<T>();
            }
            catch (Exception exception)
            {
                ReportCloseException(typeof(T), exception);
            }
        }

        /// <summary>
        /// 上报后台关闭异常。
        /// </summary>
        /// <param name="windowType">window Type 参数。</param>
        private static void ReportCloseException(Type windowType, Exception exception)
        {
            if (App.TryGetRegistered<DebugModule>(out var debug))
            {
                debug.Error(exception, $"Failed to close UI window '{windowType.Name}'.", nameof(UIModule));
                return;
            }

            UnityEngine.Debug.LogException(exception);
        }

        /// <summary>
        /// 移除窗口活动记录。
        /// </summary>
        private void RemoveActiveRecord(UIWindowRecord record)
        {
            m_Records.Remove(record.WindowType);
            if (m_LayerStacks.TryGetValue(record.Layer, out var stack))
            {
                stack.Remove(record);
            }

            m_BackStack.Remove(record);
            UnregisterDocument(record.Document);
        }

        /// <summary>
        /// 执行 Should Cache Record。
        /// </summary>
        private bool ShouldCacheRecord(UIWindowRecord record)
        {
            return m_WindowCache != null
                && record?.Option != null
                && record.Option.CacheEnabled
                && record.Option.CacheStrategy != UICacheStrategy.None
                && record.Window != null
                && record.Instance != null;
        }

        /// <summary>
        /// 移动窗口记录到缓存根节点。
        /// </summary>
        private void MoveRecordToCache(UIWindowRecord record)
        {
            if (record.Instance == null)
            {
                return;
            }

            if (m_CacheRoot != null)
            {
                record.Instance.transform.SetParent(m_CacheRoot, false);
            }

            record.Instance.SetActive(false);
        }

        /// <summary>
        /// 销毁 Game Object。
        /// </summary>
        /// <param name="gameObject">game Object 参数。</param>
        private static void DestroyGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(gameObject);
            }
            else
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static float GetWindowCacheTimeToLive(Type windowType, UIWindowRecord record)
        {
            if (record?.Option == null || record.Option.CacheStrategy != UICacheStrategy.Time)
            {
                return 0f;
            }

            if (record.Option.CacheTimeToLive <= 0f)
            {
                return 30f;
            }

            return record.Option.CacheTimeToLive;
        }

        /// <summary>
        /// 清理窗口缓存。
        /// </summary>
        private void ClearWindowCacheImmediate()
        {
            if (m_WindowCache == null)
            {
                return;
            }

            m_WindowCache.ClearAsync().Forget(ReportCacheException);
            m_WindowCache = null;
        }

        /// <summary>
        /// 上报缓存清理异常。
        /// </summary>
        private static void ReportCacheException(Exception exception)
        {
            if (App.TryGetRegistered<DebugModule>(out var debug))
            {
                debug.Error(exception, "Failed to clear cached UI windows.", nameof(UIModule));
                return;
            }

            UnityEngine.Debug.LogException(exception);
        }
    }
}
