using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 资源验证结果窗口
    /// </summary>
    public class ValidationResultWindow : EditorWindow
    {
        private ValidationResult _result;
        private string _packageName;
        private ScrollView _scrollView;
        private VisualElement _content;
        
        public static void ShowResult(ValidationResult result, string packageName)
        {
            var window = GetWindow<ValidationResultWindow>("验证结果");
            window._result = result;
            window._packageName = packageName;
            window.minSize = new Vector2(500, 400);
            window.RefreshUI();
        }
        
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            
            _scrollView = new ScrollView(ScrollViewMode.Vertical);
            _scrollView.style.flexGrow = 1;
            root.Add(_scrollView);
            
            _content = new VisualElement();
            _scrollView.Add(_content);
            
            if (_result != null)
            {
                RefreshUI();
            }
        }
        
        private void RefreshUI()
        {
            if (_content == null) return;
            _content.Clear();
            
            if (_result == null)
            {
                _content.Add(new Label("无验证结果"));
                return;
            }
            
            // 标题
            var title = new Label($"验证结果: {_packageName}");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 16;
            _content.Add(title);
            
            // 摘要卡片
            var summaryCard = CreateSummaryCard();
            _content.Add(summaryCard);
            
            // 信息列表
            if (_result.Info.Count > 0)
            {
                var infoSection = CreateSection("信息", _result.Info, new Color(0.3f, 0.6f, 0.9f));
                _content.Add(infoSection);
            }
            
            // 错误列表
            if (_result.Errors.Count > 0)
            {
                var errorSection = CreateErrorSection();
                _content.Add(errorSection);
            }
            
            // 警告列表
            if (_result.Warnings.Count > 0)
            {
                var warningSection = CreateWarningSection();
                _content.Add(warningSection);
            }
            
            // 引用关系图（直接嵌入）
            if (_result.ReferenceGraph.Count > 0)
            {
                var graphSection = CreateGraphSection();
                _content.Add(graphSection);
            }
            
            // 底部按钮
            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 16;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            
            var exportButton = new Button(() => ExportReport());
            exportButton.text = "导出报告";
            exportButton.style.marginRight = 8;
            buttonRow.Add(exportButton);
            
            var closeButton = new Button(() => Close());
            closeButton.text = "关闭";
            buttonRow.Add(closeButton);
            
            _content.Add(buttonRow);
        }
        
        private VisualElement CreateSummaryCard()
        {
            var card = new VisualElement();
            card.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;
            card.style.paddingTop = 16;
            card.style.paddingBottom = 16;
            card.style.paddingLeft = 16;
            card.style.paddingRight = 16;
            card.style.marginBottom = 16;
            
            // 状态图标和文字
            var statusRow = new VisualElement();
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.style.alignItems = Align.Center;
            statusRow.style.marginBottom = 12;
            
            var statusIcon = new Label(_result.IsValid ? "✓" : "✗");
            statusIcon.style.fontSize = 24;
            statusIcon.style.color = _result.IsValid ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.9f, 0.3f, 0.3f);
            statusIcon.style.marginRight = 8;
            statusRow.Add(statusIcon);
            
            var statusText = new Label(_result.IsValid ? "验证通过" : "验证失败");
            statusText.style.fontSize = 16;
            statusText.style.unityFontStyleAndWeight = FontStyle.Bold;
            statusText.style.color = _result.IsValid ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.9f, 0.3f, 0.3f);
            statusRow.Add(statusText);
            
            card.Add(statusRow);
            
            // 统计信息
            var statsRow = new VisualElement();
            statsRow.style.flexDirection = FlexDirection.Row;
            statsRow.style.justifyContent = Justify.SpaceAround;
            
            statsRow.Add(CreateStatItem("资源总数", _result.TotalAssets.ToString(), new Color(0.5f, 0.7f, 1f)));
            statsRow.Add(CreateStatItem("有效资源", _result.ValidAssets.ToString(), new Color(0.3f, 0.8f, 0.3f)));
            statsRow.Add(CreateStatItem("错误", _result.Errors.Count.ToString(), new Color(0.9f, 0.3f, 0.3f)));
            statsRow.Add(CreateStatItem("警告", _result.Warnings.Count.ToString(), new Color(0.9f, 0.7f, 0.2f)));
            
            card.Add(statsRow);
            
            return card;
        }
        
        private VisualElement CreateStatItem(string label, string value, Color color)
        {
            var item = new VisualElement();
            item.style.alignItems = Align.Center;
            
            var valueLabel = new Label(value);
            valueLabel.style.fontSize = 20;
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.color = color;
            item.Add(valueLabel);
            
            var nameLabel = new Label(label);
            nameLabel.style.fontSize = 12;
            nameLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            item.Add(nameLabel);
            
            return item;
        }
        
        private VisualElement CreateSection(string title, List<string> items, Color color)
        {
            var section = new VisualElement();
            section.style.marginBottom = 16;
            
            var header = new Label(title);
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = color;
            header.style.marginBottom = 8;
            section.Add(header);
            
            foreach (var item in items)
            {
                var itemLabel = new Label("  • " + item);
                itemLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
                itemLabel.style.marginBottom = 4;
                section.Add(itemLabel);
            }
            
            return section;
        }
        
        private VisualElement CreateErrorSection()
        {
            var section = new VisualElement();
            section.style.marginBottom = 16;
            
            var header = new Label($"错误 ({_result.Errors.Count})");
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = new Color(0.9f, 0.3f, 0.3f);
            header.style.marginBottom = 8;
            section.Add(header);
            
            // 按类型分组
            var groupedErrors = _result.Errors.GroupBy(e => e.Type);
            
            foreach (var group in groupedErrors)
            {
                var groupHeader = new Label($"  [{GetErrorTypeName(group.Key)}]");
                groupHeader.style.color = new Color(0.9f, 0.5f, 0.5f);
                groupHeader.style.marginTop = 4;
                groupHeader.style.marginBottom = 4;
                section.Add(groupHeader);
                
                foreach (var error in group)
                {
                    var errorRow = CreateErrorRow(error);
                    section.Add(errorRow);
                }
            }
            
            return section;
        }
        
        private VisualElement CreateErrorRow(ValidationError error)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginLeft = 16;
            row.style.marginBottom = 4;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.paddingLeft = 8;
            row.style.paddingRight = 8;
            row.style.backgroundColor = new Color(0.3f, 0.15f, 0.15f, 0.5f);
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = 4;
            row.style.borderBottomRightRadius = 4;
            
            var icon = new Label("✗");
            icon.style.color = new Color(0.9f, 0.3f, 0.3f);
            icon.style.marginRight = 8;
            row.Add(icon);
            
            var messageContainer = new VisualElement();
            messageContainer.style.flexGrow = 1;
            
            var message = new Label(error.Message);
            message.style.color = new Color(0.9f, 0.8f, 0.8f);
            messageContainer.Add(message);
            
            if (!string.IsNullOrEmpty(error.AssetPath))
            {
                var pathLabel = new Label(error.AssetPath);
                pathLabel.style.fontSize = 10;
                pathLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                messageContainer.Add(pathLabel);
                
                // 点击定位到资源
                row.RegisterCallback<ClickEvent>(evt =>
                {
                    var asset = AssetDatabase.LoadMainAssetAtPath(error.AssetPath);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                    }
                });
                // 点击时高亮显示
            }
            
            row.Add(messageContainer);
            
            return row;
        }
        
        private VisualElement CreateWarningSection()
        {
            var section = new VisualElement();
            section.style.marginBottom = 16;
            
            var header = new Label($"警告 ({_result.Warnings.Count})");
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = new Color(0.9f, 0.7f, 0.2f);
            header.style.marginBottom = 8;
            section.Add(header);
            
            // 按类型分组
            var groupedWarnings = _result.Warnings.GroupBy(w => w.Type);
            
            foreach (var group in groupedWarnings)
            {
                var groupHeader = new Label($"  [{GetWarningTypeName(group.Key)}]");
                groupHeader.style.color = new Color(0.9f, 0.8f, 0.4f);
                groupHeader.style.marginTop = 4;
                groupHeader.style.marginBottom = 4;
                section.Add(groupHeader);
                
                foreach (var warning in group)
                {
                    var warningRow = CreateWarningRow(warning);
                    section.Add(warningRow);
                }
            }
            
            return section;
        }
        
        private VisualElement CreateWarningRow(ValidationWarning warning)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginLeft = 16;
            row.style.marginBottom = 4;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.paddingLeft = 8;
            row.style.paddingRight = 8;
            row.style.backgroundColor = new Color(0.3f, 0.25f, 0.1f, 0.5f);
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = 4;
            row.style.borderBottomRightRadius = 4;
            
            var icon = new Label("⚠");
            icon.style.color = new Color(0.9f, 0.7f, 0.2f);
            icon.style.marginRight = 8;
            row.Add(icon);
            
            var messageContainer = new VisualElement();
            messageContainer.style.flexGrow = 1;
            
            var message = new Label(warning.Message);
            message.style.color = new Color(0.9f, 0.9f, 0.7f);
            messageContainer.Add(message);
            
            if (!string.IsNullOrEmpty(warning.AssetPath))
            {
                var pathLabel = new Label(warning.AssetPath);
                pathLabel.style.fontSize = 10;
                pathLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                messageContainer.Add(pathLabel);
                
                // 点击定位到资源
                row.RegisterCallback<ClickEvent>(evt =>
                {
                    var asset = AssetDatabase.LoadMainAssetAtPath(warning.AssetPath);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                    }
                });
                // 点击时高亮显示
            }
            
            row.Add(messageContainer);
            
            return row;
        }
        
        private string GetErrorTypeName(ValidationErrorType type)
        {
            return type switch
            {
                ValidationErrorType.MissingAsset => "资源缺失",
                ValidationErrorType.InvalidReference => "无效引用",
                ValidationErrorType.DuplicateAddress => "地址重复",
                ValidationErrorType.ConfigurationError => "配置错误",
                ValidationErrorType.MissingDependency => "依赖缺失",
                _ => type.ToString()
            };
        }
        
        private string GetWarningTypeName(ValidationWarningType type)
        {
            return type switch
            {
                ValidationWarningType.LargeAsset => "大文件",
                ValidationWarningType.UnusedAsset => "未使用",
                ValidationWarningType.CircularDependency => "循环依赖",
                ValidationWarningType.MixedCase => "大小写问题",
                ValidationWarningType.SpecialCharacters => "特殊字符",
                _ => type.ToString()
            };
        }
        
        private void ExportReport()
        {
            var path = EditorUtility.SaveFilePanel("导出验证报告", "", $"{_packageName}_ValidationReport.txt", "txt");
            if (string.IsNullOrEmpty(path))
                return;
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("========================================");
            sb.AppendLine($"资源验证报告: {_packageName}");
            sb.AppendLine($"验证时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("========================================");
            sb.AppendLine();
            
            sb.AppendLine($"状态: {(_result.IsValid ? "通过" : "失败")}");
            sb.AppendLine($"资源总数: {_result.TotalAssets}");
            sb.AppendLine($"有效资源: {_result.ValidAssets}");
            sb.AppendLine($"错误数: {_result.Errors.Count}");
            sb.AppendLine($"警告数: {_result.Warnings.Count}");
            sb.AppendLine();
            
            if (_result.Info.Count > 0)
            {
                sb.AppendLine("【信息】");
                foreach (var info in _result.Info)
                {
                    sb.AppendLine($"  {info}");
                }
                sb.AppendLine();
            }
            
            if (_result.Errors.Count > 0)
            {
                sb.AppendLine("【错误】");
                foreach (var error in _result.Errors)
                {
                    sb.AppendLine($"  [{GetErrorTypeName(error.Type)}] {error.Message}");
                    if (!string.IsNullOrEmpty(error.AssetPath))
                    {
                        sb.AppendLine($"    路径: {error.AssetPath}");
                    }
                }
                sb.AppendLine();
            }
            
            if (_result.Warnings.Count > 0)
            {
                sb.AppendLine("【警告】");
                foreach (var warning in _result.Warnings)
                {
                    sb.AppendLine($"  [{GetWarningTypeName(warning.Type)}] {warning.Message}");
                    if (!string.IsNullOrEmpty(warning.AssetPath))
                    {
                        sb.AppendLine($"    路径: {warning.AssetPath}");
                    }
                }
                sb.AppendLine();
            }
            
            System.IO.File.WriteAllText(path, sb.ToString());
            EditorUtility.DisplayDialog("导出成功", $"报告已保存到:\n{path}", "确定");
        }
        
        #region Reference Graph
        
        private VisualElement CreateGraphSection()
        {
            var section = new VisualElement();
            section.style.marginBottom = 16;
            
            // 标题
            var header = new Label($"引用关系图 ({_result.ReferenceGraph.Count} 个资源)");
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = new Color(0.5f, 0.8f, 0.5f);
            header.style.marginBottom = 8;
            section.Add(header);
            
            // 图形视图容器
            var graphContainer = new VisualElement();
            graphContainer.style.height = 600;
            graphContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            graphContainer.style.borderTopLeftRadius = 4;
            graphContainer.style.borderTopRightRadius = 4;
            graphContainer.style.borderBottomLeftRadius = 4;
            graphContainer.style.borderBottomRightRadius = 4;
            
            // 创建图形视图
            var graphView = new AssetReferenceGraphView(_result);
            graphView.style.flexGrow = 1;
            graphContainer.Add(graphView);
            
            section.Add(graphContainer);
            
            // 帮助提示
            var helpLabel = new Label(
                "操作: 鼠标滚轮缩放 | 中键拖拽画布 | 左键拖拽节点 | 双击定位资源 | 绿色=Package内 橙色=外部依赖"
            );
            helpLabel.style.fontSize = 10;
            helpLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            helpLabel.style.marginTop = 4;
            section.Add(helpLabel);
            
            return section;
        }
        
        #endregion
    }
}
