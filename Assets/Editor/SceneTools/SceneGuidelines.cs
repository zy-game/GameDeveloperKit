using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.SceneTools
{
    /// <summary>
    /// Scene视图辅助线系统，支持2D和3D模式
    /// </summary>
    public class SceneGuidelines
    {
        private const float GUIDELINE_LENGTH = 100000f;
        private const int MAX_GRID_LINES = 200; // 性能保护上限
        
        private float _currentGridSize = 1f; // 当前使用的网格大小
        private float _actualDrawnGridSize = 1f; // 实际绘制的网格大小（可能因性能限制而调整）
        private Vector3 _lastPosition; // 记录上次位置，用于检测是否在拖动
        private bool _isDragging = false; // 是否正在拖动
        
        public void OnSceneGUI(SceneView sceneView)
        {
            // 处理Shift+滚轮调整网格大小（无论是否选中物体都可以调整）
            HandleGridSizeAdjustment(sceneView);
            
            var selectedGO = Selection.activeGameObject;
            
            // 根据相机距离动态计算网格大小，再乘以手动缩放（从设置中读取）
            var cameraDistance = sceneView.size;
            _currentGridSize = CalculateDynamicGridSize(cameraDistance) * SceneToolsSettings.GridScale;
            
            // 确保 _actualDrawnGridSize 有初始值
            if (_actualDrawnGridSize <= 0)
            {
                _actualDrawnGridSize = _currentGridSize;
            }
            
            var is2DMode = sceneView.in2DMode;
            
            // 处理吸附（需要选中物体）
            if (selectedGO != null && SceneToolsSettings.SnapEnabled)
            {
                HandleSnapping(selectedGO, is2DMode);
            }
            
            if (!SceneToolsSettings.GuidelinesVisible)
                return;
            
            // 获取绘制中心点（选中物体的位置或相机中心）
            Vector3 position;
            if (selectedGO != null)
            {
                position = selectedGO.transform.position;
            }
            else
            {
                // 没有选中物体时，以相机中心为基准绘制
                position = sceneView.camera.transform.position;
                if (is2DMode)
                {
                    position.z = 0;
                }
            }
            
            if (is2DMode)
            {
                Draw2DGuidelines(sceneView, position, _currentGridSize);
            }
            else
            {
                Draw3DGuidelines(position, _currentGridSize);
            }
        }
        
        /// <summary>
        /// 根据相机距离计算合适的网格大小
        /// </summary>
        private float CalculateDynamicGridSize(float cameraDistance)
        {
            // 基于相机距离计算网格大小，确保网格在视觉上保持合适的密度
            var baseSize = SceneToolsSettings.SnapSize;
            
            if (cameraDistance < 1f)
                return baseSize * 0.1f;
            else if (cameraDistance < 10f)
                return baseSize;
            else if (cameraDistance < 100f)
                return baseSize * 10f;
            else if (cameraDistance < 1000f)
                return baseSize * 100f;
            else
                return baseSize * 1000f;
        }
        
        /// <summary>
        /// 处理Shift+滚轮调整网格大小
        /// </summary>
        private void HandleGridSizeAdjustment(SceneView sceneView)
        {
            var evt = Event.current;
            
            // 需要在Layout阶段也检测，因为ScrollWheel事件可能在不同阶段触发
            if (evt.shift && !evt.control && !evt.alt)
            {
                if (evt.type == EventType.ScrollWheel)
                {
                    var scale = SceneToolsSettings.GridScale;
                    // 滚轮向下放大网格，向上缩小网格（每次10倍变化）
                    if (evt.delta.y > 0)
                    {
                        scale *= 10f;
                    }
                    else if (evt.delta.y < 0)
                    {
                        scale *= 0.1f;
                    }
                    
                    // 保存到设置（会自动限制范围）
                    SceneToolsSettings.GridScale = scale;
                    
                    evt.Use();
                    sceneView.Repaint();
                }
                // 也检测按键事件作为备选方案
                else if (evt.type == EventType.KeyDown)
                {
                    var scale = SceneToolsSettings.GridScale;
                    if (evt.keyCode == KeyCode.Equals || evt.keyCode == KeyCode.Plus || evt.keyCode == KeyCode.KeypadPlus)
                    {
                        scale *= 0.5f;
                        SceneToolsSettings.GridScale = scale;
                        evt.Use();
                        sceneView.Repaint();
                    }
                    else if (evt.keyCode == KeyCode.Minus || evt.keyCode == KeyCode.KeypadMinus)
                    {
                        scale *= 2f;
                        SceneToolsSettings.GridScale = scale;
                        evt.Use();
                        sceneView.Repaint();
                    }
                }
            }
        }
        
        /// <summary>
        /// 绘制3D模式辅助线（立体XYZ轴）
        /// </summary>
        private void Draw3DGuidelines(Vector3 position, float gridSize)
        {
            var color = SceneToolsSettings.GuidelineColor;
            
            // 辅助线跟随物体实际位置（游标卡尺效果）
            // X轴辅助线（红色）
            var xColor = new Color(1f, 0.3f, 0.3f, color.a);
            Handles.color = xColor;
            Handles.DrawLine(
                new Vector3(-GUIDELINE_LENGTH, position.y, position.z),
                new Vector3(GUIDELINE_LENGTH, position.y, position.z)
            );
            
            // Y轴辅助线（绿色）
            var yColor = new Color(0.3f, 1f, 0.3f, color.a);
            Handles.color = yColor;
            Handles.DrawLine(
                new Vector3(position.x, -GUIDELINE_LENGTH, position.z),
                new Vector3(position.x, GUIDELINE_LENGTH, position.z)
            );
            
            // Z轴辅助线（蓝色）
            var zColor = new Color(0.3f, 0.3f, 1f, color.a);
            Handles.color = zColor;
            Handles.DrawLine(
                new Vector3(position.x, position.y, -GUIDELINE_LENGTH),
                new Vector3(position.x, position.y, GUIDELINE_LENGTH)
            );
            
            // 使用相机位置绘制网格
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                var cameraCenter = sceneView.camera.transform.position;
                var viewExtent = sceneView.size * 4f; // 扩大范围确保完全覆盖
                DrawGrid3D(cameraCenter, gridSize, color, viewExtent, position.y);
            }
        }
        
        /// <summary>
        /// 绘制3D网格（XZ平面）
        /// </summary>
        private void DrawGrid3D(Vector3 cameraCenter, float gridSize, Color color, float viewExtent, float yPos)
        {
            var gridColor = new Color(color.r, color.g, color.b, color.a * 0.3f);
            Handles.color = gridColor;
            
            // 计算网格起始和结束位置（对齐到网格）
            var startX = Mathf.Floor((cameraCenter.x - viewExtent) / gridSize) * gridSize;
            var endX = Mathf.Ceil((cameraCenter.x + viewExtent) / gridSize) * gridSize;
            var startZ = Mathf.Floor((cameraCenter.z - viewExtent) / gridSize) * gridSize;
            var endZ = Mathf.Ceil((cameraCenter.z + viewExtent) / gridSize) * gridSize;
            
            // 动态计算线条数量
            var xLineCount = Mathf.CeilToInt((endX - startX) / gridSize);
            var zLineCount = Mathf.CeilToInt((endZ - startZ) / gridSize);
            
            // 性能保护：如果线条太多，增大网格大小
            while (xLineCount > MAX_GRID_LINES || zLineCount > MAX_GRID_LINES)
            {
                gridSize *= 2f;
                startX = Mathf.Floor((cameraCenter.x - viewExtent) / gridSize) * gridSize;
                endX = Mathf.Ceil((cameraCenter.x + viewExtent) / gridSize) * gridSize;
                startZ = Mathf.Floor((cameraCenter.z - viewExtent) / gridSize) * gridSize;
                endZ = Mathf.Ceil((cameraCenter.z + viewExtent) / gridSize) * gridSize;
                xLineCount = Mathf.CeilToInt((endX - startX) / gridSize);
                zLineCount = Mathf.CeilToInt((endZ - startZ) / gridSize);
            }
            
            // 记录实际绘制的网格大小，供吸附功能使用
            _actualDrawnGridSize = gridSize;
            
            // X方向的线
            for (int i = 0; i <= zLineCount; i++)
            {
                var z = startZ + i * gridSize;
                Handles.DrawLine(
                    new Vector3(startX, yPos, z),
                    new Vector3(endX, yPos, z)
                );
            }
            
            // Z方向的线
            for (int i = 0; i <= xLineCount; i++)
            {
                var x = startX + i * gridSize;
                Handles.DrawLine(
                    new Vector3(x, yPos, startZ),
                    new Vector3(x, yPos, endZ)
                );
            }
        }
        
        /// <summary>
        /// 绘制2D模式辅助线（XY平面网格）
        /// </summary>
        private void Draw2DGuidelines(SceneView sceneView, Vector3 position, float gridSize)
        {
            var color = SceneToolsSettings.GuidelineColor;
            
            // 辅助线跟随物体实际位置（游标卡尺效果）
            // X轴辅助线（红色）- 水平线穿过物体
            var xColor = new Color(1f, 0.3f, 0.3f, color.a);
            Handles.color = xColor;
            Handles.DrawLine(
                new Vector3(-GUIDELINE_LENGTH, position.y, position.z),
                new Vector3(GUIDELINE_LENGTH, position.y, position.z)
            );
            
            // Y轴辅助线（绿色）- 垂直线穿过物体
            var yColor = new Color(0.3f, 1f, 0.3f, color.a);
            Handles.color = yColor;
            Handles.DrawLine(
                new Vector3(position.x, -GUIDELINE_LENGTH, position.z),
                new Vector3(position.x, GUIDELINE_LENGTH, position.z)
            );
            
            // 使用相机中心位置绘制网格，确保覆盖整个视图
            var cameraCenter = sceneView.camera.transform.position;
            var viewExtent = sceneView.size * 4f; // 扩大范围确保完全覆盖
            DrawGrid2D(cameraCenter, gridSize, color, viewExtent, position.z);
        }
        
        /// <summary>
        /// 绘制2D网格（XY平面）
        /// </summary>
        private void DrawGrid2D(Vector3 cameraCenter, float gridSize, Color color, float viewExtent, float zPos)
        {
            var gridColor = new Color(color.r, color.g, color.b, color.a * 0.3f);
            Handles.color = gridColor;
            
            // 计算网格起始和结束位置（对齐到网格）
            var startX = Mathf.Floor((cameraCenter.x - viewExtent) / gridSize) * gridSize;
            var endX = Mathf.Ceil((cameraCenter.x + viewExtent) / gridSize) * gridSize;
            var startY = Mathf.Floor((cameraCenter.y - viewExtent) / gridSize) * gridSize;
            var endY = Mathf.Ceil((cameraCenter.y + viewExtent) / gridSize) * gridSize;
            
            // 动态计算线条数量
            var xLineCount = Mathf.CeilToInt((endX - startX) / gridSize);
            var yLineCount = Mathf.CeilToInt((endY - startY) / gridSize);
            
            // 性能保护：如果线条太多，增大网格大小
            while (xLineCount > MAX_GRID_LINES || yLineCount > MAX_GRID_LINES)
            {
                gridSize *= 2f;
                startX = Mathf.Floor((cameraCenter.x - viewExtent) / gridSize) * gridSize;
                endX = Mathf.Ceil((cameraCenter.x + viewExtent) / gridSize) * gridSize;
                startY = Mathf.Floor((cameraCenter.y - viewExtent) / gridSize) * gridSize;
                endY = Mathf.Ceil((cameraCenter.y + viewExtent) / gridSize) * gridSize;
                xLineCount = Mathf.CeilToInt((endX - startX) / gridSize);
                yLineCount = Mathf.CeilToInt((endY - startY) / gridSize);
            }
            
            // 记录实际绘制的网格大小，供吸附功能使用
            _actualDrawnGridSize = gridSize;
            
            // 水平线
            for (int i = 0; i <= yLineCount; i++)
            {
                var y = startY + i * gridSize;
                Handles.DrawLine(
                    new Vector3(startX, y, zPos),
                    new Vector3(endX, y, zPos)
                );
            }
            
            // 垂直线
            for (int i = 0; i <= xLineCount; i++)
            {
                var x = startX + i * gridSize;
                Handles.DrawLine(
                    new Vector3(x, startY, zPos),
                    new Vector3(x, endY, zPos)
                );
            }
        }
        
        private Vector2 _lastSizeDelta; // 记录上次sizeDelta，用于检测是否在调整大小
        private Vector2 _startSizeDelta; // 记录开始调整时的sizeDelta
        private Vector2 _startAnchoredPosition; // 记录开始调整时的anchoredPosition
        private Vector3[] _startWorldCorners = new Vector3[4]; // 记录开始调整时的世界坐标角点
        private bool _isSizeChanging = false; // 是否正在调整大小
        
        /// <summary>
        /// 处理吸附功能 - 在拖拽结束后对齐到网格
        /// </summary>
        private void HandleSnapping(GameObject go, bool is2DMode)
        {
            var transform = go.transform;
            var currentPos = transform.position;
            var rectTransform = transform as RectTransform;
            
            // 确保网格大小已计算（即使辅助线不可见）
            if (_actualDrawnGridSize <= 0)
            {
                _actualDrawnGridSize = _currentGridSize > 0 ? _currentGridSize : SceneToolsSettings.SnapSize;
            }
            
            // 使用hotControl检测拖动状态
            var isCurrentlyDragging = GUIUtility.hotControl != 0;
            
            // 检测RectTransform的大小变化
            if (rectTransform != null)
            {
                var currentSizeDelta = rectTransform.sizeDelta;
                if (currentSizeDelta != _lastSizeDelta)
                {
                    if (!_isSizeChanging)
                    {
                        // 记录开始调整时的状态
                        _startSizeDelta = _lastSizeDelta;
                        _startAnchoredPosition = rectTransform.anchoredPosition;
                        // 记录开始时的世界坐标角点
                        rectTransform.GetWorldCorners(_startWorldCorners);
                    }
                    _isSizeChanging = true;
                    _lastSizeDelta = currentSizeDelta;
                }
            }
            
            // 检测位置变化来判断是否在移动
            if (currentPos != _lastPosition)
            {
                // 只有当大小没有变化时才认为是在移动
                if (!_isSizeChanging)
                {
                    _isDragging = true;
                }
                _lastPosition = currentPos;
            }
            
            // 当hotControl从非0变为0时，表示拖动结束
            if (!isCurrentlyDragging && (_isDragging || _isSizeChanging))
            {
                var snapSize = _actualDrawnGridSize > 0 ? _actualDrawnGridSize : _currentGridSize;
                if (snapSize <= 0) snapSize = SceneToolsSettings.SnapSize;
                
                if (_isSizeChanging && rectTransform != null)
                {
                    // 获取当前的世界坐标角点
                    var corners = new Vector3[4];
                    rectTransform.GetWorldCorners(corners);
                    // corners: 0=左下, 1=左上, 2=右上, 3=右下
                    
                    // 获取开始时的边界
                    var startCorners = _startWorldCorners;
                    
                    // 判断哪些边被拖拽了
                    bool leftChanged = Mathf.Abs(corners[0].x - startCorners[0].x) > 0.01f;
                    bool rightChanged = Mathf.Abs(corners[3].x - startCorners[3].x) > 0.01f;
                    bool bottomChanged = Mathf.Abs(corners[0].y - startCorners[0].y) > 0.01f;
                    bool topChanged = Mathf.Abs(corners[1].y - startCorners[1].y) > 0.01f;
                    
                    // 计算当前边界位置
                    float currentLeft = corners[0].x;
                    float currentRight = corners[3].x;
                    float currentBottom = corners[0].y;
                    float currentTop = corners[1].y;
                    
                    // 计算吸附后的边界位置（只吸附被拖拽的边）
                    float snappedLeft = leftChanged ? Mathf.Round(currentLeft / snapSize) * snapSize : currentLeft;
                    float snappedRight = rightChanged ? Mathf.Round(currentRight / snapSize) * snapSize : currentRight;
                    float snappedBottom = bottomChanged ? Mathf.Round(currentBottom / snapSize) * snapSize : currentBottom;
                    float snappedTop = topChanged ? Mathf.Round(currentTop / snapSize) * snapSize : currentTop;
                    
                    // 计算需要的偏移量
                    float deltaLeft = snappedLeft - currentLeft;
                    float deltaRight = snappedRight - currentRight;
                    float deltaBottom = snappedBottom - currentBottom;
                    float deltaTop = snappedTop - currentTop;
                    
                    bool hasChange = Mathf.Abs(deltaLeft) > 0.001f || Mathf.Abs(deltaRight) > 0.001f ||
                                    Mathf.Abs(deltaBottom) > 0.001f || Mathf.Abs(deltaTop) > 0.001f;
                    
                    if (hasChange)
                    {
                        Undo.RecordObject(rectTransform, "Snap Size to Grid");
                        
                        // 直接修改offsetMin和offsetMax
                        // offsetMin对应左下角，offsetMax对应右上角
                        var offsetMin = rectTransform.offsetMin;
                        var offsetMax = rectTransform.offsetMax;
                        
                        offsetMin.x += deltaLeft;
                        offsetMin.y += deltaBottom;
                        offsetMax.x += deltaRight;
                        offsetMax.y += deltaTop;
                        
                        rectTransform.offsetMin = offsetMin;
                        rectTransform.offsetMax = offsetMax;
                    }
                }
                else if (_isDragging)
                {
                    // 移动时，吸附位置
                    if (rectTransform != null)
                    {
                        var anchoredPos = rectTransform.anchoredPosition;
                        var snappedAnchoredPos = new Vector2(
                            Mathf.Round(anchoredPos.x / snapSize) * snapSize,
                            Mathf.Round(anchoredPos.y / snapSize) * snapSize
                        );
                        
                        if (anchoredPos != snappedAnchoredPos)
                        {
                            Undo.RecordObject(rectTransform, "Snap to Grid");
                            rectTransform.anchoredPosition = snappedAnchoredPos;
                        }
                    }
                    else
                    {
                        Vector3 snappedPos;
                        if (is2DMode)
                        {
                            snappedPos = new Vector3(
                                Mathf.Round(currentPos.x / snapSize) * snapSize,
                                Mathf.Round(currentPos.y / snapSize) * snapSize,
                                currentPos.z
                            );
                        }
                        else
                        {
                            snappedPos = new Vector3(
                                Mathf.Round(currentPos.x / snapSize) * snapSize,
                                Mathf.Round(currentPos.y / snapSize) * snapSize,
                                Mathf.Round(currentPos.z / snapSize) * snapSize
                            );
                        }
                        
                        if (currentPos != snappedPos)
                        {
                            Undo.RecordObject(transform, "Snap to Grid");
                            transform.position = snappedPos;
                        }
                    }
                }
                
                _isDragging = false;
                _isSizeChanging = false;
            }
        }
    }
}
