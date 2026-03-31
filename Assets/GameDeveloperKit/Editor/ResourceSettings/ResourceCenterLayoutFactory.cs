using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor
{
    internal sealed class ResourceCenterLayoutResult
    {
        public VisualElement PackageListHost { get; set; }

        public VisualElement DetailHost { get; set; }

        public VisualElement SettingsView { get; set; }

        public VisualElement EmptyState { get; set; }

        public VisualElement PlayModeHost { get; set; }

        public VisualElement RemoteUrlHost { get; set; }

        public VisualElement HeaderActionsHost { get; set; }

        public Label DetailTitle { get; set; }

        public Button SettingsButton { get; set; }

        public Button AddPackageButton { get; set; }
    }

    internal static class ResourceCenterLayoutFactory
    {
        public static ResourceCenterLayoutResult Build(
            VisualElement rootVisualElement,
            string commonUssPath,
            string uxmlPath,
            string ussPath)
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1;
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            var commonStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(commonUssPath);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);

            if (visualTree == null)
            {
                return null;
            }

            var root = visualTree.CloneTree();
            root.style.flexGrow = 1;
            root.style.flexDirection = FlexDirection.Column;
            rootVisualElement.Add(root);

            if (commonStyleSheet != null)
            {
                root.styleSheets.Add(commonStyleSheet);
            }

            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            ApplyInlineLayoutAdjustments(root);
            var result = QueryRefs(rootVisualElement);
            return IsComplete(result) ? result : null;
        }

        private static void ApplyInlineLayoutAdjustments(VisualElement root)
        {
            var contentArea = root.Q(null, "content-area");
            if (contentArea != null)
            {
                contentArea.style.flexGrow = 1;
                contentArea.style.flexDirection = FlexDirection.Row;
            }

            var leftPanel = root.Q(null, "left-panel");
            if (leftPanel != null)
            {
                leftPanel.style.width = 260;
                leftPanel.style.minWidth = 200;
                leftPanel.style.flexShrink = 0;
                leftPanel.style.flexDirection = FlexDirection.Column;
            }

            var rightPanel = root.Q(null, "right-panel");
            if (rightPanel != null)
            {
                rightPanel.style.flexGrow = 1;
                rightPanel.style.flexDirection = FlexDirection.Column;
            }

            var packageScroll = root.Q<ScrollView>("package-scroll");
            if (packageScroll != null)
            {
                packageScroll.style.flexGrow = 1;
            }

            var detailScroll = root.Q<ScrollView>("detail-scroll");
            if (detailScroll != null)
            {
                detailScroll.style.flexGrow = 1;
            }
        }

        private static ResourceCenterLayoutResult QueryRefs(VisualElement rootVisualElement)
        {
            return new ResourceCenterLayoutResult
            {
                PackageListHost = rootVisualElement.Q<VisualElement>("package-list-host"),
                DetailHost = rootVisualElement.Q<VisualElement>("detail-host"),
                SettingsView = rootVisualElement.Q<VisualElement>("settings-view"),
                EmptyState = rootVisualElement.Q<VisualElement>("empty-state"),
                PlayModeHost = rootVisualElement.Q<VisualElement>("playmode-host"),
                RemoteUrlHost = rootVisualElement.Q<VisualElement>("remoteurl-host"),
                HeaderActionsHost = rootVisualElement.Q<VisualElement>("header-actions-host"),
                DetailTitle = rootVisualElement.Q<Label>("detail-title"),
                SettingsButton = rootVisualElement.Q<Button>("settings-button"),
                AddPackageButton = rootVisualElement.Q<Button>("add-package-button")
            };
        }

        private static bool IsComplete(ResourceCenterLayoutResult result)
        {
            return result.PackageListHost != null &&
                   result.DetailHost != null &&
                   result.SettingsView != null &&
                   result.EmptyState != null &&
                   result.PlayModeHost != null &&
                   result.RemoteUrlHost != null &&
                   result.HeaderActionsHost != null &&
                   result.DetailTitle != null &&
                   result.SettingsButton != null &&
                   result.AddPackageButton != null;
        }
    }
}
