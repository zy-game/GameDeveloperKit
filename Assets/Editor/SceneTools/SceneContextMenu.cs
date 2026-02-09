using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.SceneTools
{
    /// <summary>
    /// Scene视图右键菜单，通过射线检测显示点击位置下的所有物体
    /// 使用 Shift+右键 触发，避免覆盖Unity原有的右键菜单
    /// </summary>
    public class SceneContextMenu
    {
        private const int MAX_PICK_DEPTH = 50;
        
        public void OnSceneGUI(SceneView sceneView)
        {
            var evt = Event.current;
            
            // 使用 Shift+右键 触发自定义菜单，保留原有右键菜单
            if (evt.type == EventType.MouseDown && evt.button == 1 && evt.shift && !evt.alt)
            {
                var mousePos = evt.mousePosition;
                var pickedObjects = PickAllObjects(sceneView, mousePos);
                
                if (pickedObjects.Count > 0)
                {
                    ShowContextMenu(pickedObjects);
                    evt.Use();
                }
            }
        }
        
        /// <summary>
        /// 获取鼠标位置下的所有物体
        /// </summary>
        private List<GameObject> PickAllObjects(SceneView sceneView, Vector2 mousePos)
        {
            var result = new List<GameObject>();
            var ignore = new List<GameObject>();
            
            // 使用HandleUtility.PickGameObject循环获取所有物体
            for (int i = 0; i < MAX_PICK_DEPTH; i++)
            {
                var picked = HandleUtility.PickGameObject(mousePos, false, ignore.ToArray());
                if (picked == null)
                    break;
                
                result.Add(picked);
                ignore.Add(picked);
            }
            
            return result;
        }
        
        /// <summary>
        /// 显示右键菜单
        /// </summary>
        private void ShowContextMenu(List<GameObject> objects)
        {
            var menu = new GenericMenu();
            
            // 构建层级树
            var hierarchyTree = BuildHierarchyTree(objects);
            
            // 添加菜单项
            foreach (var node in hierarchyTree)
            {
                AddMenuItemsRecursive(menu, node, "");
            }
            
            if (menu.GetItemCount() == 0)
            {
                menu.AddDisabledItem(new GUIContent("无可选物体"));
            }
            
            menu.ShowAsContext();
        }
        
        /// <summary>
        /// 构建层级树结构
        /// </summary>
        private List<HierarchyNode> BuildHierarchyTree(List<GameObject> objects)
        {
            var rootNodes = new List<HierarchyNode>();
            var nodeMap = new Dictionary<GameObject, HierarchyNode>();
            
            // 收集所有相关的父物体
            var allObjects = new HashSet<GameObject>(objects);
            foreach (var obj in objects)
            {
                var parent = obj.transform.parent;
                while (parent != null)
                {
                    allObjects.Add(parent.gameObject);
                    parent = parent.parent;
                }
            }
            
            // 创建节点
            foreach (var obj in allObjects)
            {
                nodeMap[obj] = new HierarchyNode
                {
                    GameObject = obj,
                    IsDirectHit = objects.Contains(obj)
                };
            }
            
            // 建立父子关系
            foreach (var kvp in nodeMap)
            {
                var obj = kvp.Key;
                var node = kvp.Value;
                
                if (obj.transform.parent != null && nodeMap.TryGetValue(obj.transform.parent.gameObject, out var parentNode))
                {
                    parentNode.Children.Add(node);
                }
                else
                {
                    rootNodes.Add(node);
                }
            }
            
            // 按名称排序
            SortNodes(rootNodes);
            
            return rootNodes;
        }
        
        private void SortNodes(List<HierarchyNode> nodes)
        {
            nodes.Sort((a, b) => string.Compare(a.GameObject.name, b.GameObject.name));
            foreach (var node in nodes)
            {
                SortNodes(node.Children);
            }
        }
        
        /// <summary>
        /// 递归添加菜单项
        /// </summary>
        private void AddMenuItemsRecursive(GenericMenu menu, HierarchyNode node, string path)
        {
            var itemPath = string.IsNullOrEmpty(path) ? node.GameObject.name : path + "/" + node.GameObject.name;
            var icon = GetGameObjectIcon(node.GameObject);
            
            // 如果是直接命中的物体，添加可点击项
            if (node.IsDirectHit)
            {
                var content = icon != null 
                    ? new GUIContent(itemPath, icon) 
                    : new GUIContent(itemPath);
                
                var go = node.GameObject;
                menu.AddItem(content, false, () => SelectGameObject(go));
            }
            
            // 递归添加子节点
            foreach (var child in node.Children)
            {
                AddMenuItemsRecursive(menu, child, itemPath);
            }
        }
        
        private Texture GetGameObjectIcon(GameObject go)
        {
            return EditorGUIUtility.ObjectContent(go, typeof(GameObject)).image;
        }
        
        private void SelectGameObject(GameObject go)
        {
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            SceneView.lastActiveSceneView?.FrameSelected();
        }
        
        private class HierarchyNode
        {
            public GameObject GameObject;
            public bool IsDirectHit;
            public List<HierarchyNode> Children = new List<HierarchyNode>();
        }
    }
}
