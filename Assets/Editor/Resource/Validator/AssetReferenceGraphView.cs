using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 资源引用关系图视图
    /// </summary>
    public class AssetReferenceGraphView : GraphView
    {
        private ValidationResult _result;
        private Dictionary<string, AssetNode> _nodeMap = new Dictionary<string, AssetNode>();
        
        public AssetReferenceGraphView(ValidationResult result)
        {
            _result = result;
            
            // 设置背景色
            style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            
            // 添加背景网格
            var gridBackground = new GridBackground();
            Insert(0, gridBackground);
            gridBackground.StretchToParentSize();
            
            // 添加操作能力
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            
            // 设置缩放范围
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            
            // 构建图
            BuildGraph();
        }
        
        private void BuildGraph()
        {
            // 只显示 Package 内的资源作为主节点
            var packageAssets = _result.ReferenceGraph.Values
                .Where(a => a.IsInPackage)
                .OrderByDescending(a => a.ReferencedBy.Count + a.Dependencies.Count)
                .Take(50) // 限制节点数量以保证性能
                .ToList();
            
            if (packageAssets.Count == 0)
                return;
            
            // 创建节点
            foreach (var asset in packageAssets)
            {
                var node = CreateAssetNode(asset);
                _nodeMap[asset.AssetPath] = node;
                AddElement(node);
            }
            
            // 创建边（连接）
            foreach (var asset in packageAssets)
            {
                var sourceNode = _nodeMap[asset.AssetPath];
                
                // 连接依赖
                foreach (var depPath in asset.Dependencies)
                {
                    if (_nodeMap.TryGetValue(depPath, out var targetNode))
                    {
                        var edge = sourceNode.outputPort.ConnectTo(targetNode.inputPort);
                        edge.edgeControl.edgeWidth = 2;
                        AddElement(edge);
                    }
                }
            }
            
            // 自动布局
            LayoutNodes();
        }
        
        private AssetNode CreateAssetNode(AssetReferenceInfo asset)
        {
            var node = new AssetNode(asset);
            node.SetPosition(new Rect(Vector2.zero, new Vector2(200, 100)));
            
            // 双击定位资源
            node.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount == 2)
                {
                    var obj = AssetDatabase.LoadMainAssetAtPath(asset.AssetPath);
                    if (obj != null)
                    {
                        EditorGUIUtility.PingObject(obj);
                        Selection.activeObject = obj;
                    }
                }
            });
            
            return node;
        }
        
        private void LayoutNodes()
        {
            // 使用力导向布局算法
            var nodes = _nodeMap.Values.ToList();
            if (nodes.Count == 0) return;
            
            // 初始化随机位置（更大的范围）
            var random = new System.Random(42);
            foreach (var node in nodes)
            {
                var x = random.Next(-1000, 1000);
                var y = random.Next(-1000, 1000);
                node.SetPosition(new Rect(x, y, 200, 100));
            }
            
            // 力导向迭代
            const int iterations = 200;
            const float repulsionForce = 100000f;  // 增加斥力
            const float attractionForce = 0.05f;   // 减少引力
            const float damping = 0.8f;            // 增加阻尼
            const float minDistance = 250f;        // 最小距离
            
            var velocities = new Dictionary<AssetNode, Vector2>();
            foreach (var node in nodes)
            {
                velocities[node] = Vector2.zero;
            }
            
            for (int iter = 0; iter < iterations; iter++)
            {
                // 计算斥力（所有节点互相排斥）
                for (int i = 0; i < nodes.Count; i++)
                {
                    for (int j = i + 1; j < nodes.Count; j++)
                    {
                        var node1 = nodes[i];
                        var node2 = nodes[j];
                        
                        var pos1 = node1.GetPosition().center;
                        var pos2 = node2.GetPosition().center;
                        var delta = pos1 - pos2;
                        var distance = delta.magnitude;
                        
                        if (distance < 1f) distance = 1f;
                        
                        // 如果距离太近，增加斥力
                        var repulsion = repulsionForce / (distance * distance);
                        if (distance < minDistance)
                        {
                            repulsion *= 2f;
                        }
                        
                        var force = repulsion * delta.normalized;
                        velocities[node1] += force;
                        velocities[node2] -= force;
                    }
                }
                
                // 计算引力（有连接的节点互相吸引）
                foreach (var kvp in _nodeMap)
                {
                    var asset = _result.ReferenceGraph[kvp.Key];
                    var sourceNode = kvp.Value;
                    var sourcePos = sourceNode.GetPosition().center;
                    
                    foreach (var depPath in asset.Dependencies)
                    {
                        if (_nodeMap.TryGetValue(depPath, out var targetNode))
                        {
                            var targetPos = targetNode.GetPosition().center;
                            var delta = targetPos - sourcePos;
                            var distance = delta.magnitude;
                            
                            // 只有距离超过理想距离时才吸引
                            var idealDistance = 400f;
                            if (distance > idealDistance)
                            {
                                var force = attractionForce * (distance - idealDistance) * delta.normalized;
                                velocities[sourceNode] += force;
                                velocities[targetNode] -= force;
                            }
                        }
                    }
                }
                
                // 应用速度和阻尼
                foreach (var node in nodes)
                {
                    velocities[node] *= damping;
                    
                    // 限制最大速度
                    var maxSpeed = 50f;
                    if (velocities[node].magnitude > maxSpeed)
                    {
                        velocities[node] = velocities[node].normalized * maxSpeed;
                    }
                    
                    var pos = node.GetPosition();
                    pos.position += velocities[node];
                    node.SetPosition(pos);
                }
            }
            
            // 居中显示
            CenterGraph();
        }
        
        private void CenterGraph()
        {
            if (_nodeMap.Count == 0) return;
            
            var bounds = new Rect();
            bool first = true;
            
            foreach (var node in _nodeMap.Values)
            {
                var pos = node.GetPosition();
                if (first)
                {
                    bounds = pos;
                    first = false;
                }
                else
                {
                    bounds = Rect.MinMaxRect(
                        Mathf.Min(bounds.xMin, pos.xMin),
                        Mathf.Min(bounds.yMin, pos.yMin),
                        Mathf.Max(bounds.xMax, pos.xMax),
                        Mathf.Max(bounds.yMax, pos.yMax)
                    );
                }
            }
            
            var center = bounds.center;
            var offset = -center;
            
            foreach (var node in _nodeMap.Values)
            {
                var pos = node.GetPosition();
                pos.position += offset;
                node.SetPosition(pos);
            }
        }
        
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            ports.ForEach(port =>
            {
                if (startPort != port && startPort.node != port.node)
                {
                    compatiblePorts.Add(port);
                }
            });
            return compatiblePorts;
        }
    }
    
    /// <summary>
    /// 资源节点
    /// </summary>
    public class AssetNode : Node
    {
        public Port inputPort;
        public Port outputPort;
        private AssetReferenceInfo _asset;
        
        public AssetNode(AssetReferenceInfo asset)
        {
            _asset = asset;
            title = asset.AssetName;
            
            // 根据是否在 Package 中设置颜色
            if (asset.IsInPackage)
            {
                style.backgroundColor = new Color(0.2f, 0.4f, 0.3f, 0.8f);
            }
            else
            {
                style.backgroundColor = new Color(0.4f, 0.3f, 0.2f, 0.8f);
            }
            
            // 创建输入端口（被引用）
            inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            inputPort.portName = "被引用";
            inputContainer.Add(inputPort);
            
            // 创建输出端口（依赖）
            outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(float));
            outputPort.portName = "依赖";
            outputContainer.Add(outputPort);
            
            // 添加信息标签
            var infoContainer = new VisualElement();
            infoContainer.style.paddingTop = 4;
            infoContainer.style.paddingBottom = 4;
            infoContainer.style.paddingLeft = 8;
            infoContainer.style.paddingRight = 8;
            
            var typeLabel = new Label($"[{asset.AssetType}]");
            typeLabel.style.fontSize = 10;
            typeLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            infoContainer.Add(typeLabel);
            
            var refCountLabel = new Label($"↑{asset.ReferencedBy.Count} ↓{asset.Dependencies.Count}");
            refCountLabel.style.fontSize = 10;
            refCountLabel.style.color = new Color(0.6f, 0.8f, 0.6f);
            infoContainer.Add(refCountLabel);
            
            var sizeLabel = new Label(FormatBytes(asset.FileSize));
            sizeLabel.style.fontSize = 10;
            sizeLabel.style.color = new Color(0.8f, 0.8f, 0.6f);
            infoContainer.Add(sizeLabel);
            
            mainContainer.Add(infoContainer);
            
            // 设置 tooltip
            tooltip = asset.AssetPath;
            
            RefreshExpandedState();
            RefreshPorts();
        }
        
        private string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / 1024.0 / 1024.0:F2} MB";
            return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
        }
    }
}
