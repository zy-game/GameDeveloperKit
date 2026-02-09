using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 构建报告视图 - 嵌入在ResourcePackagesWindow中
    /// </summary>
    public class BuildReportView
    {
        private VisualElement _container;
        private VisualElement _root;
        private AssetBundleBuilder.BuildReport _report;

        private VisualElement _content;
        private VisualElement _buildInfoContent;
        private VisualElement _bundleListContent;
        private TextField _searchField;

        /// <summary>
        /// 初始化报告视图
        /// </summary>
        public void Initialize(VisualElement container, VisualElement root)
        {
            _container = container;
            _root = root;

            // 创建简单的报告UI
            _content = new VisualElement();
            _content.style.paddingTop = 16;
            _content.style.paddingBottom = 16;
            _content.style.paddingLeft = 16;
            _content.style.paddingRight = 16;
            _container.Add(_content);

            // 创建构建信息卡片
            var buildInfoCard = CreateCard("构建信息");
            _buildInfoContent = new VisualElement();
            buildInfoCard.Add(_buildInfoContent);
            _content.Add(buildInfoCard);

            // 创建Bundle列表卡片
            var bundleListCard = CreateCard("Bundle 列表");
            
            // 搜索框
            _searchField = new TextField();
            _searchField.AddToClassList("custom-textfield");
            _searchField.style.marginBottom = 12;
            _searchField.RegisterValueChangedCallback(evt => RefreshBundleList());
            bundleListCard.Add(_searchField);

            _bundleListContent = new VisualElement();
            bundleListCard.Add(_bundleListContent);
            _content.Add(bundleListCard);
        }

        /// <summary>
        /// 设置报告数据并刷新UI
        /// </summary>
        public void SetReport(AssetBundleBuilder.BuildReport report)
        {
            _report = report;
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (_report == null) return;

            RefreshBuildInfo();
            RefreshBundleList();
        }

        private void RefreshBuildInfo()
        {
            if (_buildInfoContent == null) return;
            _buildInfoContent.Clear();

            if (_report == null)
            {
                _buildInfoContent.Add(new Label("无构建报告数据"));
                return;
            }

            // 构建状态
            var statusField = CreateInfoRow("状态", _report.success ? "✓ 成功" : "✗ 失败");
            _buildInfoContent.Add(statusField);

            // Bundle数量
            var bundleCount = _report.bundleSizes != null ? _report.bundleSizes.Count : 0;
            var bundleField = CreateInfoRow("Bundle 数量", bundleCount.ToString());
            _buildInfoContent.Add(bundleField);

            // 资源数量
            var assetField = CreateInfoRow("资源数量", _report.totalAssets.ToString());
            _buildInfoContent.Add(assetField);

            // 总大小
            var sizeField = CreateInfoRow("总大小", FormatBytes(_report.totalSize));
            _buildInfoContent.Add(sizeField);

            // 构建时间
            var timeField = CreateInfoRow("构建时间", $"{_report.buildTime:F2}s");
            _buildInfoContent.Add(timeField);

            // 输出路径
            if (!string.IsNullOrEmpty(_report.outputPath))
            {
                var pathRow = new VisualElement();
                pathRow.style.flexDirection = FlexDirection.Row;
                pathRow.style.alignItems = Align.Center;
                pathRow.style.marginBottom = 8;

                var pathLabel = new Label("输出路径:");
                pathLabel.style.width = 100;
                pathLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                pathRow.Add(pathLabel);

                var pathValue = new Label(_report.outputPath);
                pathValue.style.flexGrow = 1;
                pathValue.style.color = new Color(0.9f, 0.9f, 0.9f);
                pathRow.Add(pathValue);

                var openButton = new Button(() => EditorUtility.RevealInFinder(_report.outputPath));
                openButton.text = "打开";
                openButton.AddToClassList("btn");
                openButton.style.minWidth = 60;
                openButton.style.height = 22;
                pathRow.Add(openButton);

                _buildInfoContent.Add(pathRow);
            }
        }

        private void RefreshBundleList()
        {
            if (_bundleListContent == null) return;
            _bundleListContent.Clear();

            if (_report == null || _report.bundleSizes == null)
            {
                _bundleListContent.Add(new Label("无 Bundle 数据"));
                return;
            }

            var searchText = _searchField?.value?.ToLower() ?? "";
            var filteredBundles = _report.bundleSizes
                .Where(kvp => string.IsNullOrEmpty(searchText) || kvp.Key.ToLower().Contains(searchText))
                .OrderByDescending(kvp => kvp.Value)
                .ToList();

            if (filteredBundles.Count == 0)
            {
                _bundleListContent.Add(new Label("无匹配的 Bundle"));
                return;
            }

            // 表格头
            var header = CreateTableHeader();
            _bundleListContent.Add(header);

            // Bundle行（可展开显示资源列表）
            foreach (var bundle in filteredBundles)
            {
                var bundleContainer = new VisualElement();
                
                var row = CreateBundleRow(bundle.Key, bundle.Value);
                bundleContainer.Add(row);
                
                // 资源列表（可折叠）
                var assetList = new VisualElement();
                assetList.style.display = DisplayStyle.None;
                assetList.style.marginLeft = 20;
                assetList.style.marginBottom = 8;
                assetList.style.paddingLeft = 8;
                assetList.style.borderLeftWidth = 2;
                assetList.style.borderLeftColor = new Color(0.3f, 0.5f, 0.8f, 0.5f);
                
                // 获取 Bundle 中的资源
                if (_report.bundleAssets != null && _report.bundleAssets.TryGetValue(bundle.Key, out var assets))
                {
                    foreach (var assetPath in assets)
                    {
                        var assetLabel = new Label("  " + assetPath);
                        assetLabel.style.fontSize = 11;
                        assetLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                        assetLabel.style.marginBottom = 2;
                        assetList.Add(assetLabel);
                    }
                }
                else
                {
                    var noAssetsLabel = new Label("  (无资源信息)");
                    noAssetsLabel.style.fontSize = 11;
                    noAssetsLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                    assetList.Add(noAssetsLabel);
                }
                
                bundleContainer.Add(assetList);
                
                // 点击展开/折叠
                row.RegisterCallback<ClickEvent>(evt =>
                {
                    assetList.style.display = assetList.style.display == DisplayStyle.None 
                        ? DisplayStyle.Flex 
                        : DisplayStyle.None;
                });
                
                _bundleListContent.Add(bundleContainer);
            }

            // 总计
            var totalSize = filteredBundles.Sum(kvp => kvp.Value);
            var totalRow = CreateTotalRow(filteredBundles.Count, totalSize);
            _bundleListContent.Add(totalRow);
        }

        private VisualElement CreateCard(string title)
        {
            var card = new VisualElement();
            card.AddToClassList("info-card");
            card.style.marginBottom = 16;
            card.style.paddingTop = 16;
            card.style.paddingBottom = 16;
            card.style.paddingLeft = 16;
            card.style.paddingRight = 16;
            card.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("card-title");
            titleLabel.style.fontSize = 16;
            titleLabel.style.marginBottom = 12;
            titleLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            card.Add(titleLabel);

            return card;
        }

        private VisualElement CreateInfoRow(string label, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 8;

            var labelElement = new Label(label + ":");
            labelElement.style.width = 100;
            labelElement.style.color = new Color(0.7f, 0.7f, 0.7f);
            row.Add(labelElement);

            var valueElement = new Label(value);
            valueElement.style.color = new Color(0.9f, 0.9f, 0.9f);
            row.Add(valueElement);

            return row;
        }

        private VisualElement CreateTableHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
            header.style.marginBottom = 4;

            var nameLabel = new Label("Bundle 名称");
            nameLabel.style.flexGrow = 1;
            nameLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(nameLabel);

            var sizeLabel = new Label("大小");
            sizeLabel.style.width = 120;
            sizeLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            sizeLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            sizeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(sizeLabel);

            return header;
        }

        private VisualElement CreateBundleRow(string bundleName, long size)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;
            row.style.marginBottom = 2;

            // 悬停效果
            row.RegisterCallback<MouseEnterEvent>(evt =>
            {
                row.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
            });
            row.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                row.style.backgroundColor = Color.clear;
            });

            var nameLabel = new Label(bundleName);
            nameLabel.style.flexGrow = 1;
            nameLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            row.Add(nameLabel);

            var sizeLabel = new Label(FormatBytes(size));
            sizeLabel.style.width = 120;
            sizeLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            sizeLabel.style.color = new Color(0.7f, 0.9f, 1f);
            row.Add(sizeLabel);

            return row;
        }

        private VisualElement CreateTotalRow(int count, long totalSize)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.paddingTop = 12;
            row.style.paddingBottom = 8;
            row.style.borderTopWidth = 1;
            row.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
            row.style.marginTop = 8;

            var totalLabel = new Label($"总计 ({count} 个 Bundle):");
            totalLabel.style.flexGrow = 1;
            totalLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            totalLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(totalLabel);

            var sizeLabel = new Label(FormatBytes(totalSize));
            sizeLabel.style.width = 120;
            sizeLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            sizeLabel.style.color = new Color(0.5f, 1f, 0.5f);
            sizeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(sizeLabel);

            return row;
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F2} KB";
            if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F2} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }
}
