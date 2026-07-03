using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using GameDeveloperKit.UnityBridge;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using IOFile = System.IO.File;

namespace GameDeveloperKit.Tests
{
    public sealed class UnityBridgeContractTests
    {
        private readonly List<string> m_CreatedFiles = new List<string>();

        [TearDown]
        public void TearDown()
        {
            UnityBridgeTaskQueue.Stop();

            foreach (var path in m_CreatedFiles)
            {
                DeleteFileSafe(path);
                DeleteFileSafe(path + ".tmp");
            }

            m_CreatedFiles.Clear();
        }

        [Test]
        public void ActionResult_Success_PreservesJson()
        {
            var response = UnityBridgeActionResult.SuccessResult("{\"ok\":true}");

            Assert.IsTrue(response.Success);
            Assert.AreEqual("{\"ok\":true}", response.DataJson);
            Assert.IsNull(response.Error);
        }

        [Test]
        public void ActionResult_Failure_PreservesPlainError()
        {
            const string message = "bad \"name\" \\ path\nline\rnext\tend";

            var response = UnityBridgeActionResult.Failure(message);

            Assert.IsFalse(response.Success);
            Assert.AreEqual(message, response.Error);
            Assert.IsNull(response.DataJson);
        }

        [Test]
        public void ActionRegistry_HasUniqueSkillsAndStatusHandler()
        {
            var handlers = UnityBridgeActionRegistry.Handlers;

            Assert.IsNotNull(handlers);
            Assert.IsNotEmpty(handlers);
            Assert.IsFalse(handlers.Any(handler => handler == null));

            var duplicateSkills = handlers
                .GroupBy(handler => handler.Skill)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            CollectionAssert.IsEmpty(duplicateSkills);
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-status"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-console"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-project"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-selection"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-scene"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-gameobject"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-asset-creation"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-prefab"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-material"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-compile"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-menu-command"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-reflection"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-editor-settings"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-browse"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-asset-importer"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-asset-preview"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-screenshot"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-build"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-editor-camera"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-audio"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-property"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-ui"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-lighting"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-navmesh"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-animation"));
            Assert.IsNotNull(handlers.FirstOrDefault(handler => handler.Skill == "unity-animator"));
        }

        [Test]
        public void StatusAction_WhenPing_ReturnsAlive()
        {
            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-status",
                "ping",
                "{}"));
            var json = JObject.Parse(result.DataJson);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(json.Value<bool>("alive"));
            Assert.IsFalse(string.IsNullOrWhiteSpace(json.Value<string>("timestamp")));
        }

        [Test]
        public void StatusAction_WhenStatus_ReturnsEditorStateFields()
        {
            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-status",
                "status",
                "{}"));
            var json = JObject.Parse(result.DataJson);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(json["isCompiling"]);
            Assert.IsNotNull(json["isPlaying"]);
            Assert.IsNotNull(json["unityVersion"]);
            Assert.IsNotNull(json["bridgeRunning"]);
        }

        [Test]
        public void TaskQueue_WhenPingTaskProcessed_WritesSuccessResultAndDeletesPending()
        {
            var result = ProcessTask(
                "unity-bridge-contract-ping-" + Guid.NewGuid().ToString("N"),
                "unity-status",
                "ping");
            var data = JObject.Parse(result.Value<string>("data"));

            Assert.IsTrue(result.Value<bool>("success"));
            Assert.AreEqual("unity-status", result.Value<string>("skill"));
            Assert.AreEqual("ping", result.Value<string>("action"));
            Assert.IsTrue(data.Value<bool>("alive"));
            Assert.IsTrue(result["error"] == null || result["error"].Type == JTokenType.Null);
        }

        [Test]
        public void ProjectAction_WhenInfo_ReturnsProjectFields()
        {
            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-project",
                "info",
                "{}"));
            var json = JObject.Parse(result.DataJson);

            Assert.IsTrue(result.Success);
            Assert.IsFalse(string.IsNullOrWhiteSpace(json.Value<string>("dataPath")));
            Assert.IsFalse(string.IsNullOrWhiteSpace(json.Value<string>("unityVersion")));
        }

        [Test]
        public void SelectionAction_WhenGet_ReturnsObjectArray()
        {
            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-selection",
                "get",
                "{}"));
            var json = JObject.Parse(result.DataJson);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(json["objects"]);
            Assert.IsNotNull(json["count"]);
        }

        [Test]
        public void ConsoleAction_WhenRead_ReturnsLogArray()
        {
            UnityBridgeConsoleCapture.StartCapture();
            Debug.Log("unity bridge contract console read");

            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-console",
                "read",
                "{\"count\":10}"));
            var json = JObject.Parse(result.DataJson);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(json["logs"]);
            Assert.IsNotNull(json["count"]);
            Assert.IsNotNull(json["capturing"]);
        }

        [Test]
        public void SceneAction_WhenInfo_ReturnsSceneFields()
        {
            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-scene",
                "info",
                "{}"));
            var json = JObject.Parse(result.DataJson);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(json["name"]);
            Assert.IsNotNull(json["isLoaded"]);
            Assert.IsNotNull(json["rootCount"]);
        }

        [Test]
        public void GameObjectAction_CanCreateFindAndDestroyObject()
        {
            var name = "UnityBridgeContractObject_" + Guid.NewGuid().ToString("N");

            var createResult = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-gameobject",
                "create",
                "{\"name\":\"" + name + "\"}"));
            Assert.IsTrue(createResult.Success);

            var findResult = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-gameobject",
                "find",
                "{\"name\":\"" + name + "\"}"));
            var findJson = JObject.Parse(findResult.DataJson);
            Assert.IsTrue(findResult.Success);
            Assert.GreaterOrEqual(findJson.Value<int>("count"), 1);

            var destroyResult = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-gameobject",
                "destroy",
                "{\"name\":\"" + name + "\"}"));
            Assert.IsTrue(destroyResult.Success);
        }

        [Test]
        public void MaterialAction_WhenShaders_ReturnsShaderArray()
        {
            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-material",
                "shaders",
                "{\"count\":10}"));
            var json = JObject.Parse(result.DataJson);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(json["shaders"]);
            Assert.IsNotNull(json["count"]);
        }

        [Test]
        public void AssetCreationAction_CanCreateFolder()
        {
            var relativePath = "Assets/UnityBridgeContractFolder_" + Guid.NewGuid().ToString("N");
            var absolutePath = Path.Combine(ProjectRoot, relativePath);
            m_CreatedFiles.Add(absolutePath);
            m_CreatedFiles.Add(absolutePath + ".meta");

            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-asset-creation",
                "folder",
                "{\"path\":\"" + relativePath + "\"}"));
            var json = JObject.Parse(result.DataJson);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(relativePath, json.Value<string>("path"));
            Assert.IsTrue(Directory.Exists(absolutePath));
        }

        [Test]
        public void PrefabAction_WhenTargetMissing_ReturnsFailure()
        {
            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-prefab",
                "info",
                "{}"));

            Assert.IsFalse(result.Success);
            StringAssert.Contains("GameObject not found", result.Error);
        }

        [Test]
        public void CompileAction_WhenStatus_ReturnsCompileFields()
        {
            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-compile",
                "status",
                "{}"));
            var json = JObject.Parse(result.DataJson);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(json["isCompiling"]);
            Assert.IsNotNull(json["errorCount"]);
            Assert.IsNotNull(json["warningCount"]);
        }

        [Test]
        public void ReflectionAction_CanReadStaticProperty()
        {
            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-reflection",
                "get-static-property",
                "{\"type\":\"UnityEngine.Application\",\"property\":\"unityVersion\"}"));
            var json = JObject.Parse(result.DataJson);

            Assert.IsTrue(result.Success);
            Assert.IsFalse(string.IsNullOrWhiteSpace(json.Value<string>("result")));
        }

        [Test]
        public void EditorSettingsAction_WhenPlayer_ReturnsPlayerSettings()
        {
            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-editor-settings",
                "player",
                "{}"));
            var json = JObject.Parse(result.DataJson);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(json["productName"]);
            Assert.IsNotNull(json["bundleVersion"]);
        }

        [Test]
        public void MenuCommandAction_WhenMissingPath_ReturnsFailure()
        {
            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-menu-command",
                "execute",
                "{}"));

            Assert.IsFalse(result.Success);
            StringAssert.Contains("menuPath", result.Error);
        }

        [Test]
        public void BrowseAction_WhenListAssets_ReturnsEntries()
        {
            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-browse",
                "list",
                "{\"path\":\"Assets\",\"depth\":1,\"skipMeta\":true}"));
            var json = JObject.Parse(result.DataJson);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(json["entries"]);
            Assert.IsNotNull(json["count"]);
        }

        [Test]
        public void AssetImporterAction_WhenInfo_ReturnsImporterType()
        {
            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-asset-importer",
                "info",
                "{\"path\":\"Assets/GameDeveloperKit/Tests/Editor/GameDeveloperKit.Editor.Tests.asmdef\"}"));
            var json = JObject.Parse(result.DataJson);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("Assets/GameDeveloperKit/Tests/Editor/GameDeveloperKit.Editor.Tests.asmdef", json.Value<string>("path"));
            Assert.IsFalse(string.IsNullOrWhiteSpace(json.Value<string>("importerType")));
        }

        [Test]
        public void AssetPreviewAction_WhenPathMissing_ReturnsFailure()
        {
            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-asset-preview",
                "info",
                "{}"));

            Assert.IsFalse(result.Success);
            StringAssert.Contains("path", result.Error);
        }

        [Test]
        public void ScreenshotAction_WhenNoLatest_ReturnsFailure()
        {
            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-screenshot",
                "latest",
                "{}"));

            Assert.IsFalse(result.Success);
            StringAssert.Contains("No screenshot", result.Error);
        }

        [Test]
        public void BuildAction_WhenInfo_ReturnsBuildTarget()
        {
            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-build",
                "info",
                "{}"));
            var json = JObject.Parse(result.DataJson);

            Assert.IsTrue(result.Success);
            Assert.IsFalse(string.IsNullOrWhiteSpace(json.Value<string>("platform")));
            Assert.IsNotNull(json["scenes"]);
        }

        [Test]
        public void PropertyAction_CanListTransformComponents()
        {
            var name = "UnityBridgePropertyObject_" + Guid.NewGuid().ToString("N");
            var gameObject = new GameObject(name);
            try
            {
                var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                    "unity-property",
                    "components",
                    "{\"target\":\"" + name + "\"}"));
                var json = JObject.Parse(result.DataJson);

                Assert.IsTrue(result.Success);
                Assert.GreaterOrEqual(json.Value<int>("count"), 1);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AudioAction_WhenPathMissing_ReturnsFailure()
        {
            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-audio",
                "info",
                "{}"));

            Assert.IsFalse(result.Success);
            StringAssert.Contains("AudioClip not found", result.Error);
        }

        [Test]
        public void EditorCameraAction_DoesNotThrowWhenInfoRequested()
        {
            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-editor-camera",
                "info",
                "{}"));

            Assert.IsNotNull(result);
            if (!result.Success)
            {
                StringAssert.Contains("Scene View", result.Error);
            }
        }

        [Test]
        public void UIAction_CanCreateCanvas()
        {
            var name = "UnityBridgeCanvas_" + Guid.NewGuid().ToString("N");

            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-ui",
                "create-canvas",
                "{\"name\":\"" + name + "\"}"));
            var json = JObject.Parse(result.DataJson);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(name, json.Value<string>("name"));
            Assert.AreEqual("Canvas", json.Value<string>("type"));

            var created = UnityEngine.Object.FindObjectsOfType<GameObject>()
                .FirstOrDefault(candidate => candidate.name == name);
            Assert.IsNotNull(created);
            UnityEngine.Object.DestroyImmediate(created);
        }

        [Test]
        public void LightingAction_WhenInfo_ReturnsLightingFields()
        {
            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-lighting",
                "info",
                "{}"));
            var json = JObject.Parse(result.DataJson);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(json["isBaking"]);
            Assert.IsNotNull(json["giWorkflowMode"]);
            Assert.IsNotNull(json["bakedLightmaps"]);
        }

        [Test]
        public void NavMeshAction_WhenInfo_ReturnsNavMeshFields()
        {
            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-navmesh",
                "info",
                "{}"));
            var json = JObject.Parse(result.DataJson);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(json["agentRadius"]);
            Assert.IsNotNull(json["agentHeight"]);
            Assert.IsNotNull(json["vertices"]);
        }

        [Test]
        public void AnimationAction_CanCreateClip()
        {
            var relativePath = "Assets/UnityBridgeContractClip_" + Guid.NewGuid().ToString("N") + ".anim";
            var absolutePath = Path.Combine(ProjectRoot, relativePath);
            m_CreatedFiles.Add(absolutePath);
            m_CreatedFiles.Add(absolutePath + ".meta");

            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-animation",
                "create-clip",
                "{\"path\":\"" + relativePath + "\",\"frameRate\":24}"));
            var json = JObject.Parse(result.DataJson);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(relativePath, json.Value<string>("path"));
            Assert.AreEqual(24f, json.Value<float>("frameRate"));
            Assert.IsTrue(IOFile.Exists(absolutePath));
        }

        [Test]
        public void AnimatorAction_CanCreateController()
        {
            var relativePath = "Assets/UnityBridgeContractController_" + Guid.NewGuid().ToString("N") + ".controller";
            var absolutePath = Path.Combine(ProjectRoot, relativePath);
            m_CreatedFiles.Add(absolutePath);
            m_CreatedFiles.Add(absolutePath + ".meta");

            var result = UnityBridgeActionRegistry.Execute(new UnityBridgeActionRequest(
                "unity-animator",
                "create",
                "{\"path\":\"" + relativePath + "\"}"));
            var json = JObject.Parse(result.DataJson);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(relativePath, json.Value<string>("path"));
            Assert.IsTrue(IOFile.Exists(absolutePath));
        }

        [Test]
        public void TaskQueue_WhenActionUnknown_WritesFailureResultAndDeletesPending()
        {
            var result = ProcessTask(
                "unity-bridge-contract-unknown-" + Guid.NewGuid().ToString("N"),
                "unity-status",
                "missing-action");

            Assert.IsFalse(result.Value<bool>("success"));
            Assert.AreEqual("unity-status", result.Value<string>("skill"));
            Assert.AreEqual("missing-action", result.Value<string>("action"));
            StringAssert.Contains("Unknown action", result.Value<string>("error"));
        }

        [Test]
        public void TaskQueue_WhenTaskIdMissing_DeletesPendingWithoutResult()
        {
            EnsureQueueDirs();
            var pendingPath = Path.Combine(PendingDir, "unity-bridge-contract-missing-id-" + Guid.NewGuid().ToString("N") + ".json");
            var resultPath = Path.Combine(ResultsDir, Path.GetFileNameWithoutExtension(pendingPath) + ".json");
            IOFile.WriteAllText(pendingPath, "{\"skill\":\"unity-status\",\"action\":\"ping\"}");
            m_CreatedFiles.Add(pendingPath);
            m_CreatedFiles.Add(resultPath);

            InvokeProcessTaskFile(pendingPath);

            Assert.IsFalse(IOFile.Exists(pendingPath));
            Assert.IsFalse(IOFile.Exists(resultPath));
        }

        [Test]
        public void Settings_LoadOrCreate_ReturnsMutableSettingsWithoutPersistingTestChanges()
        {
            var settings = UnityBridgeSettings.LoadOrCreate();
            Assert.IsNotNull(settings);

            var original = settings.AutoStart;
            try
            {
                settings.AutoStart = !original;
                Assert.AreEqual(!original, UnityBridgeSettings.LoadOrCreate().AutoStart);
            }
            finally
            {
                settings.AutoStart = original;
            }
        }

        [Test]
        public void SkillInstaller_BuildSkillMarkdown_UsesActionModelWithoutScriptOrEndpoint()
        {
            var handler = UnityBridgeActionRegistry.Handlers.First(candidate => candidate.Skill == "unity-status");

            var markdown = SkillInstaller.BuildSkillMarkdown(handler);

            StringAssert.Contains("name: unity-status", markdown);
            StringAssert.Contains("Uses Unity Bridge file queue", markdown);
            StringAssert.Contains("unity-status.ping", markdown);
            StringAssert.DoesNotContain("scripts/unity-bridge.js", markdown);
            StringAssert.DoesNotContain("## Endpoints", markdown);
            StringAssert.DoesNotContain("GET /", markdown);
        }

        [Test]
        public void SkillInstaller_BuildSkillMarkdown_ForEveryHandlerUsesFileQueueContract()
        {
            foreach (var handler in UnityBridgeActionRegistry.Handlers)
            {
                var markdown = SkillInstaller.BuildSkillMarkdown(handler);

                StringAssert.Contains($"name: {handler.Skill}", markdown);
                StringAssert.Contains("Temp/UnityBridge/pending/{taskId}.json", markdown);
                StringAssert.Contains("Temp/UnityBridge/results/{taskId}.json", markdown);
                StringAssert.Contains("\"payload\":", markdown);
                StringAssert.Contains("\"data\":", markdown);
                StringAssert.Contains($"{handler.Skill}.", markdown);
                StringAssert.DoesNotContain("scripts/unity-bridge.js", markdown);
                StringAssert.DoesNotContain("## Endpoints", markdown);
                StringAssert.DoesNotContain("GET /", markdown);
                StringAssert.DoesNotContain("POST /", markdown);
                StringAssert.DoesNotContain("endpoint", markdown);
            }
        }

        [Test]
        public void HostProject_ConsumesUnityBridgeFromPackageAsmdef()
        {
            var manifestPath = Path.Combine(ProjectRoot, "Packages", "manifest.json");
            var testsAsmdefPath = Path.Combine(
                ProjectRoot,
                "Assets",
                "GameDeveloperKit",
                "Tests",
                "Editor",
                "GameDeveloperKit.Editor.Tests.asmdef");
            var embeddedSourceDir = Path.Combine(
                ProjectRoot,
                "Assets",
                "GameDeveloperKit",
                "Editor",
                "UnityBridge");

            StringAssert.Contains(
                "\"com.gamedeveloperkit.unitybridge\": \"file:com.gamedeveloperkit.unitybridge\"",
                IOFile.ReadAllText(manifestPath));
            StringAssert.Contains(
                "\"GameDeveloperKit.UnityBridge.Editor\"",
                IOFile.ReadAllText(testsAsmdefPath));

            if (Directory.Exists(embeddedSourceDir))
            {
                var sourceFiles = Directory.GetFiles(embeddedSourceDir, "*.cs", SearchOption.AllDirectories);
                CollectionAssert.IsEmpty(sourceFiles);
            }
        }

        private JObject ProcessTask(string taskId, string skill, string action)
        {
            EnsureQueueDirs();

            var pendingPath = Path.Combine(PendingDir, taskId + ".json");
            var resultPath = Path.Combine(ResultsDir, taskId + ".json");
            var task = new
            {
                taskId,
                createdAt = DateTime.UtcNow.ToString("O"),
                skill,
                action,
                payload = "{}"
            };

            IOFile.WriteAllText(pendingPath, JsonConvert.SerializeObject(task));
            m_CreatedFiles.Add(pendingPath);
            m_CreatedFiles.Add(resultPath);

            InvokeProcessTaskFile(pendingPath);

            Assert.IsFalse(IOFile.Exists(pendingPath));
            Assert.IsTrue(IOFile.Exists(resultPath));
            return JObject.Parse(IOFile.ReadAllText(resultPath));
        }

        private static void InvokeProcessTaskFile(string path)
        {
            var method = typeof(UnityBridgeTaskQueue).GetMethod(
                "ProcessTaskFile",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            try
            {
                method.Invoke(null, new object[] { path });
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            }
        }

        private static void EnsureQueueDirs()
        {
            Directory.CreateDirectory(PendingDir);
            Directory.CreateDirectory(ResultsDir);
        }

        private static string PendingDir =>
            Path.Combine(ProjectRoot, "Temp", "UnityBridge", "pending");

        private static string ResultsDir =>
            Path.Combine(ProjectRoot, "Temp", "UnityBridge", "results");

        private static string ProjectRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static void DeleteFileSafe(string path)
        {
            try
            {
                if (IOFile.Exists(path))
                {
                    IOFile.Delete(path);
                }
                else if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
            }
        }
    }
}
