using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityEditorCameraSkill : IUnityBridgeSkill
    {
        public string Name => "unity-editor-camera";

        public string Description =>
            "Control the Scene View camera: get position, move, frame objects, set field of view.";

        public string Trigger =>
            "Use when the user wants to navigate the scene view, look at specific objects, frame a GameObject, or adjust the editor camera.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("GET", "/camera", "Returns current Scene View camera position, rotation, FOV, and orthographic mode."),
            new UnityBridgeSkillEndpoint("POST", "/camera/move", "Sets the Scene View camera position and/or rotation.", "{\"position\":[10,5,0],\"rotation\":[30,45,0]}"),
            new UnityBridgeSkillEndpoint("POST", "/camera/frame", "Frames the selected object or a named GameObject in the Scene View.", "{\"name\":\"Main Camera\"}"),
            new UnityBridgeSkillEndpoint("POST", "/camera/lookat", "Points the camera to look at a specific position.", "{\"target\":[0,0,0]}"),
            new UnityBridgeSkillEndpoint("POST", "/camera/ortho", "Toggles orthographic mode.", "{\"ortho\":true,\"size\":5}")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js camera`",
            "`node scripts/unity-bridge.js camera-move --position 10,5,0 --rotation 30,45,0`",
            "`node scripts/unity-bridge.js camera-lookat --target 0,0,0`",
            "`node scripts/unity-bridge.js camera-frame --name Player`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Requires an active Scene View window.",
            "Camera movement is relative to the current pivot mode.",
            "Framing uses the Scene View's built-in FrameSelected functionality."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            return (request.Method == "GET" && path == "camera")
                || (request.Method == "POST" && path == "camera/move")
                || (request.Method == "POST" && path == "camera/frame")
                || (request.Method == "POST" && path == "camera/lookat")
                || (request.Method == "POST" && path == "camera/ortho");
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            var sceneView = GetSceneView();
            if (sceneView == null)
                return UnityBridgeSkillResponse.Error(400, "No active Scene View. Open a Scene View window first.");

            return path switch
            {
                "camera" => GetCameraInfo(sceneView),
                "camera/move" => MoveCamera(sceneView, request),
                "camera/frame" => FrameObject(sceneView, request),
                "camera/lookat" => LookAt(sceneView, request),
                "camera/ortho" => SetOrtho(sceneView, request),
                _ => UnityBridgeSkillResponse.Error(404, $"Unknown endpoint: /{path}")
            };
        }

        private static SceneView GetSceneView()
        {
            return SceneView.lastActiveSceneView ?? SceneView.sceneViews.Count > 0
                ? (SceneView)SceneView.sceneViews[0] : null;
        }

        private static UnityBridgeSkillResponse GetCameraInfo(SceneView sv)
        {
            var pivot = sv.pivot;
            var rot = sv.rotation.eulerAngles;
            var size = sv.size;
            return UnityBridgeSkillResponse.Success(
                $"{{\"position\":[{pivot.x:F2},{pivot.y:F2},{pivot.z:F2}],\"rotation\":[{rot.x:F2},{rot.y:F2},{rot.z:F2}],\"size\":{size:F2},\"ortho\":{(sv.orthographic ? "true" : "false")}}}");
        }

        private static UnityBridgeSkillResponse MoveCamera(SceneView sv, UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            if (body == null)
                return UnityBridgeSkillResponse.Error(400, "Invalid request body");

            if (ParseVector3(body, "position", out var pos))
                sv.pivot = pos;

            if (ParseVector3(body, "rotation", out var rot))
                sv.rotation = Quaternion.Euler(rot);

            if (body.TryGetValue("size", out var sizeStr) && float.TryParse(sizeStr, out var size))
                sv.size = Mathf.Clamp(size, 0.1f, 100000f);

            sv.Repaint();
            return GetCameraInfo(sv);
        }

        private static UnityBridgeSkillResponse FrameObject(SceneView sv, UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            var name = body?.GetValueOrDefault("name", "");

            if (!string.IsNullOrWhiteSpace(name))
            {
                var go = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                    .FirstOrDefault(g => string.Equals(g.name, name, StringComparison.OrdinalIgnoreCase));
                if (go != null)
                    Selection.activeGameObject = go;
            }

            if (Selection.activeGameObject == null && Selection.activeObject == null)
                return UnityBridgeSkillResponse.Error(400, "No object selected and no 'name' provided. Select an object or provide a name.");

            sv.FrameSelected();
            return GetCameraInfo(sv);
        }

        private static UnityBridgeSkillResponse LookAt(SceneView sv, UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            if (body == null || !ParseVector3(body, "target", out var target))
                return UnityBridgeSkillResponse.Error(400, "Missing or invalid 'target' field (comma-separated x,y,z)");

            sv.LookAt(target, sv.rotation);
            sv.Repaint();
            return GetCameraInfo(sv);
        }

        private static UnityBridgeSkillResponse SetOrtho(SceneView sv, UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            var orthoStr = body?.GetValueOrDefault("ortho", "toggle");

            if (orthoStr.Equals("toggle", StringComparison.OrdinalIgnoreCase))
            {
                sv.orthographic = !sv.orthographic;
            }
            else
            {
                sv.orthographic = !orthoStr.Equals("false", StringComparison.OrdinalIgnoreCase) && orthoStr != "0";
            }

            if (body != null && body.TryGetValue("size", out var sizeStr) && float.TryParse(sizeStr, out var size))
                sv.size = Mathf.Clamp(size, 0.1f, 100000f);

            sv.Repaint();
            return GetCameraInfo(sv);
        }

        private static bool ParseVector3(Dictionary<string, string> body, string key, out Vector3 result)
        {
            result = Vector3.zero;
            if (body == null || !body.TryGetValue(key, out var val)) return false;
            var parts = val.Split(',');
            if (parts.Length != 3) return false;
            if (!float.TryParse(parts[0], out var x)) return false;
            if (!float.TryParse(parts[1], out var y)) return false;
            if (!float.TryParse(parts[2], out var z)) return false;
            result = new Vector3(x, y, z);
            return true;
        }

        private static Dictionary<string, string> ParseBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                var obj = JsonUtility.FromJson<CameraBody>(body);
                if (obj == null) return null;
                var d = new Dictionary<string, string>();
                if (obj.position != null) d["position"] = obj.position;
                if (obj.rotation != null) d["rotation"] = obj.rotation;
                if (obj.target != null) d["target"] = obj.target;
                if (obj.name != null) d["name"] = obj.name;
                if (obj.size != null) d["size"] = obj.size;
                if (obj.ortho != null) d["ortho"] = obj.ortho;
                return d;
            }
            catch { return null; }
        }

        private static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        [Serializable]
        private class CameraBody { public string position; public string rotation; public string target; public string name; public string size; public string ortho; }
    }
}
