using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityUISkill : IUnityBridgeSkill
    {
        public string Name => "unity-ui";

        public string Description =>
            "Build UGUI elements: create Canvas, Image, Text (TMP), Button, set RectTransform, assign sprites and colors.";

        public string Trigger =>
            "Use when the user wants to create UI elements, build Canvas hierarchies, set anchor/pivot/position/size on RectTransforms, assign sprite images, or construct UGUI prefabs.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("POST", "/ui/canvas/create", "Creates a Canvas with default settings.",
                "{\"name\":\"MyCanvas\",\"parent\":\"Root\"}"),
            new UnityBridgeSkillEndpoint("POST", "/ui/element/create", "Creates a UI element (Image, Text, Button, RawImage, Panel).",
                "{\"name\":\"MyImage\",\"type\":\"Image\",\"parent\":\"MyCanvas\",\"sprite\":\"Assets/btn.png\"}"),
            new UnityBridgeSkillEndpoint("POST", "/ui/rect/set", "Sets RectTransform properties.",
                "{\"target\":\"MyImage\",\"position\":[100,200],\"size\":[200,50],\"anchor\":[0.5,0.5],\"pivot\":[0.5,0.5]}"),
            new UnityBridgeSkillEndpoint("POST", "/ui/layout", "Configures layout on a RectTransform.",
                "{\"target\":\"MyCanvas\",\"layoutType\":\"Horizontal\",\"spacing\":10,\"padding\":[10,10,10,10]}")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js ui-canvas --name MainUI`",
            "`node scripts/unity-bridge.js ui-element --name Btn --type Image --parent Canvas --sprite Assets/btn.png`",
            "`node scripts/unity-bridge.js ui-rect --target Btn --position 100,200 --size 200,50`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Element types: Image, Text, Button, RawImage, Panel (plain RectTransform).",
            "RectTransform values: position (anchoredPosition), size (sizeDelta), anchor (anchorMin=anchorMax), pivot. Accept 'x,y' format.",
            "If Text type, creates with TextMeshProUGUI if TMP is available, otherwise falls back to legacy Text."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var p = request.Path.Trim('/');
            return request.Method == "POST" && (p == "ui/canvas/create" || p == "ui/element/create" || p == "ui/rect/set" || p == "ui/layout");
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            return request.Path.Trim('/') switch
            {
                "ui/canvas/create" => CreateCanvas(request),
                "ui/element/create" => CreateElement(request),
                "ui/rect/set" => SetRect(request),
                "ui/layout" => SetLayout(request),
                _ => UnityBridgeSkillResponse.Error(404, "Unknown")
            };
        }

        private static UnityBridgeSkillResponse CreateCanvas(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var name = b?.GetValueOrDefault("name", "Canvas");
            var go = new GameObject(name, typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
            return UnityBridgeSkillResponse.Success($"{{\"created\":true,\"name\":\"{Esc(name)}\",\"type\":\"Canvas\"}}");
        }

        private static UnityBridgeSkillResponse CreateElement(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var name = b?.GetValueOrDefault("name", "Element");
            var type = b?.GetValueOrDefault("type", "Image");
            var parentName = b?.GetValueOrDefault("parent", "");
            var spritePath = b?.GetValueOrDefault("sprite", "");
            var text = b?.GetValueOrDefault("text", name);

            var parent = string.IsNullOrWhiteSpace(parentName) ? null : FindGo(parentName);
            var go = new GameObject(name, typeof(RectTransform));

            switch (type.ToLowerInvariant())
            {
                case "image":
                    go.AddComponent<UnityEngine.UI.Image>();
                    if (!string.IsNullOrWhiteSpace(spritePath))
                    {
                        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                        if (sp != null) go.GetComponent<UnityEngine.UI.Image>().sprite = sp;
                    }
                    break;
                case "rawimage":
                    go.AddComponent<UnityEngine.UI.RawImage>();
                    break;
                case "text":
                    var tmpType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } }).FirstOrDefault(t => t.Name == "TextMeshProUGUI");
                    if (tmpType != null) go.AddComponent(tmpType);
                    else go.AddComponent<UnityEngine.UI.Text>();
                    break;
                case "button":
                    go.AddComponent<UnityEngine.UI.Image>();
                    go.AddComponent<UnityEngine.UI.Button>();
                    if (!string.IsNullOrWhiteSpace(spritePath))
                    {
                        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                        if (sp != null) go.GetComponent<UnityEngine.UI.Image>().sprite = sp;
                    }
                    break;
                // "panel" stays as plain RectTransform
            }

            if (parent != null) go.transform.SetParent(parent.transform, false);
            Undo.RegisterCreatedObjectUndo(go, $"Create {type}");
            return UnityBridgeSkillResponse.Success($"{{\"created\":true,\"name\":\"{Esc(name)}\",\"type\":\"{Esc(type)}\"}}");
        }

        private static UnityBridgeSkillResponse SetRect(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var target = b?.GetValueOrDefault("target", "");
            var go = FindGo(target);
            if (go == null) return UnityBridgeSkillResponse.Error(404, $"GameObject not found: {target}");
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) return UnityBridgeSkillResponse.Error(400, "Target has no RectTransform");

            if (ParseVec2(b, "position", out var pos)) rt.anchoredPosition = pos;
            if (ParseVec2(b, "size", out var sz)) rt.sizeDelta = sz;
            if (ParseVec2(b, "anchor", out var anc)) { rt.anchorMin = anc; rt.anchorMax = anc; }
            if (ParseVec2(b, "pivot", out var pv)) rt.pivot = pv;
            if (ParseVec2(b, "anchorMin", out var amin)) rt.anchorMin = amin;
            if (ParseVec2(b, "anchorMax", out var amax)) rt.anchorMax = amax;

            return UnityBridgeSkillResponse.Success(
                $"{{\"set\":true,\"target\":\"{Esc(target)}\",\"position\":[{rt.anchoredPosition.x:F1},{rt.anchoredPosition.y:F1}],\"size\":[{rt.sizeDelta.x:F1},{rt.sizeDelta.y:F1}]}}");
        }

        private static UnityBridgeSkillResponse SetLayout(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var target = b?.GetValueOrDefault("target", "");
            var layoutType = b?.GetValueOrDefault("layoutType", "").ToLowerInvariant();
            var go = FindGo(target);
            if (go == null) return UnityBridgeSkillResponse.Error(404, $"GameObject not found: {target}");

            // Remove existing layout groups
            foreach (var c in go.GetComponents<Component>())
                if (c is UnityEngine.UI.LayoutGroup) Object.DestroyImmediate(c);

            switch (layoutType)
            {
                case "horizontal": go.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>(); break;
                case "vertical": go.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); break;
                case "grid": go.AddComponent<UnityEngine.UI.GridLayoutGroup>(); break;
                default: return UnityBridgeSkillResponse.Error(400, $"Unknown layoutType: {layoutType}. Use Horizontal/Vertical/Grid");
            }

            var lg = go.GetComponent<UnityEngine.UI.LayoutGroup>();
            if (lg != null)
            {
                if (b.TryGetValue("spacing", out var sp) && float.TryParse(sp, out var sf))
                {
                    if (lg is UnityEngine.UI.HorizontalOrVerticalLayoutGroup hvlg) hvlg.spacing = sf;
                    else if (lg is UnityEngine.UI.GridLayoutGroup glg) glg.spacing = new Vector2(sf, sf);
                }
                if (ParsePadding(b, "padding", out var pad))
                    lg.padding = new RectOffset((int)pad.x, (int)pad.y, (int)pad.z, (int)pad.w);
            }

            return UnityBridgeSkillResponse.Success($"{{\"set\":true,\"target\":\"{Esc(target)}\",\"layoutType\":\"{Esc(layoutType)}\"}}");
        }

        private static bool ParseVec2(Dictionary<string, string> b, string k, out Vector2 v)
        {
            v = Vector2.zero;
            if (b == null || !b.TryGetValue(k, out var val)) return false;
            var p = val.Trim('[', ']').Split(',');
            if (p.Length == 2 && float.TryParse(p[0], out var x) && float.TryParse(p[1], out var y)) { v = new Vector2(x, y); return true; }
            return false;
        }

        private static bool ParsePadding(Dictionary<string, string> b, string k, out Vector4 v)
        {
            v = Vector4.zero;
            if (b == null || !b.TryGetValue(k, out var val)) return false;
            var p = val.Trim('[', ']').Split(',');
            if (p.Length == 4 && float.TryParse(p[0], out var l) && float.TryParse(p[1], out var r) && float.TryParse(p[2], out var t) && float.TryParse(p[3], out var b2))
            { v = new Vector4(l, r, t, b2); return true; }
            return false;
        }

        private static GameObject FindGo(string n) => string.IsNullOrWhiteSpace(n) ? null : Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).FirstOrDefault(g => string.Equals(g.name, n, StringComparison.OrdinalIgnoreCase));
        private static Dictionary<string, string> ParseBody(string body) { if (string.IsNullOrWhiteSpace(body)) return null; try { var o = JsonUtility.FromJson<UIB>(body); var d = new Dictionary<string, string>(); if (o.name != null) d["name"] = o.name; if (o.type != null) d["type"] = o.type; if (o.parent != null) d["parent"] = o.parent; if (o.sprite != null) d["sprite"] = o.sprite; if (o.text != null) d["text"] = o.text; if (o.target != null) d["target"] = o.target; if (o.position != null) d["position"] = o.position; if (o.size != null) d["size"] = o.size; if (o.anchor != null) d["anchor"] = o.anchor; if (o.anchorMin != null) d["anchorMin"] = o.anchorMin; if (o.anchorMax != null) d["anchorMax"] = o.anchorMax; if (o.pivot != null) d["pivot"] = o.pivot; if (o.layoutType != null) d["layoutType"] = o.layoutType; if (o.spacing != null) d["spacing"] = o.spacing; if (o.padding != null) d["padding"] = o.padding; return d; } catch { return null; } }
        private static string Esc(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        [Serializable] private class UIB { public string name, type, parent, sprite, text, target, position, size, anchor, anchorMin, anchorMax, pivot, layoutType, spacing, padding; }
    }
}
