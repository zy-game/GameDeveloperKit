using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Config;
using GameDeveloperKit.Data;
using GameDeveloperKit.Procedure;
using GameDeveloperKit.Resource;
using GameDeveloperKit.Sound;
using GameDeveloperKit.UI;
using Newtonsoft.Json;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GameDeveloperKit.Tests
{
    public sealed class FrameworkStartupTests : RuntimeTestBase
    {
        private readonly List<GameObject> m_GameObjects = new List<GameObject>();
        private readonly List<UnityEngine.Object> m_Objects = new List<UnityEngine.Object>();
        private readonly List<string> m_TempFiles = new List<string>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            return UniTask.ToCoroutine(async () =>
            {
                foreach (var gameObject in m_GameObjects)
                {
                    if (gameObject != null)
                    {
                        UnityEngine.Object.Destroy(gameObject);
                    }
                }

                m_GameObjects.Clear();
                await UniTask.Yield();

                try
                {
                    await App.Shutdown();
                }
                catch (GameException)
                {
                }

                StartupLoadingTestFixture.Restore();

                foreach (var value in m_Objects)
                {
                    if (value != null)
                    {
                        UnityEngine.Object.DestroyImmediate(value);
                    }
                }

                m_Objects.Clear();

                foreach (var path in m_TempFiles)
                {
                    if (System.IO.File.Exists(path))
                    {
                        System.IO.File.Delete(path);
                    }
                }

                m_TempFiles.Clear();
                RecordingProcedure.Reset();
                WaitingProcedure.Reset();
                ResourceReadyProcedure.Reset();
            });
        }

        [UnityTest]
        public IEnumerator StartupAsync_WhenTargetProcedureIsValid_ChangesCurrentProcedureAndPassesUserData()
        {
            return UniTask.ToCoroutine(async () =>
            {
                StartupLoadingTestFixture.Prepare();
                var userData = CreateObject<StartupUserData>();
                var startup = CreateStartup(typeof(RecordingProcedure), userData);

                await startup.StartupAsync();

                Assert.IsTrue(startup.HasStarted);
                Assert.IsFalse(startup.IsRunning);
                Assert.IsNull(startup.LastError);
                Assert.AreEqual(typeof(RecordingProcedure), startup.TargetProcedureType);
                Assert.IsInstanceOf<RecordingProcedure>(App.Procedure.Current);
                Assert.AreSame(userData, RecordingProcedure.LastUserData);
                Assert.AreEqual(1, RecordingProcedure.EnterCount);
                Assert.IsTrue(App.UI.IsOpen<LoadingWindow>());
            });
        }

        [UnityTest]
        public IEnumerator StartupAsync_WhenCalledWhileRunning_ReusesInFlightStartup()
        {
            return UniTask.ToCoroutine(async () =>
            {
                StartupLoadingTestFixture.Prepare();
                WaitingProcedure.Reset();
                var startup = CreateStartup(typeof(WaitingProcedure));

                var first = startup.StartupAsync();
                await UniTask.Yield();
                var second = startup.StartupAsync();

                Assert.IsTrue(startup.IsRunning);
                Assert.AreEqual(1, WaitingProcedure.EnterCount);

                WaitingProcedure.CompleteEnter();
                await UniTask.WhenAll(first, second);

                Assert.IsTrue(startup.HasStarted);
                Assert.AreEqual(1, WaitingProcedure.EnterCount);
                Assert.IsInstanceOf<WaitingProcedure>(App.Procedure.Current);
                Assert.IsTrue(App.UI.IsOpen<LoadingWindow>());
            });
        }

        [UnityTest]
        public IEnumerator StartupAsync_WhenTargetProcedureTypeIsInvalid_ThrowsAndRecordsLastError()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var startup = CreateStartup(typeof(string));

                var exception = await ThrowsAsync<GameException>(async () =>
                {
                    await startup.StartupAsync();
                });

                StringAssert.Contains("must inherit ProcedureBase", exception.Message);
                Assert.AreSame(exception, startup.LastError);
                Assert.IsFalse(startup.HasStarted);
                Assert.IsNull(App.Procedure.Current);
            });
        }

        [UnityTest]
        public IEnumerator StartupAsync_WhenTargetProcedureTypeNameIsEmpty_ThrowsAndRecordsLastError()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var startup = CreateStartup(string.Empty);

                var exception = await ThrowsAsync<GameException>(async () =>
                {
                    await startup.StartupAsync();
                });

                StringAssert.Contains("is not configured", exception.Message);
                Assert.AreSame(exception, startup.LastError);
                Assert.IsFalse(startup.HasStarted);
            });
        }

        [UnityTest]
        public IEnumerator StartupAsync_WhenResourceInitializationEnabled_ReadiesResourceBeforeTargetEnter()
        {
            return UniTask.ToCoroutine(async () =>
            {
                StartupLoadingTestFixture.Prepare();
                ResourceReadyProcedure.Reset();
                var settings = CreateResourceSettings(CreateManifestPath("framework-startup-resource"));
                var startup = CreateStartup(
                    typeof(ResourceReadyProcedure),
                    null,
                    CreateOptions(initializeResource: true, resourceSettings: settings));

                await startup.StartupAsync();

                Assert.IsTrue(App.Resource.IsInitialized);
                Assert.IsInstanceOf<ResourceReadyProcedure>(App.Procedure.Current);
                Assert.IsTrue(ResourceReadyProcedure.ResourceInitializedOnEnter);
                Assert.IsTrue(App.UI.IsOpen<LoadingWindow>());
            });
        }

        [UnityTest]
        public IEnumerator StartupAsync_WhenResourceInitializationDisabled_KeepsResourceStartupReadyOnly()
        {
            return UniTask.ToCoroutine(async () =>
            {
                StartupLoadingTestFixture.Prepare();
                var startup = CreateStartup(typeof(RecordingProcedure));

                await startup.StartupAsync();

                Assert.IsTrue(App.TryGetRegistered<ResourceModule>(out var resource));
                Assert.IsTrue(resource.IsLocalInitialized);
                Assert.IsFalse(resource.IsInitialized);
                Assert.IsTrue(App.UI.IsOpen<LoadingWindow>());
            });
        }

        [UnityTest]
        public IEnumerator StartupAsync_WhenConfigAndDataResolveEnabled_RegistersConfigAndDataShells()
        {
            return UniTask.ToCoroutine(async () =>
            {
                StartupLoadingTestFixture.Prepare();
                var startup = CreateStartup(
                    typeof(RecordingProcedure),
                    null,
                    CreateOptions(resolveConfig: true, resolveData: true));

                await startup.StartupAsync();

                Assert.IsTrue(App.TryGetRegistered<ConfigModule>(out _));
                Assert.IsTrue(App.TryGetRegistered<DataModule>(out _));
                Assert.IsFalse(App.Resource.IsInitialized);
            });
        }

        [UnityTest]
        public IEnumerator StartupAsync_WhenSoundResolveEnabled_RegistersSoundShell()
        {
            return UniTask.ToCoroutine(async () =>
            {
                StartupLoadingTestFixture.Prepare();
                var startup = CreateStartup(
                    typeof(RecordingProcedure),
                    null,
                    CreateOptions(resolveSound: true));

                await startup.StartupAsync();

                Assert.IsTrue(App.TryGetRegistered<SoundModule>(out _));
                Assert.IsNotNull(GameObject.Find(SoundModule.RootName));
            });
        }

        [UnityTest]
        public IEnumerator OnDestroy_WhenShutdownEnabled_ShutsDownApp()
        {
            return UniTask.ToCoroutine(async () =>
            {
                StartupLoadingTestFixture.Prepare();
                var startup = CreateStartup(typeof(RecordingProcedure), shutdownOnDestroy: true);

                await startup.StartupAsync();
                Assert.IsTrue(App.TryGetRegistered<ProcedureModule>(out _));

                UnityEngine.Object.Destroy(startup.gameObject);
                await UniTask.Yield();

                Assert.IsFalse(App.TryGetRegistered<ProcedureModule>(out _));
            });
        }

        [UnityTest]
        public IEnumerator StartupAsync_WhenStartupLoadingIsMissing_FailsAndRecordsLastError()
        {
            return UniTask.ToCoroutine(async () =>
            {
                StartupLoadingTestFixture.Prepare(includeLoadingAssetInManifest: false);
                var startup = CreateStartup(typeof(RecordingProcedure));

                var exception = await ThrowsAsync<GameException>(async () =>
                {
                    await startup.StartupAsync();
                });

                StringAssert.Contains("Failed to load UI prefab", exception.Message);
                Assert.AreSame(exception, startup.LastError);
                Assert.IsFalse(startup.HasStarted);
                Assert.IsFalse(App.UI.IsOpen<LoadingWindow>());
            });
        }

        [UnityTest]
        public IEnumerator StartupAsync_WhenDefaultPackageIsMissing_DoesNotFailResourceInitialization()
        {
            return UniTask.ToCoroutine(async () =>
            {
                StartupLoadingTestFixture.Prepare();
                var settings = CreateResourceSettings(ResourceSettings.MANIFEST_NAME);
                settings.DefaultPackages = new[] { "Missing" };
                var startup = CreateStartup(
                    typeof(RecordingProcedure),
                    null,
                    CreateOptions(initializeResource: true, resourceSettings: settings));

                await startup.StartupAsync();

                Assert.IsNull(startup.LastError);
                Assert.IsTrue(startup.HasStarted);
                Assert.IsTrue(App.UI.IsOpen<LoadingWindow>());
                Assert.IsTrue(App.Resource.IsLocalInitialized);
                Assert.IsTrue(App.Resource.IsInitialized);
            });
        }

        private FrameworkStartup CreateStartup(
            Type targetProcedureType,
            UnityEngine.Object userData = null,
            FrameworkStartupModuleOptions options = null,
            bool shutdownOnDestroy = false)
        {
            return CreateStartup(targetProcedureType?.AssemblyQualifiedName, userData, options, shutdownOnDestroy);
        }

        private FrameworkStartup CreateStartup(
            string targetProcedureTypeName,
            UnityEngine.Object userData = null,
            FrameworkStartupModuleOptions options = null,
            bool shutdownOnDestroy = false)
        {
            var gameObject = new GameObject("FrameworkStartupTests");
            m_GameObjects.Add(gameObject);

            var startup = gameObject.AddComponent<FrameworkStartup>();
            startup.enabled = false;
            SetField(startup, "m_TargetProcedureTypeName", targetProcedureTypeName);
            SetField(startup, "m_TargetUserData", userData);
            SetField(startup, "m_Modules", options ?? CreateOptions());
            SetField(startup, "m_ShutdownAppOnDestroy", shutdownOnDestroy);
            return startup;
        }

        private FrameworkStartupModuleOptions CreateOptions(
            bool initializeResource = false,
            ResourceSettings resourceSettings = null,
            bool resolveConfig = false,
            bool resolveData = false,
            bool resolveSound = false)
        {
            var options = new FrameworkStartupModuleOptions();
            SetField(options, "m_InitializeResource", initializeResource);
            if (resourceSettings != null)
            {
                SetField(options, "m_ResourceSettings", resourceSettings);
            }

            SetField(options, "m_ResolveConfigModule", resolveConfig);
            SetField(options, "m_ResolveDataModule", resolveData);
            SetField(options, "m_ResolveSoundModule", resolveSound);
            return options;
        }

        private T CreateObject<T>() where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>();
            m_Objects.Add(value);
            return value;
        }

        private ResourceSettings CreateResourceSettings(string manifestPath)
        {
            var settings = new ResourceSettings();
            settings.Mode = ResourceMode.Offline;
            settings.ManifestName = manifestPath;
            settings.DefaultPackages = Array.Empty<string>();
            return settings;
        }

        private string CreateManifestPath(string version)
        {
            var manifest = new ManifestInfo
            {
                Version = version,
                BuildTime = 1,
                Packages = new List<PackageInfo>(),
            };

            var path = Path.GetTempFileName();
            System.IO.File.WriteAllText(path, JsonConvert.SerializeObject(manifest));
            m_TempFiles.Add(path);
            return path;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }

            field.SetValue(target, value);
        }

        private static async UniTask<TException> ThrowsAsync<TException>(Func<UniTask> action)
            where TException : Exception
        {
            try
            {
                await action();
            }
            catch (TException exception)
            {
                return exception;
            }

            Assert.Fail($"Expected exception of type {typeof(TException).FullName}.");
            return null;
        }

        private sealed class RecordingProcedure : ProcedureBase
        {
            public static int EnterCount { get; private set; }

            public static object LastUserData { get; private set; }

            public static void Reset()
            {
                EnterCount = 0;
                LastUserData = null;
            }

            public override UniTask OnEnterAsync(ProcedureBase previous, object userData)
            {
                EnterCount++;
                LastUserData = userData;
                return UniTask.CompletedTask;
            }
        }

        private sealed class WaitingProcedure : ProcedureBase
        {
            private static UniTaskCompletionSource s_EnterCompletion;

            public static int EnterCount { get; private set; }

            public static void Reset()
            {
                EnterCount = 0;
                s_EnterCompletion = new UniTaskCompletionSource();
            }

            public static void CompleteEnter()
            {
                s_EnterCompletion.TrySetResult();
            }

            public override UniTask OnEnterAsync(ProcedureBase previous, object userData)
            {
                EnterCount++;
                return s_EnterCompletion.Task;
            }
        }

        private sealed class ResourceReadyProcedure : ProcedureBase
        {
            public static bool ResourceInitializedOnEnter { get; private set; }

            public static void Reset()
            {
                ResourceInitializedOnEnter = false;
            }

            public override UniTask OnEnterAsync(ProcedureBase previous, object userData)
            {
                ResourceInitializedOnEnter = App.Resource.IsInitialized;
                return UniTask.CompletedTask;
            }
        }

        private sealed class StartupUserData : ScriptableObject
        {
        }
    }

    internal static class StartupLoadingTestFixture
    {
        private const string LoadingAssetPath = "Assets/GameDeveloperKit/Resources/Loading.prefab";
        private static readonly List<string> s_CreatedAssetPaths = new List<string>();
        private static bool s_ManifestCaptured;
        private static bool s_HadManifest;
        private static string s_OriginalManifest;

        public static void Prepare(bool includeLoadingAssetInManifest = true)
        {
            EnsureLoadingPrefabAsset();
            WriteStartupManifest(includeLoadingAssetInManifest);
        }

        public static void Restore()
        {
            RestoreStartupManifest();
            DeleteCreatedAssets();
        }

        private static void WriteStartupManifest(bool includeLoadingAssetInManifest)
        {
            var manifestPath = Path.Combine(Application.streamingAssetsPath, ResourceSettings.MANIFEST_NAME);
            var directory = Path.GetDirectoryName(manifestPath);
            if (string.IsNullOrWhiteSpace(directory) is false)
            {
                Directory.CreateDirectory(directory);
            }

            if (s_ManifestCaptured is false)
            {
                s_HadManifest = System.IO.File.Exists(manifestPath);
                s_OriginalManifest = s_HadManifest ? System.IO.File.ReadAllText(manifestPath) : null;
                s_ManifestCaptured = true;
            }

            var assets = includeLoadingAssetInManifest
                ? new List<AssetInfo>
                {
                    new AssetInfo
                    {
                        Location = "Resources/Loading",
                        TypeName = nameof(GameObject)
                    }
                }
                : new List<AssetInfo>();
            var manifest = new ManifestInfo
            {
                Version = "startup-loading-test",
                BuildTime = 1,
                Packages = new List<PackageInfo>
                {
                    new PackageInfo
                    {
                        Name = BuiltinMode.BUILTIN_PACKAGE_NAME,
                        Bundles = new List<BundleInfo>
                        {
                            new BundleInfo
                            {
                                Name = "Resources",
                                ProviderId = ResourceProviderIds.Resources,
                                Assets = assets
                            }
                        }
                    }
                }
            };

            System.IO.File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));
        }

        private static void RestoreStartupManifest()
        {
            if (s_ManifestCaptured is false)
            {
                return;
            }

            var manifestPath = Path.Combine(Application.streamingAssetsPath, ResourceSettings.MANIFEST_NAME);
            if (s_HadManifest)
            {
                System.IO.File.WriteAllText(manifestPath, s_OriginalManifest);
            }
            else if (System.IO.File.Exists(manifestPath))
            {
                System.IO.File.Delete(manifestPath);
            }

            s_ManifestCaptured = false;
            s_HadManifest = false;
            s_OriginalManifest = null;
        }

        private static void EnsureLoadingPrefabAsset()
        {
#if UNITY_EDITOR
            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(LoadingAssetPath);
            if (existing != null)
            {
                return;
            }

            var root = new GameObject("Loading", typeof(RectTransform));
            var document = root.AddComponent<UIDocument>();
            var sliderObject = new GameObject("b_Slider", typeof(RectTransform));
            sliderObject.transform.SetParent(root.transform, false);
            var slider = sliderObject.AddComponent<Slider>();
            var infoObject = new GameObject("b_info", typeof(RectTransform));
            infoObject.transform.SetParent(root.transform, false);
            var text = infoObject.AddComponent<TextMeshProUGUI>();
            SetField(
                document,
                "mappings",
                new[]
                {
                    new UIBindMapping
                    {
                        Name = "b_Slider",
                        Target = sliderObject,
                        Components = new Component[] { slider }
                    },
                    new UIBindMapping
                    {
                        Name = "b_info",
                        Target = infoObject,
                        Components = new Component[] { text }
                    }
                });

            try
            {
                UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, LoadingAssetPath);
                s_CreatedAssetPaths.Add(LoadingAssetPath);
                UnityEditor.AssetDatabase.ImportAsset(LoadingAssetPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
#else
            Assert.Ignore("Startup loading fixture requires UnityEditor AssetDatabase.");
#endif
        }

        private static void DeleteCreatedAssets()
        {
#if UNITY_EDITOR
            foreach (var assetPath in s_CreatedAssetPaths)
            {
                UnityEditor.AssetDatabase.DeleteAsset(assetPath);
            }

            s_CreatedAssetPaths.Clear();
            UnityEditor.AssetDatabase.Refresh();
#endif
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }

            field.SetValue(target, value);
        }
    }
}
