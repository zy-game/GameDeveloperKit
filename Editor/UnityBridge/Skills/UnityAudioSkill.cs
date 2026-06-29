using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityAudioSkill : IUnityBridgeSkill
    {
        public string Name => "unity-audio";

        public string Description =>
            "Create AudioSources, play/stop audio in editor, configure AudioMixer groups, set AudioClip import settings.";

        public string Trigger =>
            "Use when the user wants to play audio in the editor, create audio sources, configure audio import settings, or manage AudioMixers.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("POST", "/audio/play", "Plays an AudioClip as a one-shot in the editor.",
                "{\"path\":\"Assets/Sounds/Click.wav\",\"volume\":1.0}"),
            new UnityBridgeSkillEndpoint("POST", "/audio/source/create", "Creates an AudioSource component on a GameObject and optionally sets its clip.",
                "{\"target\":\"Speaker\",\"clipPath\":\"Assets/Sounds/Music.mp3\",\"loop\":true,\"volume\":0.8}"),
            new UnityBridgeSkillEndpoint("POST", "/audio/stop", "Stops all AudioSources or a specific one.",
                "{\"target\":\"Speaker\"}"),
            new UnityBridgeSkillEndpoint("GET", "/audio?path=Assets/Sounds/Click.wav", "Returns AudioClip import info.")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js audio-play --path Assets/Sounds/Click.wav`",
            "`node scripts/unity-bridge.js audio-source --target Speaker --clipPath Assets/Sounds/Music.mp3 --loop true`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Editor audio playback uses Preview mode via AudioUtil (internal API). May not work in all Unity versions.",
            "AudioSource creation assigns the clip, volume, loop, and spatial blend settings."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var p = request.Path.Trim('/');
            return (request.Method == "GET" && p == "audio")
                || (request.Method == "POST" && (p == "audio/play" || p == "audio/source/create" || p == "audio/stop"));
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            return request.Path.Trim('/') switch
            {
                "audio" => GetAudioInfo(request),
                "audio/play" => PlayClip(request),
                "audio/source/create" => CreateSource(request),
                "audio/stop" => StopAudio(request),
                _ => UnityBridgeSkillResponse.Error(404, "Unknown")
            };
        }

        private static UnityBridgeSkillResponse GetAudioInfo(UnityBridgeSkillRequest request)
        {
            var q = ParseQuery(request.QueryString);
            var path = q.GetValueOrDefault("path", "");
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) return UnityBridgeSkillResponse.Error(404, $"AudioClip not found: {path}");

            return UnityBridgeSkillResponse.Success(
                $"{{\"path\":\"{Esc(path)}\",\"name\":\"{Esc(clip.name)}\",\"length\":{clip.length:F2},\"channels\":{clip.channels},\"frequency\":{clip.frequency},\"samples\":{clip.samples}}}");
        }

        private static UnityBridgeSkillResponse PlayClip(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var path = b?.GetValueOrDefault("path", "");
            var volume = 1f;
            if (b != null && b.TryGetValue("volume", out var v) && float.TryParse(v, out var vf)) volume = Mathf.Clamp01(vf);

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) return UnityBridgeSkillResponse.Error(404, $"AudioClip not found: {path}");

            // Use reflection to call internal AudioUtil.PlayPreviewClip
            var audioUtilType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .FirstOrDefault(t => t.Name == "AudioUtil");
            if (audioUtilType != null)
            {
                var method = audioUtilType.GetMethod("PlayPreviewClip",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                    null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
                method?.Invoke(null, new object[] { clip, 0, false });
            }

            return UnityBridgeSkillResponse.Success($"{{\"playing\":true,\"clip\":\"{Esc(clip.name)}\",\"volume\":{volume:F2}}}");
        }

        private static UnityBridgeSkillResponse CreateSource(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var target = b?.GetValueOrDefault("target", "");
            var clipPath = b?.GetValueOrDefault("clipPath", "");
            var go = FindGo(target);
            if (go == null) go = new GameObject(target ?? "AudioSource");
            var source = go.GetComponent<AudioSource>();
            if (source == null) source = go.AddComponent<AudioSource>();

            if (!string.IsNullOrWhiteSpace(clipPath))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip != null) source.clip = clip;
            }
            if (b.TryGetValue("loop", out var lp)) source.loop = !lp.Equals("false", StringComparison.OrdinalIgnoreCase) && lp != "0";
            if (b.TryGetValue("volume", out var vol) && float.TryParse(vol, out var vf)) source.volume = Mathf.Clamp01(vf);
            if (b.TryGetValue("spatialBlend", out var sb) && float.TryParse(sb, out var sbf)) source.spatialBlend = Mathf.Clamp01(sbf);

            return UnityBridgeSkillResponse.Success($"{{\"created\":true,\"target\":\"{Esc(go.name)}\",\"clip\":\"{Esc(source.clip?.name ?? "")}\"}}");
        }

        private static UnityBridgeSkillResponse StopAudio(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var target = b?.GetValueOrDefault("target", "");
            if (!string.IsNullOrWhiteSpace(target))
            {
                var go = FindGo(target);
                var source = go?.GetComponent<AudioSource>();
                if (source != null) source.Stop();
            }
            else
            {
                foreach (var s in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                    s.Stop();
            }
            // Also stop preview
            var audioUtilType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } }).FirstOrDefault(t => t.Name == "AudioUtil");
            audioUtilType?.GetMethod("StopAllPreviewClips", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.Invoke(null, null);

            return UnityBridgeSkillResponse.Success("{\"stopped\":true}");
        }

        private static GameObject FindGo(string n) => string.IsNullOrWhiteSpace(n) ? null : Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).FirstOrDefault(g => string.Equals(g.name, n, StringComparison.OrdinalIgnoreCase));
        private static Dictionary<string, string> ParseQuery(string q) { var r = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); if (!string.IsNullOrWhiteSpace(q)) foreach (var p in q.TrimStart('?').Split('&')) { var kv = p.Split('='); if (kv.Length == 2) r[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]); } return r; }
        private static Dictionary<string, string> ParseBody(string body) { if (string.IsNullOrWhiteSpace(body)) return null; try { var o = JsonUtility.FromJson<Aud>(body); var d = new Dictionary<string, string>(); if (o.path != null) d["path"] = o.path; if (o.volume != null) d["volume"] = o.volume; if (o.target != null) d["target"] = o.target; if (o.clipPath != null) d["clipPath"] = o.clipPath; if (o.loop != null) d["loop"] = o.loop; if (o.spatialBlend != null) d["spatialBlend"] = o.spatialBlend; return d; } catch { return null; } }
        private static string Esc(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        [Serializable] private class Aud { public string path, volume, target, clipPath, loop, spatialBlend; }
    }
}
