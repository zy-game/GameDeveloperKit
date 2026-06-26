using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityLightingSkill : IUnityBridgeSkill
    {
        public string Name => "unity-lighting";

        public string Description =>
            "Configure lighting, bake lightmaps, clear baked data.";

        public string Trigger =>
            "Use when the user wants to bake lightmaps, configure lighting, or check lightmapping status.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("GET", "/lighting", "Returns lightmapping status and settings."),
            new UnityBridgeSkillEndpoint("POST", "/lighting/bake", "Starts an async lightmap bake."),
            new UnityBridgeSkillEndpoint("GET", "/lighting/status", "Whether lightmapping is currently running."),
            new UnityBridgeSkillEndpoint("POST", "/lighting/clear", "Clears all baked lightmap data.")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js lighting`",
            "`node scripts/unity-bridge.js lighting-bake`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Baking is async but blocks the editor during processing.",
            "Use the status endpoint to poll for completion."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var p = request.Path.Trim('/');
            return (request.Method == "GET" && (p == "lighting" || p == "lighting/status"))
                || (request.Method == "POST" && (p == "lighting/bake" || p == "lighting/clear"));
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            return request.Path.Trim('/') switch
            {
                "lighting" => Get(),
                "lighting/bake" => Bake(),
                "lighting/status" => Status(),
                "lighting/clear" => Clear(),
                _ => UnityBridgeSkillResponse.Error(404, "Unknown")
            };
        }

        private static UnityBridgeSkillResponse Get()
        {
            return UnityBridgeSkillResponse.Success(
                $"{{\"isBaking\":{(Lightmapping.isRunning ? "true" : "false")},\"autoGenerate\":\"{Lightmapping.giWorkflowMode}\",\"bakedLightmaps\":{LightmapSettings.lightmaps?.Length ?? 0}}}");
        }

        private static UnityBridgeSkillResponse Bake() { Lightmapping.BakeAsync(); return UnityBridgeSkillResponse.Success("{\"baking\":true}"); }
        private static UnityBridgeSkillResponse Status() => UnityBridgeSkillResponse.Success($"{{\"isBaking\":{(Lightmapping.isRunning ? "true" : "false")}}}");
        private static UnityBridgeSkillResponse Clear() { Lightmapping.Clear(); return UnityBridgeSkillResponse.Success("{\"cleared\":true}"); }
    }
}
