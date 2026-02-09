using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace GameDeveloperKit.Editor.PsdToUgui
{
    /// <summary>
    /// PSD图层类型
    /// </summary>
    public enum PsdLayerType
    {
        Normal,
        Group,
        Text,
        Shape,
        Adjustment
    }

    /// <summary>
    /// 文本对齐方式
    /// </summary>
    public enum PsdTextAlignment
    {
        Left,
        Center,
        Right,
        Justify
    }

    /// <summary>
    /// PSD图层效果类型
    /// </summary>
    [Flags]
    public enum PsdLayerEffects
    {
        None = 0,
        DropShadow = 1 << 0,
        InnerShadow = 1 << 1,
        OuterGlow = 1 << 2,
        InnerGlow = 1 << 3,
        Bevel = 1 << 4,
        Stroke = 1 << 5,
        ColorOverlay = 1 << 6,
        GradientOverlay = 1 << 7,
        PatternOverlay = 1 << 8
    }

    /// <summary>
    /// 描边效果数据
    /// </summary>
    [Serializable]
    public class PsdStrokeEffect
    {
        public bool Enabled;
        public Color32 Color;
        public int Size;
        public float Opacity;
        public string Position; // Outside, Inside, Center
    }

    /// <summary>
    /// 阴影效果数据
    /// </summary>
    [Serializable]
    public class PsdShadowEffect
    {
        public bool Enabled;
        public Color32 Color;
        public float Opacity;
        public float Angle;
        public float Distance;
        public float Spread;
        public float Size;
    }

    /// <summary>
    /// 发光效果数据
    /// </summary>
    [Serializable]
    public class PsdGlowEffect
    {
        public bool Enabled;
        public Color32 Color;
        public float Opacity;
        public float Size;
        public float Spread;
    }

    /// <summary>
    /// 渐变叠加效果数据
    /// </summary>
    [Serializable]
    public class PsdGradientEffect
    {
        public bool Enabled;
        public float Opacity;
        public float Angle;
        public Color32[] Colors;
        public float[] Positions;
        public string Type; // Linear, Radial, Angle, Reflected, Diamond
    }

    /// <summary>
    /// 图层效果集合
    /// </summary>
    [Serializable]
    public class PsdLayerEffectData
    {
        public PsdStrokeEffect Stroke;
        public PsdShadowEffect DropShadow;
        public PsdShadowEffect InnerShadow;
        public PsdGlowEffect OuterGlow;
        public PsdGlowEffect InnerGlow;
        public PsdGradientEffect GradientOverlay;
    }

    /// <summary>
    /// 文本图层数据
    /// </summary>
    [Serializable]
    public class PsdTextData
    {
        public string Text;
        public string FontName;
        public float FontSize;
        public Color32 Color;
        public PsdTextAlignment Alignment;
        public bool Bold;
        public bool Italic;
        public float Leading; // 行距
        public float Tracking; // 字间距
    }
    
    /// <summary>
    /// 锚点预设类型
    /// </summary>
    public enum AnchorPreset
    {
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleCenter,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight,
        StretchTop,
        StretchMiddle,
        StretchBottom,
        StretchLeft,
        StretchCenter,
        StretchRight,
        StretchAll
    }
    
    /// <summary>
    /// 图片类型
    /// </summary>
    public enum ImageType
    {
        Simple,
        Sliced,
        Tiled,
        Filled
    }
    
    /// <summary>
    /// 布局类型
    /// </summary>
    public enum LayoutType
    {
        None,
        HorizontalLayout,
        VerticalLayout,
        GridLayout,
        ScrollView
    }
    
    /// <summary>
    /// ContentSizeFitter 适配模式
    /// </summary>
    public enum FitMode
    {
        Unconstrained,
        MinSize,
        PreferredSize
    }
    
    /// <summary>
    /// 布局配置
    /// </summary>
    [Serializable]
    public class LayoutConfig
    {
        public LayoutType LayoutType = LayoutType.None;
        
        // 通用布局设置
        public float Spacing = 0;
        public int PaddingLeft = 0;
        public int PaddingRight = 0;
        public int PaddingTop = 0;
        public int PaddingBottom = 0;
        
        // ContentSizeFitter
        public FitMode HorizontalFit = FitMode.Unconstrained;
        public FitMode VerticalFit = FitMode.Unconstrained;
        
        // ScrollView 设置
        public bool ScrollHorizontal = false;
        public bool ScrollVertical = true;
    }

    /// <summary>
    /// PSD图层信息
    /// </summary>
    [Serializable]
    public class PsdLayerInfo
    {
        public string Name;
        public int Id;
        public int ParentId = -1;
        public PsdLayerType LayerType;
        public Rect Bounds;
        public bool Visible;
        public float Opacity;
        public string BlendMode;
        public bool IsClipped;
        
        // 锚点设置
        public AnchorPreset AnchorPreset = AnchorPreset.MiddleCenter;
        
        // 图片设置
        public ImageType ImageType = ImageType.Simple;
        public Vector4 SliceBorder; // 9宫格边距 (left, bottom, right, top)
        
        // 布局配置
        public LayoutConfig LayoutConfig;
        
        // 图层效果
        public PsdLayerEffects Effects;
        public PsdLayerEffectData EffectData;
        
        // 文本数据（仅文本图层）
        public PsdTextData TextData;
        
        // 图像数据
        public Texture2D Texture;
        
        // 子图层
        public List<PsdLayerInfo> Children = new();
        
        // 编辑器状态
        [NonSerialized] public bool IsExpanded = true;
        [NonSerialized] public bool IsSelected;
        
        public bool IsGroup => LayerType == PsdLayerType.Group;
        public bool IsTextLayer => LayerType == PsdLayerType.Text;
        public bool IsImageLayer => LayerType == PsdLayerType.Normal && Texture != null;
        
        public Vector2 Position => new(Bounds.x, Bounds.y);
        public Vector2 Size => new(Bounds.width, Bounds.height);
    }

    /// <summary>
    /// PSD文档信息
    /// </summary>
    [Serializable]
    public class PsdDocument
    {
        public string FilePath;
        public string FileName;
        public int Width;
        public int Height;
        public int Depth; // 位深度
        public int ColorMode;
        public List<PsdLayerInfo> Layers = new();
        
        // PSD 文档配置
        [NonSerialized]
        public PsdDocumentConfig Config;
        
        // 扁平化的图层列表（用于快速查找）
        [NonSerialized] 
        private Dictionary<int, PsdLayerInfo> _layerMap;
        
        public PsdLayerInfo GetLayerById(int id)
        {
            if (_layerMap == null)
            {
                _layerMap = new Dictionary<int, PsdLayerInfo>();
                BuildLayerMap(Layers);
            }
            return _layerMap.TryGetValue(id, out var layer) ? layer : null;
        }
        
        private void BuildLayerMap(List<PsdLayerInfo> layers)
        {
            foreach (var layer in layers)
            {
                _layerMap[layer.Id] = layer;
                if (layer.Children.Count > 0)
                {
                    BuildLayerMap(layer.Children);
                }
            }
        }
        
        public void InvalidateLayerMap()
        {
            _layerMap = null;
        }
        
        public int GetNextLayerId()
        {
            int maxId = 0;
            FindMaxId(Layers, ref maxId);
            // 用户创建的节点 ID 从 1000000 开始，以便区分 PSD 解析的图层
            return Math.Max(maxId + 1, PsdParser.UserCreatedIdStart);
        }
        
        private void FindMaxId(List<PsdLayerInfo> layers, ref int maxId)
        {
            foreach (var layer in layers)
            {
                if (layer.Id > maxId) maxId = layer.Id;
                if (layer.Children.Count > 0)
                {
                    FindMaxId(layer.Children, ref maxId);
                }
            }
        }
    }
}
