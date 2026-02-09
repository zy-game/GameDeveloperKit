using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using PhotoshopFile;
using PaintDotNet.Data.PhotoshopFileType;
using PDNWrapper;

namespace GameDeveloperKit.Editor.PsdToUgui
{
    /// <summary>
    /// PSD解析器 - 使用 PSDPlugin 库
    /// </summary>
    public class PsdParser
    {
        // 用户创建的节点 ID 从此值开始
        public const int UserCreatedIdStart = 1000000;
        private int _userLayerIdCounter = UserCreatedIdStart;

        public PsdDocument Parse(string filePath)
        {
            Debug.Log($"[PsdParser] Parsing PSD file: {filePath}");

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                var psdFile = PsdLoad.Load(stream, ELoadFlag.All);
                
                var document = new PsdDocument
                {
                    FilePath = filePath,
                    FileName = Path.GetFileNameWithoutExtension(filePath),
                    Width = psdFile.ColumnCount,
                    Height = psdFile.RowCount,
                    Layers = new List<PsdLayerInfo>()
                };

                Debug.Log($"[PsdParser] PSD Info: {document.Width}x{document.Height}, Layers: {psdFile.Layers.Count}");

                // 验证图层分组
                psdFile.VerifyLayerSections();
                
                // 应用图层分组信息（设置 IsGroup 和 IsEndGroupMarker）
                ApplyLayerSections(psdFile.Layers);

                // 解析图层树结构
                ParseLayerTree(psdFile.Layers, document);

                return document;
            }
        }
        
        /// <summary>
        /// 应用图层分组信息 - 从 PsdLoad.cs 复制
        /// </summary>
        private void ApplyLayerSections(List<PhotoshopFile.Layer> layers)
        {
            int topHiddenSectionDepth = Int32.MaxValue;
            Stack<string> layerSectionNames = new Stack<string>();

            // Layers are stored bottom-to-top, but layer sections are specified top-to-bottom.
            foreach (PhotoshopFile.Layer layer in Enumerable.Reverse(layers))
            {
                LayerSectionInfo sectionInfo = (LayerSectionInfo)layer.AdditionalInfo
                    .SingleOrDefault(x => x is LayerSectionInfo);
                if (sectionInfo == null)
                    continue;

                switch (sectionInfo.SectionType)
                {
                    case LayerSectionType.OpenFolder:
                    case LayerSectionType.ClosedFolder:
                        // Start a new layer section
                        if ((!layer.Visible) && (topHiddenSectionDepth == Int32.MaxValue))
                            topHiddenSectionDepth = layerSectionNames.Count;
                        layerSectionNames.Push(layer.Name);
                        layer.IsGroup = true;
                        break;

                    case LayerSectionType.SectionDivider:
                        // End the current layer section
                        if (layerSectionNames.Count > 0)
                            layerSectionNames.Pop();
                        if (layerSectionNames.Count == topHiddenSectionDepth)
                            topHiddenSectionDepth = Int32.MaxValue;
                        layer.IsEndGroupMarker = true;
                        break;
                }
            }
        }

        private void ParseLayerTree(List<PhotoshopFile.Layer> psdLayers, PsdDocument document)
        {
            // PSD 图层是从底部到顶部存储的，需要反转
            var reversedLayers = psdLayers.AsEnumerable().Reverse().ToList();
            
            // 使用栈来跟踪当前的父级
            var parentStack = new Stack<PsdLayerInfo>();
            PsdLayerInfo currentParent = null;

            foreach (var psdLayer in reversedLayers)
            {
                // 跳过结束标记
                if (psdLayer.IsEndGroupMarker)
                {
                    // 弹出当前父级
                    if (parentStack.Count > 0)
                    {
                        currentParent = parentStack.Pop();
                    }
                    else
                    {
                        currentParent = null;
                    }
                    continue;
                }

                var layerInfo = CreateLayerInfo(psdLayer, document);
                
                // 设置父级关系
                if (currentParent != null)
                {
                    layerInfo.ParentId = currentParent.Id;
                    currentParent.Children.Add(layerInfo);
                }
                else
                {
                    layerInfo.ParentId = -1;
                    document.Layers.Add(layerInfo);
                }

                // 如果是分组，压入栈
                if (psdLayer.IsGroup)
                {
                    parentStack.Push(currentParent);
                    currentParent = layerInfo;
                }
            }
        }

        private PsdLayerInfo CreateLayerInfo(PhotoshopFile.Layer psdLayer, PsdDocument document)
        {
            var layerInfo = new PsdLayerInfo
            {
                // 使用 PSD 原生的图层 ID，如果没有则使用备用计数器
                Id = psdLayer.LayerID > 0 ? psdLayer.LayerID : _userLayerIdCounter++,
                Name = psdLayer.Name,
                Visible = psdLayer.Visible,
                Opacity = psdLayer.Opacity / 255f,
                BlendMode = psdLayer.BlendModeKey,
                Bounds = new Rect(psdLayer.Rect.X, psdLayer.Rect.Y, psdLayer.Rect.Width, psdLayer.Rect.Height),
                Children = new List<PsdLayerInfo>(),
                IsExpanded = true
            };

            // 判断图层类型
            if (psdLayer.IsGroup)
            {
                layerInfo.LayerType = PsdLayerType.Group;
            }
            else
            {
                // 检查是否是文本图层
                var textInfo = GetTextLayerInfo(psdLayer);
                if (textInfo != null)
                {
                    layerInfo.LayerType = PsdLayerType.Text;
                    layerInfo.TextData = textInfo;
                }
                else
                {
                    layerInfo.LayerType = PsdLayerType.Normal;
                }
            }

            // 创建纹理（非分组图层）
            if (!psdLayer.IsGroup && psdLayer.Rect.Width > 0 && psdLayer.Rect.Height > 0)
            {
                try
                {
                    layerInfo.Texture = CreateTexture(psdLayer);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PsdParser] Failed to create texture for layer '{psdLayer.Name}': {ex.Message}");
                }
            }

            return layerInfo;
        }

        private PsdTextData GetTextLayerInfo(PhotoshopFile.Layer psdLayer)
        {
            foreach (var info in psdLayer.AdditionalInfo)
            {
                // 文本图层的 key 是 "TySh" (Type Shape)
                if (info.Key == "TySh")
                {
                    var textData = new PsdTextData
                    {
                        Text = psdLayer.Name,
                        FontSize = 24,
                        Color = Color.white,
                        Alignment = PsdTextAlignment.Left
                    };
                    return textData;
                }
            }
            
            return null;
        }

        private Texture2D CreateTexture(PhotoshopFile.Layer psdLayer)
        {
            var width = psdLayer.Rect.Width;
            var height = psdLayer.Rect.Height;

            if (width <= 0 || height <= 0)
                return null;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];

            var hasAlpha = psdLayer.Channels.ContainsId(-1);
            var hasRed = psdLayer.Channels.ContainsId(0);
            var hasGreen = psdLayer.Channels.ContainsId(1);
            var hasBlue = psdLayer.Channels.ContainsId(2);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    byte r = 0, g = 0, b = 0, a = 255;

                    if (hasRed)
                    {
                        var channel = psdLayer.Channels.GetId(0);
                        if (channel.ImageData.IsCreated && index < channel.ImageData.Length)
                            r = channel.ImageData[index];
                    }

                    if (hasGreen)
                    {
                        var channel = psdLayer.Channels.GetId(1);
                        if (channel.ImageData.IsCreated && index < channel.ImageData.Length)
                            g = channel.ImageData[index];
                    }

                    if (hasBlue)
                    {
                        var channel = psdLayer.Channels.GetId(2);
                        if (channel.ImageData.IsCreated && index < channel.ImageData.Length)
                            b = channel.ImageData[index];
                    }

                    if (hasAlpha)
                    {
                        var channel = psdLayer.Channels.GetId(-1);
                        if (channel.ImageData.IsCreated && index < channel.ImageData.Length)
                            a = channel.ImageData[index];
                    }

                    // PSD 是从上到下，Unity 是从下到上
                    int flippedY = height - 1 - y;
                    pixels[flippedY * width + x] = new Color32(r, g, b, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            return texture;
        }
    }
}
