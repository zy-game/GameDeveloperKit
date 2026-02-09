using UnityEngine;
using UnityEditor;
using GameDeveloperKit.PsdToUgui;
using System.IO;

namespace GameDeveloperKit.Editor.PsdToUgui
{
    [CustomEditor(typeof(PsdDocumentBinding))]
    public class PsdDocumentBindingEditor : UnityEditor.Editor
    {
        private PsdDocumentBinding _binding;
        private bool _showBindings = true;
        private Vector2 _scrollPosition;

        private void OnEnable()
        {
            _binding = (PsdDocumentBinding)target;
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.Space(5);

            // PSD 文件信息
            EditorGUILayout.LabelField("PSD 文件信息", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("文件路径", _binding.PsdFilePath);
                EditorGUILayout.TextField("文件哈希", _binding.PsdFileHash);
                EditorGUILayout.Vector2IntField("尺寸", new Vector2Int(_binding.PsdWidth, _binding.PsdHeight));
                EditorGUILayout.IntField("图层数", _binding.Bindings.Count);
            }

            EditorGUILayout.Space(10);

            // 操作按钮
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("重新导入", GUILayout.Height(25)))
            {
                ReimportPsd();
            }

            if (GUILayout.Button("打开 PSD 文件", GUILayout.Height(25)))
            {
                OpenPsdFile();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // 图层绑定列表
            _showBindings = EditorGUILayout.Foldout(_showBindings, $"图层绑定 ({_binding.Bindings.Count})", true);
            if (_showBindings)
            {
                DrawBindingsList();
            }

            // 孤立节点警告
            var orphans = _binding.GetOrphanBindings();
            if (orphans.Count > 0)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox($"发现 {orphans.Count} 个孤立节点（PSD 中已删除的图层）", MessageType.Warning);
                
                if (GUILayout.Button("删除所有孤立节点"))
                {
                    DeleteOrphanNodes();
                }
            }
        }

        private void DrawBindingsList()
        {
            var bindings = _binding.Bindings;
            if (bindings.Count == 0)
            {
                EditorGUILayout.HelpBox("暂无图层绑定", MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.MaxHeight(300));

            foreach (var binding in bindings)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                // 孤立标记
                if (binding.IsOrphan)
                {
                    EditorGUILayout.LabelField("!", GUILayout.Width(15));
                }

                // 图层名称
                EditorGUILayout.LabelField(binding.LayerName, GUILayout.Width(150));

                // 图层类型
                var typeNames = new[] { "Normal", "Group", "Text", "Shape", "Adjustment" };
                var typeName = binding.LayerType >= 0 && binding.LayerType < typeNames.Length 
                    ? typeNames[binding.LayerType] 
                    : "Unknown";
                EditorGUILayout.LabelField(typeName, GUILayout.Width(80));

                // GameObject 路径
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField(binding.GameObjectPath);
                }

                // 定位按钮
                if (GUILayout.Button("定位", GUILayout.Width(40)))
                {
                    LocateGameObject(binding);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void ReimportPsd()
        {
            if (string.IsNullOrEmpty(_binding.PsdFilePath))
            {
                EditorUtility.DisplayDialog("错误", "PSD 文件路径为空", "确定");
                return;
            }

            if (!File.Exists(_binding.PsdFilePath))
            {
                EditorUtility.DisplayDialog("错误", $"PSD 文件不存在:\n{_binding.PsdFilePath}", "确定");
                return;
            }

            PsdImporter.Import(_binding.PsdFilePath, PsdToUguiSettings.Instance);
        }

        private void OpenPsdFile()
        {
            if (string.IsNullOrEmpty(_binding.PsdFilePath))
            {
                EditorUtility.DisplayDialog("错误", "PSD 文件路径为空", "确定");
                return;
            }

            if (!File.Exists(_binding.PsdFilePath))
            {
                EditorUtility.DisplayDialog("错误", $"PSD 文件不存在:\n{_binding.PsdFilePath}", "确定");
                return;
            }

            // 使用系统默认程序打开 PSD 文件
            System.Diagnostics.Process.Start(_binding.PsdFilePath);
        }

        private void LocateGameObject(PsdDocumentBinding.LayerBinding binding)
        {
            var go = _binding.FindGameObject(binding.LayerId);
            if (go != null)
            {
                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);
            }
            else
            {
                EditorUtility.DisplayDialog("提示", $"找不到对应的 GameObject:\n{binding.GameObjectPath}", "确定");
            }
        }

        private void DeleteOrphanNodes()
        {
            var orphans = _binding.GetOrphanBindings();
            if (orphans.Count == 0) return;

            if (!EditorUtility.DisplayDialog("确认删除", 
                $"确定要删除 {orphans.Count} 个孤立节点吗？\n此操作不可撤销。", 
                "删除", "取消"))
            {
                return;
            }

            Undo.RecordObject(_binding.gameObject, "Delete Orphan Nodes");

            foreach (var binding in orphans)
            {
                var go = _binding.FindGameObject(binding.LayerId);
                if (go != null && go != _binding.gameObject)
                {
                    Undo.DestroyObjectImmediate(go);
                }
                _binding.RemoveBinding(binding.LayerId);
            }

            EditorUtility.SetDirty(_binding);
            
            // 如果是 Prefab，保存修改
            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                EditorUtility.SetDirty(prefabStage.prefabContentsRoot);
            }
        }
    }
}
