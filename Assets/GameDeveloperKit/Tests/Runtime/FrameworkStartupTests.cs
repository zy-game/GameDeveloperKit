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
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
            });
        }

        [UnityTest]
        public IEnumerator StartupAsync_WhenCalledWhileRunning_ReusesInFlightStartup()
        {
            return UniTask.ToCoroutine(async () =>
            {
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
            });
        }

        [UnityTest]
        public IEnumerator StartupAsync_WhenResourceInitializationDisabled_DoesNotInitializeResource()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var startup = CreateStartup(typeof(RecordingProcedure));

                await startup.StartupAsync();

                Assert.IsFalse(App.TryGetRegistered<ResourceModule>(out _));
                Assert.IsFalse(App.Resource.IsInitialized);
            });
        }

        [UnityTest]
        public IEnumerator StartupAsync_WhenConfigAndDataResolveEnabled_RegistersConfigAndDataShells()
        {
            return UniTask.ToCoroutine(async () =>
            {
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
                var startup = CreateStartup(typeof(RecordingProcedure), shutdownOnDestroy: true);

                await startup.StartupAsync();
                Assert.IsTrue(App.TryGetRegistered<ProcedureModule>(out _));

                UnityEngine.Object.Destroy(startup.gameObject);
                await UniTask.Yield();

                Assert.IsFalse(App.TryGetRegistered<ProcedureModule>(out _));
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
}
