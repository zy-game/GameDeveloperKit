using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 资源引用关系图窗口
    /// </summary>
    public class AssetReferenceGraphWindow : EditorWindow
    {
        private ValidationResult _result;
        private string _packageName;
        private AssetReferenceGraphView _graphView;
        
        public static void ShowGraph(ValidationResult result, string packageName)
        {
            var window = GetWindow<AssetReferenceGraphWindow>("引用关系图");
            window._result = result;
            window._packageName = packageName;
            window.minSize = new Vector2(800, 600);
            window.CreateGraphView();
        }
        
        private void CreateGUI()
        {
            if (_result != null)
            {
                CreateGraphView();
            }
        }
        
        private void CreateGraphView()
        {
            rootVisualElement.Clear();
            
            if (_result == null || _result.ReferenceGraph.Count == 0)
            {
                var label = new Label("无引用关系数据");
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.flexGrow = 1;
                rootVisualElement.Add(label);
                return;
            }
            
            // 创建工具栏
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            toolbar.style.paddingTop = 4;
            toolbar.style.paddingBottom = 4;
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            
            var titleLabel = new Label($"引用关系图: {_packageName}");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginLeft = 8;
            titleLabel.style.flexGrow = 1;
            toolbar.Add(titleLabel);
            
            var statsLabel = new Label($"{_result.ReferenceGraph.Count} 个资源");
            statsLabel.style.marginRight = 8;
            toolbar.Add(statsLabel);
            
            var resetButton = new Button(() => ResetView());
            resetButton.text = "重置视图";
            toolbar.Add(resetButton);
            
            var listViewButton = new Button(() => ShowListView());
            listViewButton.text = "列表视图";
            toolbar.Add(listViewButton);
            
            rootVisualElement.Add(toolbar);
            
            // 创建图形视图
            _graphView = new AssetReferenceGraphView(_result);
            _graphView.style.flexGrow = 1;
            rootVisualElement.Add(_graphView);
            
            // 添加说明
            var helpBox = new HelpBox(
                "操作说明:\n" +
                "• 鼠标滚轮缩放\n" +
                "• 鼠标中键拖拽画布\n" +
                "• 左键拖拽节点\n" +
                "• 双击节点定位资源\n" +
                "• 绿色节点 = Package 内资源\n" +
                "• 橙色节点 = 外部依赖",
                HelpBoxMessageType.Info
            );
            helpBox.style.position = Position.Absolute;
            helpBox.style.bottom = 10;
            helpBox.style.right = 10;
            helpBox.style.maxWidth = 250;
            rootVisualElement.Add(helpBox);
        }
        
        private void ResetView()
        {
            if (_graphView != null)
            {
                _graphView.FrameAll();
            }
        }
        
        private void ShowListView()
        {
            ValidationResultWindow.ShowResult(_result, _packageName);
        }
    }
}
