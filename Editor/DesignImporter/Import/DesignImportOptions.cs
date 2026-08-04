using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.DesignImporter
{
    internal enum DesignScaleMode
    {
        Fit,
        Fill,
        Stretch
    }

    internal sealed class DesignImportOptions
    {
        public string OutputRoot = "Assets/UI/Generated";
        public Vector2Int TargetResolution = new Vector2Int(1920, 1080);
        public DesignScaleMode ScaleMode = DesignScaleMode.Fit;
        public int MaxTextureSize = 2048;
        public bool IncludeCanvas = true;
        public bool ExtractSharedAssets = true;
        public bool GenerateWindowCode = true;
        public string GeneratedCodeRoot = "Assets/UI/Generated/Code";
        public string CodeNamespace = "GameDeveloperKit.UI.Generated";
        public int LayerOrder = 200;
        public bool CacheEnabled = true;
    }

    internal readonly struct DesignImportProgress
    {
        public DesignImportProgress(float normalized, string message)
        {
            Normalized = Mathf.Clamp01(normalized);
            Message = message ?? string.Empty;
        }

        public float Normalized { get; }

        public string Message { get; }
    }

    internal sealed class DesignImportReport
    {
        public readonly List<string> PrefabPaths = new List<string>();
        public readonly List<string> AssetPaths = new List<string>();
        public int DownloadedAssetCount;
        public int ReusedAssetCount;
        public int SharedAssetCount;
        public TimeSpan Duration;
    }
}
