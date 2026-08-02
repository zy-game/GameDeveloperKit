using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace GameDeveloperKit.DesignImporter
{
    internal static class DesignManifestSchema
    {
        public const string CurrentVersion = "1.0";
    }

    [JsonConverter(typeof(StringEnumConverter))]
    internal enum DesignSourceKind
    {
        Manifest,
        Lanhu,
        Figma
    }

    [JsonConverter(typeof(StringEnumConverter))]
    internal enum DesignNodeKind
    {
        Container,
        Image,
        Text
    }

    [JsonConverter(typeof(StringEnumConverter))]
    internal enum DesignComponentKind
    {
        None,
        Button,
        Toggle,
        Slider,
        InputField,
        ScrollRect
    }

    [Serializable]
    internal sealed class DesignDocument
    {
        [JsonProperty("schemaVersion")]
        public string SchemaVersion = DesignManifestSchema.CurrentVersion;

        [JsonProperty("exporterVersion")]
        public string ExporterVersion = string.Empty;

        [JsonProperty("id")]
        public string Id = string.Empty;

        [JsonProperty("name")]
        public string Name = string.Empty;

        [JsonProperty("source")]
        public DesignSourceKind Source = DesignSourceKind.Manifest;

        [JsonProperty("teamId")]
        public string TeamId = string.Empty;

        [JsonProperty("sourceRevision")]
        public string SourceRevision = string.Empty;

        [JsonProperty("sourceUpdatedAt")]
        public string SourceUpdatedAt = string.Empty;

        [JsonProperty("pages")]
        public List<DesignPage> Pages = new List<DesignPage>();

        [JsonProperty("assets")]
        public List<DesignAsset> Assets = new List<DesignAsset>();

        [JsonIgnore]
        public string SourceLocation = string.Empty;

        public void Normalize()
        {
            SchemaVersion = string.IsNullOrWhiteSpace(SchemaVersion)
                ? DesignManifestSchema.CurrentVersion
                : SchemaVersion.Trim();
            ExporterVersion = ExporterVersion?.Trim() ?? string.Empty;
            Id = Id?.Trim() ?? string.Empty;
            Name = string.IsNullOrWhiteSpace(Name) ? "Untitled Design" : Name.Trim();
            TeamId = TeamId?.Trim() ?? string.Empty;
            SourceRevision = SourceRevision?.Trim() ?? string.Empty;
            SourceUpdatedAt = SourceUpdatedAt?.Trim() ?? string.Empty;
            Pages ??= new List<DesignPage>();
            Assets ??= new List<DesignAsset>();

            for (var i = 0; i < Pages.Count; i++)
            {
                Pages[i]?.Normalize(i);
            }

            for (var i = 0; i < Assets.Count; i++)
            {
                Assets[i]?.Normalize(i);
            }
        }
    }

    [Serializable]
    internal sealed class DesignPage
    {
        [JsonProperty("id")]
        public string Id = string.Empty;

        [JsonProperty("name")]
        public string Name = string.Empty;

        [JsonProperty("width")]
        public float Width;

        [JsonProperty("height")]
        public float Height;

        [JsonProperty("previewUrl")]
        public string PreviewUrl = string.Empty;

        [JsonProperty("revisionId")]
        public string RevisionId = string.Empty;

        [JsonProperty("revisionTimestamp")]
        public string RevisionTimestamp = string.Empty;

        [JsonProperty("root")]
        public DesignNode Root;

        [JsonIgnore]
        public bool Selected = true;

        [JsonIgnore]
        public string CachedPreviewPath = string.Empty;

        public void Normalize(int index)
        {
            Id = string.IsNullOrWhiteSpace(Id) ? $"page-{index + 1}" : Id.Trim();
            Name = string.IsNullOrWhiteSpace(Name) ? $"Page {index + 1}" : Name.Trim();
            PreviewUrl = PreviewUrl?.Trim() ?? string.Empty;
            RevisionId = RevisionId?.Trim() ?? string.Empty;
            RevisionTimestamp = RevisionTimestamp?.Trim() ?? string.Empty;
            Width = Mathf.Max(1f, Width);
            Height = Mathf.Max(1f, Height);
            Root ??= new DesignNode
            {
                Id = Id + "-root",
                Name = Name,
                Kind = DesignNodeKind.Container,
                Width = Width,
                Height = Height
            };
            Root.Normalize(Id + "-root", Name, Width, Height);
        }
    }

    [Serializable]
    internal sealed class DesignNode
    {
        [JsonProperty("id")]
        public string Id = string.Empty;

        [JsonProperty("name")]
        public string Name = string.Empty;

        [JsonProperty("kind")]
        public DesignNodeKind Kind = DesignNodeKind.Container;

        [JsonProperty("x")]
        public float X;

        [JsonProperty("y")]
        public float Y;

        [JsonProperty("width")]
        public float Width;

        [JsonProperty("height")]
        public float Height;

        [JsonProperty("anchorMin")]
        public DesignVector2 AnchorMin;

        [JsonProperty("anchorMax")]
        public DesignVector2 AnchorMax;

        [JsonProperty("pivot")]
        public DesignVector2 Pivot;

        [JsonProperty("visible")]
        public bool Visible = true;

        [JsonProperty("opacity")]
        public float Opacity = 1f;

        [JsonProperty("assetId")]
        public string AssetId = string.Empty;

        [JsonProperty("text")]
        public string Text = string.Empty;

        [JsonProperty("fontSize")]
        public float FontSize = 24f;

        [JsonProperty("fontName")]
        public string FontName = string.Empty;

        [JsonProperty("fontPostScriptName")]
        public string FontPostScriptName = string.Empty;

        [JsonProperty("fontStyleName")]
        public string FontStyleName = string.Empty;

        [JsonProperty("bold")]
        public bool Bold;

        [JsonProperty("italic")]
        public bool Italic;

        [JsonProperty("tracking")]
        public float Tracking;

        [JsonProperty("lineHeight")]
        public float LineHeight;

        [JsonProperty("wordWrap")]
        public bool WordWrap = true;

        [JsonProperty("overflow")]
        public string Overflow = "overflow";

        [JsonProperty("color")]
        public string Color = "#FFFFFFFF";

        [JsonProperty("backgroundColor")]
        public string BackgroundColor = string.Empty;

        [JsonProperty("textAlignment")]
        public string TextAlignment = "left";

        [JsonProperty("cornerRadius")]
        public float CornerRadius;

        [JsonProperty("clipsContent")]
        public bool ClipsContent;

        [JsonProperty("nineSlice")]
        public bool NineSlice;

        [JsonProperty("shared")]
        public bool Shared;

        [JsonProperty("component")]
        public DesignComponentKind Component;

        [JsonProperty("bindingName")]
        public string BindingName = string.Empty;

        [JsonProperty("interactable")]
        public bool Interactable = true;

        [JsonProperty("toggleValue")]
        public bool ToggleValue;

        [JsonProperty("sliderMinValue")]
        public float SliderMinValue;

        [JsonProperty("sliderMaxValue")]
        public float SliderMaxValue = 1f;

        [JsonProperty("sliderValue")]
        public float SliderValue;

        [JsonProperty("sliderWholeNumbers")]
        public bool SliderWholeNumbers;

        [JsonProperty("scrollHorizontal")]
        public bool ScrollHorizontal = true;

        [JsonProperty("scrollVertical")]
        public bool ScrollVertical = true;

        [JsonProperty("border")]
        public DesignBorder Border = new DesignBorder();

        [JsonProperty("children")]
        public List<DesignNode> Children = new List<DesignNode>();

        public void Normalize(string fallbackId, string fallbackName, float fallbackWidth, float fallbackHeight)
        {
            Id = string.IsNullOrWhiteSpace(Id) ? fallbackId : Id.Trim();
            Name = string.IsNullOrWhiteSpace(Name) ? fallbackName : Name.Trim();
            AssetId = AssetId?.Trim() ?? string.Empty;
            Text ??= string.Empty;
            FontName = FontName?.Trim() ?? string.Empty;
            FontPostScriptName = FontPostScriptName?.Trim() ?? string.Empty;
            FontStyleName = FontStyleName?.Trim() ?? string.Empty;
            Color = string.IsNullOrWhiteSpace(Color) ? "#FFFFFFFF" : Color.Trim();
            BackgroundColor = BackgroundColor?.Trim() ?? string.Empty;
            TextAlignment = string.IsNullOrWhiteSpace(TextAlignment) ? "left" : TextAlignment.Trim();
            Overflow = string.IsNullOrWhiteSpace(Overflow) ? "overflow" : Overflow.Trim();
            BindingName = BindingName?.Trim() ?? string.Empty;
            Width = Width > 0f ? Width : fallbackWidth;
            Height = Height > 0f ? Height : fallbackHeight;
            Opacity = Mathf.Clamp01(Opacity);
            FontSize = Mathf.Max(1f, FontSize);
            Tracking = Mathf.Clamp(Tracking, -1000f, 10000f);
            LineHeight = Mathf.Max(0f, LineHeight);
            SliderMaxValue = Mathf.Max(SliderMinValue, SliderMaxValue);
            SliderValue = Mathf.Clamp(SliderValue, SliderMinValue, SliderMaxValue);
            AnchorMin?.Clamp01();
            AnchorMax?.Clamp01();
            Pivot?.Clamp01();
            Border ??= new DesignBorder();
            Border.Clamp();
            Children ??= new List<DesignNode>();
            for (var i = 0; i < Children.Count; i++)
            {
                Children[i]?.Normalize(Id + "-" + i, "Layer " + (i + 1), 1f, 1f);
            }
        }

        public IEnumerable<DesignNode> DescendantsAndSelf()
        {
            yield return this;
            if (Children == null)
            {
                yield break;
            }

            foreach (var child in Children)
            {
                if (child == null)
                {
                    continue;
                }

                foreach (var descendant in child.DescendantsAndSelf())
                {
                    yield return descendant;
                }
            }
        }
    }

    [Serializable]
    internal sealed class DesignVector2
    {
        public DesignVector2()
        {
        }

        public DesignVector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        [JsonProperty("x")]
        public float X;

        [JsonProperty("y")]
        public float Y;

        [JsonIgnore]
        public Vector2 Value => new Vector2(X, Y);

        public void Clamp01()
        {
            X = Mathf.Clamp01(X);
            Y = Mathf.Clamp01(Y);
        }
    }

    [Serializable]
    internal sealed class DesignBorder
    {
        [JsonProperty("left")]
        public float Left;

        [JsonProperty("bottom")]
        public float Bottom;

        [JsonProperty("right")]
        public float Right;

        [JsonProperty("top")]
        public float Top;

        public bool HasValue => Left > 0f || Bottom > 0f || Right > 0f || Top > 0f;

        public void Clamp()
        {
            Left = Mathf.Max(0f, Left);
            Bottom = Mathf.Max(0f, Bottom);
            Right = Mathf.Max(0f, Right);
            Top = Mathf.Max(0f, Top);
        }

        public Vector4 ToVector4(float pixelScale)
        {
            var scale = Mathf.Max(0.01f, pixelScale);
            return new Vector4(Left * scale, Bottom * scale, Right * scale, Top * scale);
        }
    }

    [Serializable]
    internal sealed class DesignAsset
    {
        [JsonProperty("id")]
        public string Id = string.Empty;

        [JsonProperty("name")]
        public string Name = string.Empty;

        [JsonProperty("url")]
        public string Url = string.Empty;

        [JsonProperty("format")]
        public string Format = "png";

        [JsonProperty("pixelScale")]
        public float PixelScale = 1f;

        [JsonProperty("shared")]
        public bool Shared;

        [JsonIgnore]
        public string CachedFilePath = string.Empty;

        [JsonIgnore]
        public string CachedHash = string.Empty;

        public void Normalize(int index)
        {
            Id = string.IsNullOrWhiteSpace(Id) ? $"asset-{index + 1}" : Id.Trim();
            Name = string.IsNullOrWhiteSpace(Name) ? Id : Name.Trim();
            Url = Url?.Trim() ?? string.Empty;
            Format = DesignPathUtility.NormalizeImageExtension(Format);
            PixelScale = Mathf.Max(0.01f, PixelScale);
        }
    }
}
