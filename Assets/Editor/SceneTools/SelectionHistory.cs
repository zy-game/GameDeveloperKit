using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.SceneTools
{
    /// <summary>
    /// 选择历史记录管理器，支持前进/后退导航
    /// </summary>
    public class SelectionHistory
    {
        private const int MAX_HISTORY_SIZE = 50;
        
        private readonly List<GameObject> _history = new List<GameObject>();
        private int _currentIndex = -1;
        private bool _isNavigating = false;
        
        public SelectionHistory()
        {
            Selection.selectionChanged += OnSelectionChanged;
        }
        
        ~SelectionHistory()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }
        
        private void OnSelectionChanged()
        {
            // 如果是通过导航触发的选择变化，不记录
            if (_isNavigating)
                return;
            
            var selected = Selection.activeGameObject;
            if (selected == null)
                return;
            
            // 检查是否已经在历史中存在
            int existingIndex = _history.IndexOf(selected);
            if (existingIndex >= 0)
            {
                // 如果已存在，只更新当前索引位置
                _currentIndex = existingIndex;
                return;
            }
            
            // 添加到历史末尾（不删除后面的记录）
            _history.Add(selected);
            _currentIndex = _history.Count - 1;
            
            // 限制历史大小
            if (_history.Count > MAX_HISTORY_SIZE)
            {
                _history.RemoveAt(0);
                _currentIndex--;
            }
        }
        
        /// <summary>
        /// 后退到上一个选择
        /// </summary>
        public void GoBack()
        {
            // 清理已销毁的对象
            CleanupDestroyedObjects();
            
            if (_history.Count == 0)
                return;
            
            if (_currentIndex > 0)
            {
                _currentIndex--;
            }
            
            SelectAtCurrentIndex();
        }
        
        /// <summary>
        /// 前进到下一个选择
        /// </summary>
        public void GoForward()
        {
            // 清理已销毁的对象
            CleanupDestroyedObjects();
            
            if (_history.Count == 0)
                return;
            
            if (_currentIndex < _history.Count - 1)
            {
                _currentIndex++;
            }
            
            SelectAtCurrentIndex();
        }
        
        private void SelectAtCurrentIndex()
        {
            if (_currentIndex < 0 || _currentIndex >= _history.Count)
                return;
            
            var target = _history[_currentIndex];
            if (target == null)
            {
                // 对象已被销毁，尝试下一个
                _history.RemoveAt(_currentIndex);
                if (_currentIndex >= _history.Count)
                    _currentIndex = _history.Count - 1;
                return;
            }
            
            _isNavigating = true;
            Selection.activeGameObject = target;
            EditorGUIUtility.PingObject(target);
            _isNavigating = false;
        }
        
        private void CleanupDestroyedObjects()
        {
            for (int i = _history.Count - 1; i >= 0; i--)
            {
                if (_history[i] == null)
                {
                    _history.RemoveAt(i);
                    if (_currentIndex > i)
                        _currentIndex--;
                    else if (_currentIndex == i)
                        _currentIndex = Mathf.Min(_currentIndex, _history.Count - 1);
                }
            }
            
            _currentIndex = Mathf.Clamp(_currentIndex, -1, _history.Count - 1);
        }
        
        /// <summary>
        /// 处理键盘事件
        /// </summary>
        public bool HandleKeyboardEvent(Event evt)
        {
            if (evt.type != EventType.KeyDown)
                return false;
            
            // Alt + 左方向键：后退
            if (evt.alt && evt.keyCode == KeyCode.LeftArrow)
            {
                GoBack();
                evt.Use();
                return true;
            }
            
            // Alt + 右方向键：前进
            if (evt.alt && evt.keyCode == KeyCode.RightArrow)
            {
                GoForward();
                evt.Use();
                return true;
            }
            
            return false;
        }
    }
}
