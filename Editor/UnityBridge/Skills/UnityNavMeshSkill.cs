using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityNavMeshSkill : IUnityBridgeSkill
    {
        public string Name => "unity-navmesh";

        public string Description =>
            "Bake and manage NavMesh surfaces for AI pathfinding.";

        public string Trigger =>
            "Use when the user wants to bake NavMesh, check NavMesh status, or configure navigation settings.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("GET", "/navmesh", "Returns NavMesh build settings and status."),
            new UnityBridgeSkillEndpoint("POST", "/navmesh/bake", "Bakes the NavMesh for the active scene."),
            new UnityBridgeSkillEndpoint("POST", "/navmesh/clear", "Clears the current NavMesh data.")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js navmesh`",
            "`node scripts/unity-bridge.js navmesh-bake`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "NavMesh baking requires objects to be marked as Navigation Static.",
            "The active scene must contain NavMesh surfaces or be properly configured."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var p = request.Path.Trim('/');
            return (request.Method == "GET" && p == "navmesh")
                || (request.Method == "POST" && (p == "navmesh/bake" || p == "navmesh/clear"));
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            return request.Path.Trim('/') switch
            {
                "navmesh" => GetInfo(),
                "navmesh/bake" => Bake(),
                "navmesh/clear" => Clear(),
                _ => UnityBridgeSkillResponse.Error(404, "Unknown")
            };
        }

        private static UnityBridgeSkillResponse GetInfo()
        {
            var settings = NavMesh.GetSettingsByIndex(0);
            var triangulation = NavMesh.CalculateTriangulation();
            return UnityBridgeSkillResponse.Success(
                $"{{\"agentRadius\":{settings.agentRadius:F3},\"agentHeight\":{settings.agentHeight:F3},\"agentSlope\":{settings.agentSlope:F1},\"agentClimb\":{settings.agentClimb:F3},\"vertices\":{triangulation.vertices?.Length ?? 0},\"indices\":{triangulation.indices?.Length ?? 0}}}");
        }

        private static UnityBridgeSkillResponse Bake()
        {
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
            return UnityBridgeSkillResponse.Success("{\"baked\":true}");
        }

        private static UnityBridgeSkillResponse Clear()
        {
            UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
            return UnityBridgeSkillResponse.Success("{\"cleared\":true}");
        }
    }
}
