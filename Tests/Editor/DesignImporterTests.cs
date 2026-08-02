using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Net.Http;
using System.Text;
using System.Threading;
using GameDeveloperKit.DesignImporter;
using GameDeveloperKit.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UGUIButton = UnityEngine.UI.Button;
using UGUIScrollRect = UnityEngine.UI.ScrollRect;
using UGUISlider = UnityEngine.UI.Slider;
using UGUIToggle = UnityEngine.UI.Toggle;

namespace GameDeveloperKit.Tests
{
    public sealed class DesignImporterTests
    {
        [Test]
        public void ManifestCodec_LanhuLayeredManifest_PreservesHierarchyAndAssets()
        {
            const string json = @"{
  ""schemaVersion"": ""1.0"",
  ""id"": ""b807477a-7f1e-4144-9bf8-541e166191d9"",
  ""name"": ""Lanhu Fixture"",
  ""source"": ""Lanhu"",
  ""pages"": [{
    ""id"": ""settlement-b"",
    ""name"": ""Settlement B"",
    ""width"": 1920,
    ""height"": 1080,
    ""previewUrl"": ""https://assets.example/settlement-b.png"",
    ""root"": {
      ""id"": ""settlement-b:root"",
      ""name"": ""Settlement B"",
      ""kind"": ""Container"",
      ""width"": 1920,
      ""height"": 1080,
      ""children"": [{
        ""id"": ""dialog"",
        ""name"": ""Dialog"",
        ""kind"": ""Container"",
        ""x"": 803,
        ""y"": 895,
        ""width"": 343,
        ""height"": 108,
        ""children"": [
          {
            ""id"": ""confirm-button"",
            ""name"": ""Confirm Button"",
            ""kind"": ""Image"",
            ""x"": 1,
            ""y"": 1,
            ""width"": 341,
            ""height"": 106,
            ""assetId"": ""confirm-button:asset""
          },
          {
            ""id"": ""caption"",
            ""name"": ""Caption"",
            ""kind"": ""Text"",
            ""x"": 100,
            ""y"": 20,
            ""width"": 140,
            ""height"": 48,
            ""text"": ""Done"",
            ""fontSize"": 32
          }
        ]
      }]
    }
  }],
  ""assets"": [
    { ""id"": ""confirm-button:asset"", ""name"": ""Confirm Button"", ""url"": ""https://assets.example/confirm-button.png"" }
  ]
}";

            var document = DesignManifestCodec.Parse(json);

            Assert.AreEqual(DesignSourceKind.Lanhu, document.Source);
            Assert.AreEqual(1, document.Pages.Count);
            Assert.AreEqual(1, document.Assets.Count);
            Assert.AreEqual(DesignNodeKind.Container, document.Pages[0].Root.Kind);
            var dialog = document.Pages[0].Root.Children.Single();
            Assert.AreEqual(DesignNodeKind.Container, dialog.Kind);
            Assert.AreEqual(803f, dialog.X);
            Assert.AreEqual(895f, dialog.Y);
            var image = dialog.Children.Single(x => x.Kind == DesignNodeKind.Image);
            Assert.AreEqual(1f, image.X);
            Assert.AreEqual(1f, image.Y);
            Assert.AreEqual("confirm-button:asset", image.AssetId);
            Assert.AreEqual("Done", dialog.Children.Single(x => x.Kind == DesignNodeKind.Text).Text);
            Assert.IsTrue(document.Pages.All(x => x.Selected));
        }

        [Test]
        public void ManifestCodec_TextStyle_PreservesFontMetrics()
        {
            const string json = @"{
  ""schemaVersion"": ""1.0"", ""name"": ""Text"",
  ""pages"": [{ ""id"": ""p"", ""name"": ""P"", ""width"": 100, ""height"": 100,
    ""root"": { ""id"": ""t"", ""name"": ""T"", ""kind"": ""Text"", ""width"": 80, ""height"": 30,
      ""text"": ""标题"", ""fontName"": ""STZhongsong"", ""fontPostScriptName"": ""STZhongsong-Regular"",
      ""fontStyleName"": ""Regular"", ""fontSize"": 48.3613, ""bold"": true,
      ""tracking"": 100, ""lineHeight"": 56 } }], ""assets"": [] }";

            var node = DesignManifestCodec.Parse(json).Pages[0].Root;

            Assert.AreEqual("STZhongsong", node.FontName);
            Assert.AreEqual("STZhongsong-Regular", node.FontPostScriptName);
            Assert.AreEqual(48.3613f, node.FontSize, 0.0001f);
            Assert.AreEqual(100f, node.Tracking);
            Assert.AreEqual(56f, node.LineHeight);
            Assert.IsTrue(node.Bold);
        }

        [Test]
        public void ManifestCodec_MissingImageAsset_ThrowsReadableError()
        {
            const string json = @"{
  ""schemaVersion"": ""1.0"",
  ""name"": ""Broken"",
  ""pages"": [{
    ""id"": ""page"",
    ""name"": ""Page"",
    ""width"": 100,
    ""height"": 100,
    ""root"": { ""id"": ""image"", ""name"": ""Image"", ""kind"": ""Image"", ""width"": 100, ""height"": 100, ""assetId"": ""missing"" }
  }],
  ""assets"": []
}";

            var exception = Assert.Throws<InvalidDataException>(() => DesignManifestCodec.Parse(json));

            StringAssert.Contains("不存在的资源", exception.Message);
        }

        [Test]
        public void ManifestCodec_LegacyLanhuScreenshotManifest_ThrowsReadableError()
        {
            const string json = @"{
  ""schemaVersion"": ""1.0"",
  ""name"": ""Legacy Lanhu"",
  ""source"": ""Lanhu"",
  ""pages"": [{
    ""id"": ""page"",
    ""name"": ""Settlement B"",
    ""width"": 1920,
    ""height"": 1080,
    ""root"": {
      ""id"": ""page-root"",
      ""name"": ""Settlement B"",
      ""kind"": ""Image"",
      ""width"": 1920,
      ""height"": 1080,
      ""assetId"": ""page-image"",
      ""children"": []
    }
  }],
  ""assets"": [{
    ""id"": ""page-image"",
    ""name"": ""Settlement B"",
    ""url"": ""https://assets.example/settlement-b.png""
  }]
}";

            var exception = Assert.Throws<InvalidDataException>(() => DesignManifestCodec.Parse(json));

            StringAssert.Contains("旧版蓝湖整页截图清单", exception.Message);
            StringAssert.Contains("layered-design-manifest", exception.Message);
        }

        [Test]
        public void FigmaParser_ConvertsFramesToPagesAndUsesParentRelativeCoordinates()
        {
            const string json = @"{
  ""name"": ""HUD"",
  ""document"": {
    ""id"": ""root"",
    ""type"": ""DOCUMENT"",
    ""children"": [{
      ""id"": ""canvas"",
      ""type"": ""CANVAS"",
      ""children"": [{
        ""id"": ""frame"",
        ""name"": ""Home"",
        ""type"": ""FRAME"",
        ""absoluteBoundingBox"": { ""x"": 100, ""y"": 200, ""width"": 1920, ""height"": 1080 },
        ""fills"": [{ ""type"": ""SOLID"", ""color"": { ""r"": 0.1, ""g"": 0.2, ""b"": 0.3, ""a"": 1 } }],
        ""children"": [
          {
            ""id"": ""title"",
            ""name"": ""Title"",
            ""type"": ""TEXT"",
            ""characters"": ""Start"",
            ""absoluteBoundingBox"": { ""x"": 140, ""y"": 260, ""width"": 240, ""height"": 60 },
            ""style"": { ""fontSize"": 32, ""textAlignHorizontal"": ""CENTER"" },
            ""fills"": [{ ""type"": ""SOLID"", ""color"": { ""r"": 1, ""g"": 1, ""b"": 1 } }]
          },
          {
            ""id"": ""icon"",
            ""name"": ""Play Icon"",
            ""type"": ""VECTOR"",
            ""absoluteBoundingBox"": { ""x"": 180, ""y"": 340, ""width"": 64, ""height"": 64 }
          }
        ]
      }]
    }]
  }
}";

            var result = FigmaDocumentParser.Parse("file-key", json, 2f);

            Assert.AreEqual("HUD", result.Document.Name);
            Assert.AreEqual(1, result.Document.Pages.Count);
            var page = result.Document.Pages[0];
            Assert.AreEqual(new Vector2(1920f, 1080f), new Vector2(page.Width, page.Height));
            var title = page.Root.Children.Single(x => x.Id == "title");
            Assert.AreEqual(40f, title.X);
            Assert.AreEqual(60f, title.Y);
            Assert.AreEqual(32f, title.FontSize);
            Assert.AreEqual("center", title.TextAlignment);
            Assert.AreEqual("#FFFFFFFF", title.Color);
            Assert.Contains("icon", result.RenderNodeIds);
            Assert.AreEqual(2f, result.Document.Assets.Single().PixelScale);
        }

        [TestCase("abc123", "abc123")]
        [TestCase("https://www.figma.com/design/ABC_def/My-File", "ABC_def")]
        [TestCase("https://www.figma.com/file/XYZ987/Legacy", "XYZ987")]
        public void FigmaParser_ExtractFileKey_SupportsKeyAndKnownUrls(string input, string expected)
        {
            Assert.AreEqual(expected, FigmaDocumentParser.ExtractFileKey(input));
        }

        [Test]
        public void LanhuProjectAddress_ParsesHashQuery()
        {
            var address = LanhuProjectAddress.Parse(
                "https://lanhuapp.com/web/#/item/project/stage?pid=project-1&teamId=team-2&tid=ignored");

            Assert.AreEqual("project-1", address.ProjectId);
            Assert.AreEqual("team-2", address.TeamId);
        }

        [Test]
        public void VersionDiff_TracksNewUpdatedAndDeletedPagesByStableId()
        {
            var previous = CreateDiffDocument(("keep", "Old"), ("gone", "Gone"));
            var current = CreateDiffDocument(("keep", "New"), ("added", "Added"));

            var diff = DesignVersionDiff.Compare(previous, current);

            Assert.AreEqual(DesignChangeKind.Updated, diff.Pages.Single(x => x.Page.Id == "keep").Kind);
            Assert.AreEqual(DesignChangeKind.New, diff.Pages.Single(x => x.Page.Id == "added").Kind);
            Assert.AreEqual(DesignChangeKind.Deleted, diff.Pages.Single(x => x.Page.Id == "gone").Kind);
            Assert.AreEqual(DesignChangeKind.Updated, diff.NodeChange("keep", "keep-text"));
        }

        [Test]
        public void MappingStore_ReparentsLayerAndRestoresItOnFreshDesignData()
        {
            var cacheRoot = Path.Combine(Path.GetTempPath(), "gdk-design-mapping-" + Guid.NewGuid().ToString("N"));
            try
            {
                var page = CreateMappingPage();
                var sourceParent = page.Root.Children.Single(x => x.Id == "left");
                var targetParent = page.Root.Children.Single(x => x.Id == "right");
                var layer = sourceParent.Children.Single();
                Assert.IsTrue(DesignMappingStore.MoveNode(page, layer, targetParent, 0));
                layer.X = 12f;
                DesignMappingStore.Save(cacheRoot, page);

                var fresh = CreateMappingPage();
                Assert.IsTrue(DesignMappingStore.Apply(cacheRoot, fresh));

                var restoredParent = fresh.Root.Children.Single(x => x.Id == "right");
                Assert.AreEqual("layer", restoredParent.Children.Single().Id);
                Assert.AreEqual(12f, restoredParent.Children.Single().X);
            }
            finally
            {
                if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, true);
            }
        }

        [Test]
        public void ImporterWindow_DropTargetValidationRejectsSelfAndDescendants()
        {
            var page = CreateMappingPage();
            var left = page.Root.Children.Single(node => node.Id == "left");
            var right = page.Root.Children.Single(node => node.Id == "right");
            var layer = left.Children.Single();
            var window = ScriptableObject.CreateInstance<DesignImporterWindow>();
            try
            {
                var type = typeof(DesignImporterWindow);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                type.GetField("m_SelectedPage", flags)?.SetValue(window, page);
                var validate = type.GetMethod("IsValidDropTarget", flags);
                var placementType = type.GetNestedType("LayerDropPlacement", BindingFlags.NonPublic);
                Assert.NotNull(validate);
                Assert.NotNull(placementType);

                var inside = Enum.Parse(placementType, "Inside");

                Assert.IsTrue((bool)validate.Invoke(window, new object[] { layer, right, inside }));
                Assert.IsFalse((bool)validate.Invoke(window, new object[] { layer, layer, inside }));
                Assert.IsFalse((bool)validate.Invoke(window, new object[] { left, layer, inside }));
                Assert.IsFalse((bool)validate.Invoke(window, new object[] { page.Root, right, inside }));

                right.Kind = DesignNodeKind.Image;
                Assert.IsTrue((bool)validate.Invoke(window, new object[] { layer, right, inside }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ImporterWindow_NodeRowIncludesInlineDropFeedback()
        {
            var window = ScriptableObject.CreateInstance<DesignImporterWindow>();
            try
            {
                var makeNodeRow = typeof(DesignImporterWindow).GetMethod(
                    "MakeNodeRow",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(makeNodeRow);

                var row = makeNodeRow.Invoke(window, null) as VisualElement;
                Assert.NotNull(row);
                var dragLabel = row.Q<Label>("node-drag-label");
                var dropLabel = row.Q<Label>("node-drop-label");
                var expandButton = row.Q<Button>("node-expand");
                Assert.NotNull(dragLabel);
                Assert.NotNull(dropLabel);
                Assert.NotNull(expandButton);
                Assert.AreEqual(DisplayStyle.None, dragLabel.style.display.value);
                Assert.AreEqual(DisplayStyle.None, dropLabel.style.display.value);
                Assert.AreEqual(PickingMode.Ignore, dragLabel.pickingMode);
                Assert.AreEqual(PickingMode.Ignore, dropLabel.pickingMode);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ImporterWindow_PageRowDoesNotExposeBatchGenerationToggle()
        {
            var window = ScriptableObject.CreateInstance<DesignImporterWindow>();
            try
            {
                var makePageRow = typeof(DesignImporterWindow).GetMethod(
                    "MakePageRow",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(makePageRow);

                var row = makePageRow.Invoke(window, null) as VisualElement;
                Assert.NotNull(row);
                Assert.IsNull(row.Q<Toggle>("page-toggle"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ImporterWindow_SinglePageDocumentUsesOnlyCurrentPage()
        {
            var document = CreateWindowDocument("Generate", 2);
            document.Pages[0].Selected = false;
            document.Pages[1].Selected = false;

            var generationDocument = DesignImporterWindow.CreateSinglePageDocument(document, document.Pages[1]);

            Assert.AreEqual(1, generationDocument.Pages.Count);
            Assert.AreSame(document.Pages[1].Root, generationDocument.Pages[0].Root);
            Assert.AreEqual(document.Pages[1].Id, generationDocument.Pages[0].Id);
            Assert.IsTrue(generationDocument.Pages[0].Selected);
            Assert.IsFalse(document.Pages[0].Selected);
            Assert.IsFalse(document.Pages[1].Selected);
        }

        [Test]
        public void ImporterWindow_AnchorPresetWritesAllTransformAnchorValues()
        {
            var page = CreateMappingPage();
            var node = page.Root.Children.Single(x => x.Id == "left");
            var window = ScriptableObject.CreateInstance<DesignImporterWindow>();
            try
            {
                var type = typeof(DesignImporterWindow);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                type.GetField("m_SelectedPage", flags)?.SetValue(window, page);
                type.GetField("m_SelectedNode", flags)?.SetValue(window, node);
                var applyPreset = type.GetMethod("ApplyAnchorPreset", flags);
                Assert.NotNull(applyPreset);

                applyPreset.Invoke(window, new object[]
                {
                    new AnchorPreset(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f))
                });

                Assert.AreEqual(Vector2.zero, node.AnchorMin.Value);
                Assert.AreEqual(Vector2.one, node.AnchorMax.Value);
                Assert.AreEqual(new Vector2(0.5f, 0.5f), node.Pivot.Value);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void AnchorPresetElement_GridContainsAllPointAndStretchCombinations()
        {
            var topLeft = AnchorPresetElement.GetPresetForCell(0, 0);
            Assert.AreEqual(new Vector2(0f, 1f), topLeft.AnchorMin);
            Assert.AreEqual(topLeft.AnchorMin, topLeft.AnchorMax);
            Assert.AreEqual(topLeft.AnchorMin, topLeft.Pivot);

            var horizontalStretch = AnchorPresetElement.GetPresetForCell(3, 0);
            Assert.AreEqual(new Vector2(0f, 1f), horizontalStretch.AnchorMin);
            Assert.AreEqual(Vector2.one, horizontalStretch.AnchorMax);
            Assert.AreEqual(new Vector2(0.5f, 1f), horizontalStretch.Pivot);

            var verticalStretch = AnchorPresetElement.GetPresetForCell(0, 3);
            Assert.AreEqual(Vector2.zero, verticalStretch.AnchorMin);
            Assert.AreEqual(new Vector2(0f, 1f), verticalStretch.AnchorMax);
            Assert.AreEqual(new Vector2(0f, 0.5f), verticalStretch.Pivot);

            var fullStretch = AnchorPresetElement.GetPresetForCell(3, 3);
            Assert.AreEqual(Vector2.zero, fullStretch.AnchorMin);
            Assert.AreEqual(Vector2.one, fullStretch.AnchorMax);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), fullStretch.Pivot);

            var uniquePresets = Enumerable.Range(0, 4)
                .SelectMany(row => Enumerable.Range(0, 4)
                    .Select(column => AnchorPresetElement.GetPresetForCell(column, row)))
                .Select(preset => $"{preset.AnchorMin.x},{preset.AnchorMin.y}:{preset.AnchorMax.x},{preset.AnchorMax.y}")
                .Distinct()
                .Count();
            Assert.AreEqual(16, uniquePresets);
            Assert.Throws<ArgumentOutOfRangeException>(() => AnchorPresetElement.GetPresetForCell(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => AnchorPresetElement.GetPresetForCell(0, 4));
        }

        [UnityTest]
        public IEnumerator ImporterWindow_InspectorContentStaysInsideViewport()
        {
            var window = ScriptableObject.CreateInstance<DesignImporterWindow>();
            try
            {
                window.minSize = new Vector2(1160f, 700f);
                window.position = new Rect(0f, 0f, 1160f, 700f);
                window.Show();
                yield return null;
                var type = typeof(DesignImporterWindow);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var document = CreateWindowDocument("Inspector Layout", 1);
                var page = document.Pages[0];
                page.Root.Kind = DesignNodeKind.Text;
                page.Root.Text = "Inspector content visibility test";
                page.Root.FontName = "STZhongsong";
                page.Root.FontStyleName = "Regular";
                page.Root.FontSize = 36.9245f;
                page.Root.Tracking = 40f;
                type.GetMethod("SetDocument", flags)?.Invoke(window, new object[] { document });
                type.GetMethod("EnterPageEditor", flags)?.Invoke(window, new object[] { page });
                type.GetMethod("SelectNode", flags)?.Invoke(window, new object[] { page.Root, true });
                yield return null;
                yield return null;

                var inspector = window.rootVisualElement.Q<ScrollView>("inspector-pane");
                Assert.NotNull(inspector);
                var viewport = inspector.contentViewport.worldBound;
                var content = inspector.contentContainer.worldBound;
                Assert.LessOrEqual(content.width, viewport.width + 0.5f);
                Assert.AreEqual(ScrollerVisibility.Hidden, inspector.horizontalScrollerVisibility);
                Assert.AreEqual(DisplayStyle.None, inspector.horizontalScroller.resolvedStyle.display);

                var overflowing = inspector.contentContainer.Query<VisualElement>().ToList()
                    .Where(element => element.resolvedStyle.display != DisplayStyle.None && element.worldBound.width > 0.01f)
                    .Where(element => element.worldBound.xMin < viewport.xMin - 0.75f || element.worldBound.xMax > viewport.xMax + 0.75f)
                    .ToArray();
                Assert.IsEmpty(overflowing, "Inspector 中存在超出水平视口的可见控件。" );
                Assert.Greater(window.rootVisualElement.Q<FloatField>("anchor-min-x").worldBound.width, 45f);
                Assert.AreNotEqual(DisplayStyle.None, inspector.verticalScroller.resolvedStyle.display);
                Assert.Greater(
                    Mathf.Abs(inspector.verticalScroller.highValue - inspector.verticalScroller.lowValue),
                    0.5f,
                    "Inspector 没有可用的纵向滚动范围。" );

                var visibleChildren = inspector.contentContainer.Children()
                    .Where(element => element.resolvedStyle.display != DisplayStyle.None && element.worldBound.height > 0.01f)
                    .OrderBy(element => element.worldBound.yMin)
                    .ToArray();
                for (var i = 1; i < visibleChildren.Length; i++)
                {
                    Assert.LessOrEqual(
                        visibleChildren[i - 1].worldBound.yMax,
                        visibleChildren[i].worldBound.yMin + 0.75f,
                        $"Inspector 区块发生垂直重叠: {visibleChildren[i - 1].name} -> {visibleChildren[i].name}");
                }

                var sectionTitles = inspector.Query<Label>(className: "section-title").ToList();
                var interactionTitle = sectionTitles.Single(label => label.text == "交互与绑定");
                var generationTitle = sectionTitles.Single(label => label.text == "生成");
                Assert.LessOrEqual(
                    inspector.Q<VisualElement>("anchor-editor").worldBound.yMax,
                    interactionTitle.worldBound.yMin + 0.75f,
                    "锚点编辑器与交互区块发生垂直重叠。" );

                var textInspector = (VisualElement)type.GetField("m_TextInspector", flags)?.GetValue(window);
                Assert.NotNull(textInspector);
                AssertSequentialLayout(textInspector, "文本 Inspector");
                Assert.LessOrEqual(
                    textInspector.worldBound.yMax,
                    generationTitle.worldBound.yMin + 0.75f,
                    "文本 Inspector 与生成区块发生垂直重叠。" );
            }
            finally
            {
                window.Close();
            }
        }

        [UnityTest]
        public IEnumerator LanhuSyncBridge_RoundTripsManifestOnLoopback()
        {
            using var server = new LanhuSyncBridgeServer();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var address = LanhuProjectAddress.Parse(
                "https://lanhuapp.com/web/#/item/project/stage?pid=project-bridge&tid=team-bridge");
            var pending = server.RequestManifestAsync(address, cancellation.Token);
            using var client = new HttpClient();
            var jobRequest = client.GetStringAsync("http://127.0.0.1:18766/gdk-lanhu/jobs/next");
            while (!jobRequest.IsCompleted) yield return null;
            var jobJson = jobRequest.GetAwaiter().GetResult();
            var jobId = (string)Newtonsoft.Json.Linq.JObject.Parse(jobJson)["jobId"];
            var payload = "{\"jobId\":\"" + jobId + "\",\"manifest\":{\"schemaVersion\":\"1.0\",\"name\":\"Bridge\"}}";
            var postRequest = client.PostAsync(
                "http://127.0.0.1:18766/gdk-lanhu/complete",
                new StringContent(payload, Encoding.UTF8, "application/json"));
            while (!postRequest.IsCompleted) yield return null;
            var response = postRequest.GetAwaiter().GetResult();

            Assert.IsTrue(response.IsSuccessStatusCode);
            while (!pending.IsCompleted) yield return null;
            StringAssert.Contains("Bridge", pending.GetAwaiter().GetResult());
        }

        [Test]
        public void LayoutUtility_FitCentersLetterboxedContent()
        {
            var viewport = DesignLayoutUtility.CalculateViewport(
                new Vector2(1920f, 1080f),
                new Vector2(1080f, 1920f),
                DesignScaleMode.Fit);

            Assert.AreEqual(0.5625f, viewport.Scale.x, 0.0001f);
            Assert.AreEqual(viewport.Scale.x, viewport.Scale.y, 0.0001f);
            Assert.AreEqual(0f, viewport.Offset.x, 0.0001f);
            Assert.AreEqual(656.25f, viewport.Offset.y, 0.0001f);
        }

        [Test]
        public void LayoutUtility_StretchUsesIndependentAxes()
        {
            var viewport = DesignLayoutUtility.CalculateViewport(
                new Vector2(100f, 200f),
                new Vector2(300f, 400f),
                DesignScaleMode.Stretch);

            Assert.AreEqual(new Vector2(3f, 2f), viewport.Scale);
            Assert.AreEqual(Vector2.zero, viewport.Offset);
        }

        [Test]
        public void PathUtility_RejectsOutputOutsideAssets()
        {
            Assert.Throws<ArgumentException>(() => DesignPathUtility.EnsureAssetsPath("Packages/Generated"));
            Assert.Throws<ArgumentException>(() => DesignPathUtility.EnsureAssetsPath("Assets/../Outside"));
            Assert.Throws<ArgumentException>(() => DesignPathUtility.EnsureAssetsPath("Assets//Generated"));
            Assert.AreEqual("Assets/UI/Generated", DesignPathUtility.EnsureAssetsPath("Assets\\UI\\Generated\\"));
        }

        [UnityTest]
        public IEnumerator ImporterWindow_ReplacingDocumentResetsListSelection()
        {
            var window = ScriptableObject.CreateInstance<DesignImporterWindow>();
            try
            {
                window.Show();
                yield return null;
                var type = typeof(DesignImporterWindow);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var setDocument = type.GetMethod("SetDocument", flags);
                var pageList = (ListView)type.GetField("m_PageList", flags)?.GetValue(window);
                var selectedPage = type.GetField("m_SelectedPage", flags);
                Assert.NotNull(setDocument);
                Assert.NotNull(pageList);
                Assert.NotNull(selectedPage);

                var first = CreateWindowDocument("First", 70);
                setDocument.Invoke(window, new object[] { first });
                for (var i = 0; i < 2; i++)
                {
                    yield return null;
                }

                pageList.SetSelection(59);
                yield return null;
                Assert.AreEqual(59, pageList.selectedIndex);

                var second = CreateWindowDocument("Second", 70);
                setDocument.Invoke(window, new object[] { second });
                for (var i = 0; i < 5; i++)
                {
                    yield return null;
                }

                Assert.AreEqual(0, pageList.selectedIndex);
                Assert.AreSame(second.Pages[0], selectedPage.GetValue(window));
            }
            finally
            {
                window.Close();
            }
        }

        [UnityTest]
        public IEnumerator ImporterWindow_PageAndLayerBrowsersAreMutuallyExclusive()
        {
            var window = ScriptableObject.CreateInstance<DesignImporterWindow>();
            try
            {
                window.Show();
                yield return null;
                var type = typeof(DesignImporterWindow);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var setDocument = type.GetMethod("SetDocument", flags);
                var enterPageEditor = type.GetMethod("EnterPageEditor", flags);
                var showPageBrowser = type.GetMethod("ShowPageBrowser", flags);
                var pageBrowser = (VisualElement)type.GetField("m_PageBrowserView", flags)?.GetValue(window);
                var layerBrowser = (VisualElement)type.GetField("m_LayerBrowserView", flags)?.GetValue(window);
                var breadcrumbPage = (Label)type.GetField("m_BreadcrumbPage", flags)?.GetValue(window);
                var document = CreateWindowDocument("Navigation", 2);

                Assert.NotNull(setDocument);
                Assert.NotNull(enterPageEditor);
                Assert.NotNull(showPageBrowser);
                Assert.NotNull(pageBrowser);
                Assert.NotNull(layerBrowser);
                setDocument.Invoke(window, new object[] { document });
                yield return null;

                Assert.AreEqual(DisplayStyle.Flex, pageBrowser.style.display.value);
                Assert.AreEqual(DisplayStyle.None, layerBrowser.style.display.value);

                enterPageEditor.Invoke(window, new object[] { document.Pages[1] });
                Assert.AreEqual(DisplayStyle.None, pageBrowser.style.display.value);
                Assert.AreEqual(DisplayStyle.Flex, layerBrowser.style.display.value);
                Assert.AreEqual(document.Pages[1].Name, breadcrumbPage.text);

                showPageBrowser.Invoke(window, null);
                Assert.AreEqual(DisplayStyle.Flex, pageBrowser.style.display.value);
                Assert.AreEqual(DisplayStyle.None, layerBrowser.style.display.value);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void PreviewFallbackComposer_RemovesEditableImageAndTextLayers()
        {
            const int width = 16;
            const int height = 8;
            var previewPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + "-preview.png");
            var imagePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + "-image.png");
            var background = new Color32(23, 47, 71, 255);
            try
            {
                var previewPixels = Enumerable.Repeat(background, width * height).ToArray();
                for (var y = 2; y < 6; y++)
                {
                    for (var x = 4; x < 8; x++)
                    {
                        previewPixels[(height - 1 - y) * width + x] = new Color32(220, 30, 40, 255);
                    }
                }

                for (var y = 2; y < 5; y++)
                {
                    for (var x = 10; x < 13; x++)
                    {
                        previewPixels[(height - 1 - y) * width + x] = new Color32(240, 240, 240, 255);
                    }
                }

                var previewTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var imageTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                try
                {
                    previewTexture.SetPixels32(previewPixels);
                    previewTexture.Apply(false, false);
                    System.IO.File.WriteAllBytes(previewPath, previewTexture.EncodeToPNG());

                    imageTexture.SetPixels32(Enumerable.Repeat(
                        new Color32(220, 30, 40, 255),
                        16).ToArray());
                    imageTexture.Apply(false, false);
                    System.IO.File.WriteAllBytes(imagePath, imageTexture.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(previewTexture);
                    UnityEngine.Object.DestroyImmediate(imageTexture);
                }

                var page = new DesignPage
                {
                    Id = "fallback-page",
                    Name = "Fallback Page",
                    Width = width,
                    Height = height,
                    CachedPreviewPath = previewPath,
                    Root = new DesignNode
                    {
                        Id = "root",
                        Name = "Root",
                        Kind = DesignNodeKind.Container,
                        Width = width,
                        Height = height,
                        Children =
                        {
                            new DesignNode
                            {
                                Id = "image",
                                Name = "Editable Image",
                                Kind = DesignNodeKind.Image,
                                X = 4f,
                                Y = 2f,
                                Width = 4f,
                                Height = 4f,
                                AssetId = "asset"
                            },
                            new DesignNode
                            {
                                Id = "text",
                                Name = "Editable Text",
                                Kind = DesignNodeKind.Text,
                                X = 10f,
                                Y = 2f,
                                Width = 3f,
                                Height = 3f,
                                Text = "T"
                            }
                        }
                    }
                };
                var assets = new System.Collections.Generic.Dictionary<string, DesignAsset>
                {
                    ["asset"] = new DesignAsset
                    {
                        Id = "asset",
                        Name = "Editable Image",
                        CachedFilePath = imagePath
                    }
                };

                var composedBytes = DesignPreviewFallbackComposer.Compose(page, assets);
                var composed = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.IsTrue(composed.LoadImage(composedBytes, false));
                    Assert.AreEqual(width, composed.width);
                    Assert.AreEqual(height, composed.height);
                    foreach (var pixel in composed.GetPixels32())
                    {
                        Assert.That(pixel.r, Is.EqualTo(background.r).Within(1));
                        Assert.That(pixel.g, Is.EqualTo(background.g).Within(1));
                        Assert.That(pixel.b, Is.EqualTo(background.b).Within(1));
                        Assert.AreEqual(255, pixel.a);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(composed);
                }
            }
            finally
            {
                System.IO.File.Delete(previewPath);
                System.IO.File.Delete(imagePath);
            }
        }

        [Test]
        public void ImportPipeline_PageWithoutImages_GeneratesPrefab()
        {
            var outputRoot = "Assets/__GameDeveloperKitDesignImporterTests_" + Guid.NewGuid().ToString("N");
            var activeScene = EditorSceneManager.GetActiveScene();
            var sceneWasDirty = activeScene.isDirty;
            var previewSceneCount = EditorSceneManager.previewSceneCount;
            try
            {
                var document = new DesignDocument
                {
                    Id = "text-only",
                    Name = "Text Only",
                    Pages =
                    {
                        new DesignPage
                        {
                            Id = "page",
                            Name = "Text Page",
                            Width = 320f,
                            Height = 180f,
                            Root = new DesignNode
                            {
                                Id = "root",
                                Name = "Root",
                                Kind = DesignNodeKind.Container,
                                Width = 320f,
                                Height = 180f,
                                Children =
                                {
                                    new DesignNode
                                    {
                                        Id = "panel",
                                        Name = "Panel",
                                        Kind = DesignNodeKind.Container,
                                        X = 40f,
                                        Y = 30f,
                                        Width = 240f,
                                        Height = 120f,
                                        Children =
                                        {
                                            new DesignNode
                                            {
                                                Id = "title",
                                                Name = "Title",
                                                Kind = DesignNodeKind.Text,
                                                X = 20f,
                                                Y = 20f,
                                                Width = 200f,
                                                Height = 40f,
                                                Text = "Hello"
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };
                document.Normalize();

                var report = new DesignImportPipeline().ImportAsync(
                        document,
                        new DesignImportOptions
                        {
                            OutputRoot = outputRoot,
                            TargetResolution = new Vector2Int(320, 180),
                            IncludeCanvas = false,
                            GenerateWindowCode = false
                        },
                        null,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                Assert.AreEqual(1, report.PrefabPaths.Count);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(report.PrefabPaths[0]);
                Assert.NotNull(prefab);
                var panel = prefab.transform.Find("Design/Root/Panel") as RectTransform;
                var title = prefab.transform.Find("Design/Root/Panel/Title") as RectTransform;
                Assert.NotNull(panel, "分组应保留为嵌套 RectTransform。" );
                Assert.NotNull(title, "文本图层应保留在所属分组下。" );
                Assert.AreEqual(new Vector2(0.5f, 0.5f), panel.anchorMin);
                Assert.AreEqual(panel.anchorMin, panel.anchorMax);
                Assert.AreEqual(Vector2.zero, panel.anchoredPosition);
                Assert.AreEqual(new Vector2(0.5f, 0.5f), panel.pivot);
                Assert.AreEqual(new Vector2(240f, 120f), panel.sizeDelta);
                Assert.AreEqual(Vector2.zero, title.anchoredPosition);
                Assert.AreEqual(new Vector2(200f, 40f), title.sizeDelta);
                var titleText = title.GetComponent("TextMeshProUGUI");
                Assert.NotNull(titleText);
                var wrappingProperty = titleText.GetType().GetProperty("enableWordWrapping");
                Assert.NotNull(wrappingProperty);
                Assert.IsFalse(
                    (bool)wrappingProperty.GetValue(titleText),
                    "单行设计文本不应因为源清单的默认值而自动换行。");
                Assert.AreEqual(sceneWasDirty, activeScene.isDirty, "Prefab 生成不应修改当前场景的 Dirty 状态。");
                Assert.AreEqual(previewSceneCount, EditorSceneManager.previewSceneCount, "Prefab 生成不应遗留 Preview Scene。");
            }
            finally
            {
                AssetDatabase.DeleteAsset(outputRoot);
            }
        }

        [Test]
        public void ImportPipeline_InteractiveLayers_CreateControlsUidocumentAndWindowBindings()
        {
            var outputRoot = "Assets/__GameDeveloperKitDesignImporterControls_" + Guid.NewGuid().ToString("N");
            try
            {
                var pageName = "ImportedControls";
                var document = new DesignDocument
                {
                    Id = "interactive-document",
                    Name = pageName,
                    Pages =
                    {
                        new DesignPage
                        {
                            Id = "interactive-page",
                            Name = pageName,
                            Width = 400f,
                            Height = 240f,
                            Root = new DesignNode
                            {
                                Id = "root",
                                Name = "Root",
                                Kind = DesignNodeKind.Container,
                                Width = 400f,
                                Height = 240f,
                                Children =
                                {
                                    CreateInteractiveNode("button", "Submit", DesignComponentKind.Button, "b_btn_submit", 20f),
                                    CreateInteractiveNode("toggle", "Sound", DesignComponentKind.Toggle, "b_toggle_sound", 70f),
                                    CreateInteractiveNode("slider", "Volume", DesignComponentKind.Slider, "b_slider_volume", 120f),
                                    CreateInteractiveNode("input", "Name", DesignComponentKind.InputField, "b_input_name", 170f),
                                    new DesignNode
                                    {
                                        Id = "scroll",
                                        Name = "History",
                                        Kind = DesignNodeKind.Container,
                                        X = 240f,
                                        Y = 20f,
                                        Width = 140f,
                                        Height = 180f,
                                        BackgroundColor = "#FFFFFFFF",
                                        Component = DesignComponentKind.ScrollRect,
                                        BindingName = "b_scroll_history",
                                        ScrollHorizontal = false,
                                        ScrollVertical = true,
                                        Children =
                                        {
                                            new DesignNode
                                            {
                                                Id = "history-text",
                                                Name = "History Text",
                                                Kind = DesignNodeKind.Text,
                                                X = 8f,
                                                Y = 8f,
                                                Width = 120f,
                                                Height = 30f,
                                                Text = "History"
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };
                document.Normalize();

                var report = new DesignImportPipeline().ImportAsync(
                        document,
                        new DesignImportOptions
                        {
                            OutputRoot = outputRoot,
                            GeneratedCodeRoot = outputRoot + "/Code",
                            CodeNamespace = "GameDeveloperKit.UI.Generated",
                            TargetResolution = new Vector2Int(400, 240),
                            IncludeCanvas = false,
                            GenerateWindowCode = true,
                            LayerOrder = 300,
                            CacheEnabled = false
                        },
                        null,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(report.PrefabPaths.Single());
                Assert.NotNull(prefab);
                var uiDocument = prefab.GetComponent<GameDeveloperKit.UI.UIDocument>();
                Assert.NotNull(uiDocument);
                Assert.AreEqual(prefab.transform, uiDocument.FullScreenRoot);
                Assert.AreEqual(300, uiDocument.Layer.Order);
                Assert.IsFalse(uiDocument.CacheEnabled);
                Assert.AreEqual("GameDeveloperKit.UI.Generated", uiDocument.CodeNamespace);
                Assert.AreEqual(5, uiDocument.Mappings.Count);
                Assert.NotNull(prefab.transform.Find("Design/Root/Submit").GetComponent<UGUIButton>());
                Assert.NotNull(prefab.transform.Find("Design/Root/Sound").GetComponent<UGUIToggle>());
                Assert.NotNull(prefab.transform.Find("Design/Root/Volume").GetComponent<UGUISlider>());
                Assert.NotNull(prefab.transform.Find("Design/Root/Name").GetComponent("TMP_InputField"));
                Assert.NotNull(prefab.transform.Find("Design/Root/History").GetComponent<UGUIScrollRect>());
                Assert.IsTrue(AssetDatabase.LoadAssetAtPath<MonoScript>(
                    outputRoot + "/Code/" + pageName + "/" + pageName + "Window.Design.g.cs") != null);
            }
            finally
            {
                AssetDatabase.DeleteAsset(outputRoot);
            }
        }

        private static DesignDocument CreateWindowDocument(string name, int pageCount)
        {
            var document = new DesignDocument
            {
                Id = name,
                Name = name
            };
            for (var i = 0; i < pageCount; i++)
            {
                document.Pages.Add(new DesignPage
                {
                    Id = name + "-" + i,
                    Name = "Page " + i,
                    Width = 480f,
                    Height = 270f,
                    Root = new DesignNode
                    {
                        Id = name + "-root-" + i,
                        Name = "Root " + i,
                        Width = 480f,
                        Height = 270f
                    }
                });
            }

            document.Normalize();
            return document;
        }

        private static DesignDocument CreateDiffDocument(params (string id, string text)[] pages)
        {
            var document = new DesignDocument { Id = "diff", Name = "Diff" };
            foreach (var item in pages)
            {
                document.Pages.Add(new DesignPage
                {
                    Id = item.id,
                    Name = item.id,
                    Width = 100f,
                    Height = 100f,
                    Root = new DesignNode
                    {
                        Id = item.id + "-root",
                        Name = item.id,
                        Width = 100f,
                        Height = 100f,
                        Children =
                        {
                            new DesignNode
                            {
                                Id = item.id + "-text",
                                Name = "Text",
                                Kind = DesignNodeKind.Text,
                                Width = 80f,
                                Height = 20f,
                                Text = item.text
                            }
                        }
                    }
                });
            }

            document.Normalize();
            return document;
        }

        private static DesignPage CreateMappingPage()
        {
            var page = new DesignPage
            {
                Id = "mapping-page",
                Name = "Mapping",
                Width = 200f,
                Height = 100f,
                Root = new DesignNode
                {
                    Id = "root",
                    Name = "Root",
                    Width = 200f,
                    Height = 100f,
                    Children =
                    {
                        new DesignNode
                        {
                            Id = "left",
                            Name = "Left",
                            Width = 100f,
                            Height = 100f,
                            Children =
                            {
                                new DesignNode
                                {
                                    Id = "layer",
                                    Name = "Layer",
                                    Kind = DesignNodeKind.Text,
                                    Width = 20f,
                                    Height = 20f
                                }
                            }
                        },
                        new DesignNode
                        {
                            Id = "right",
                            Name = "Right",
                            Kind = DesignNodeKind.Container,
                            X = 100f,
                            Width = 100f,
                            Height = 100f
                        }
                    }
                }
            };
            page.Normalize(0);
            return page;
        }

        private static DesignNode CreateInteractiveNode(
            string id,
            string name,
            DesignComponentKind component,
            string bindingName,
            float y)
        {
            return new DesignNode
            {
                Id = id,
                Name = name,
                Kind = component == DesignComponentKind.InputField ? DesignNodeKind.Text : DesignNodeKind.Container,
                X = 20f,
                Y = y,
                Width = 180f,
                Height = 34f,
                BackgroundColor = "#FFFFFFFF",
                Text = component == DesignComponentKind.InputField ? "Name" : string.Empty,
                Component = component,
                BindingName = bindingName,
                Interactable = true,
                ToggleValue = true,
                SliderMinValue = 10f,
                SliderMaxValue = 50f,
                SliderValue = 25f,
                SliderWholeNumbers = true
            };
        }

        private static void AssertSequentialLayout(VisualElement parent, string description)
        {
            var children = parent.Children()
                .Where(element => element.resolvedStyle.display != DisplayStyle.None && element.worldBound.height > 0.01f)
                .OrderBy(element => element.worldBound.yMin)
                .ToArray();
            for (var i = 1; i < children.Length; i++)
            {
                Assert.LessOrEqual(
                    children[i - 1].worldBound.yMax,
                    children[i].worldBound.yMin + 0.75f,
                    $"{description} 内部控件发生垂直重叠: {children[i - 1].name} -> {children[i].name}");
            }
        }
    }
}
