using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using GameDeveloperKit.PsdToUgui;

namespace GameDeveloperKit.Editor.PsdToUgui
{
    /// <summary>
    /// PSD 导入器 - 支持首次导入和增量导入
    /// </summary>
    public static class PsdImporter
    {
        public static void Import(string psdFilePath, PsdToUguiSettings settings)
        {
            if (string.IsNullOrEmpty(psdFilePath) || !File.Exists(psdFilePath))
            {
                EditorUtility.DisplayDialog("错误", "PSD 文件不存在", "确定");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("导入 PSD", "正在解析 PSD 文件...", 0.1f);

                // 解析 PSD
                var parser = new PsdParser();
                var document = parser.Parse(psdFilePath);

                // 计算文件哈希
                var fileHash = ComputeFileHash(psdFilePath);

                // 计算输出路径
                var prefabPath = settings.GetPrefabOutputPath(document.FileName);
                var texturePath = settings.GetTextureOutputPath(document.FileName);

                // 确保目录存在
                EnsureDirectoryExists(Path.GetDirectoryName(prefabPath));
                EnsureDirectoryExists(texturePath);

                EditorUtility.DisplayProgressBar("导入 PSD", "检查现有 Prefab...", 0.2f);

                // 检查是否存在现有 Prefab
                var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                PsdDocumentBinding existingBinding = null;

                if (existingPrefab != null)
                {
                    existingBinding = existingPrefab.GetComponent<PsdDocumentBinding>();
                    
                    if (existingBinding == null)
                    {
                        // Prefab 存在但没有绑定组件，询问是否覆盖
                        if (!EditorUtility.DisplayDialog("警告", 
                            $"目标 Prefab 已存在但没有 PSD 绑定信息:\n{prefabPath}\n\n是否覆盖？", 
                            "覆盖", "取消"))
                        {
                            return;
                        }
                    }
                }

                EditorUtility.DisplayProgressBar("导入 PSD", "正在生成 UGUI...", 0.4f);

                GameObject resultPrefab;
                if (existingBinding != null)
                {
                    // 增量导入
                    resultPrefab = IncrementalImport(document, existingPrefab, existingBinding, 
                        psdFilePath, fileHash, texturePath, settings);
                }
                else
                {
                    // 首次导入
                    resultPrefab = FirstTimeImport(document, psdFilePath, fileHash, 
                        prefabPath, texturePath, settings);
                }

                EditorUtility.DisplayProgressBar("导入 PSD", "完成", 1f);

                // 选中生成的 Prefab
                Selection.activeObject = resultPrefab;
                EditorGUIUtility.PingObject(resultPrefab);

                Debug.Log($"[PsdImporter] 成功导入 PSD: {document.FileName}");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("导入失败", $"无法导入 PSD 文件:\n{ex.Message}", "确定");
                Debug.LogError($"[PsdImporter] 导入失败: {ex}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static GameObject FirstTimeImport(PsdDocument document, string psdFilePath, 
            string fileHash, string prefabPath, string texturePath, PsdToUguiSettings settings)
        {
            // 使用现有的 UguiConverter 生成 GameObject
            var converter = new UguiConverter(settings);
            var root = converter.ConvertWithBinding(document, texturePath);

            // 添加 PsdDocumentBinding 组件
            var binding = root.AddComponent<PsdDocumentBinding>();
            binding.Initialize(psdFilePath, fileHash, document.Width, document.Height);

            // 记录所有图层绑定
            RecordBindings(binding, document.Layers, "");

            // 保存为 Prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            // 清理临时对象
            UnityEngine.Object.DestroyImmediate(root);

            AssetDatabase.Refresh();
            return prefab;
        }

        private static GameObject IncrementalImport(PsdDocument document, GameObject existingPrefab,
            PsdDocumentBinding existingBinding, string psdFilePath, string fileHash, 
            string texturePath, PsdToUguiSettings settings)
        {
            // 打开 Prefab 进行编辑
            var prefabPath = AssetDatabase.GetAssetPath(existingPrefab);
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            var binding = prefabRoot.GetComponent<PsdDocumentBinding>();

            try
            {
                // 清除之前的孤立标记
                binding.ClearOrphanMarks();

                // 创建图层 ID 到绑定的映射
                var bindingMap = new Dictionary<int, PsdDocumentBinding.LayerBinding>();
                foreach (var b in binding.Bindings)
                {
                    bindingMap[b.LayerId] = b;
                }

                // 建立 layerId -> GameObject 的映射（在整个层级中搜索）
                var gameObjectMap = new Dictionary<int, GameObject>();
                foreach (var b in binding.Bindings)
                {
                    var go = binding.FindGameObject(b.LayerId);
                    if (go != null)
                    {
                        gameObjectMap[b.LayerId] = go;
                    }
                }

                // 创建新的图层 ID 集合
                var newLayerIds = new HashSet<int>();
                CollectLayerIds(document.Layers, newLayerIds);

                // 标记孤立节点（在绑定中存在但在新 PSD 中不存在的图层）
                foreach (var b in binding.Bindings)
                {
                    if (!newLayerIds.Contains(b.LayerId))
                    {
                        binding.MarkOrphan(b.LayerId);
                    }
                }

                // 更新现有图层内容（只更新内容，不改变层级）
                var converter = new UguiConverter(settings);
                UpdateLayerContents(document.Layers, binding, bindingMap, gameObjectMap, 
                    converter, texturePath, prefabRoot.transform, document.Width, document.Height);

                // 保存孤立绑定
                var orphanBindings = new List<PsdDocumentBinding.LayerBinding>(binding.GetOrphanBindings());

                // 更新绑定信息
                binding.Initialize(psdFilePath, fileHash, document.Width, document.Height);
                
                // 重新记录所有绑定
                RecordBindings(binding, document.Layers, "");
                
                // 更新绑定路径为节点的实际位置
                UpdateBindingPaths(binding, prefabRoot.transform);
                
                // 恢复孤立绑定
                foreach (var orphan in orphanBindings)
                {
                    var existingBindingItem = binding.FindBinding(orphan.LayerId);
                    if (existingBindingItem == null)
                    {
                        binding.AddBinding(orphan.LayerId, orphan.LayerName, orphan.GameObjectPath, orphan.LayerType);
                        binding.MarkOrphan(orphan.LayerId);
                    }
                }

                // 保存 Prefab
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

                // 显示孤立节点信息
                var finalOrphans = binding.GetOrphanBindings();
                if (finalOrphans.Count > 0)
                {
                    Debug.LogWarning($"[PsdImporter] 发现 {finalOrphans.Count} 个孤立节点（PSD 中已删除的图层）");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        /// <summary>
        /// 更新图层内容（只更新内容，不改变层级结构）
        /// </summary>
        private static void UpdateLayerContents(List<PsdLayerInfo> layers, 
            PsdDocumentBinding binding, 
            Dictionary<int, PsdDocumentBinding.LayerBinding> bindingMap,
            Dictionary<int, GameObject> gameObjectMap,
            UguiConverter converter, string texturePath, 
            Transform defaultParent, float parentWidth, float parentHeight)
        {
            foreach (var layer in layers)
            {
                if (gameObjectMap.TryGetValue(layer.Id, out var existingGo))
                {
                    // 节点存在，只更新内容
                    converter.UpdateLayerContent(existingGo, layer, texturePath);
                }
                else if (!bindingMap.ContainsKey(layer.Id))
                {
                    // 新增节点，创建在默认父级下
                    var newGo = converter.CreateLayerGameObject(layer, defaultParent, texturePath, parentWidth, parentHeight);
                    if (newGo != null)
                    {
                        gameObjectMap[layer.Id] = newGo;
                    }
                }
                // 如果 bindingMap 中有但 gameObjectMap 中没有，说明节点被用户删除了，不重新创建

                // 递归处理子节点
                if (layer.Children.Count > 0)
                {
                    // 确定子节点的默认父级
                    Transform childDefaultParent = defaultParent;
                    float childParentWidth = parentWidth;
                    float childParentHeight = parentHeight;
                    
                    if (gameObjectMap.TryGetValue(layer.Id, out var parentGo))
                    {
                        childDefaultParent = parentGo.transform;
                        childParentWidth = layer.Bounds.width > 0 ? layer.Bounds.width : parentWidth;
                        childParentHeight = layer.Bounds.height > 0 ? layer.Bounds.height : parentHeight;
                    }
                    
                    UpdateLayerContents(layer.Children, binding, bindingMap, gameObjectMap, 
                        converter, texturePath, childDefaultParent, childParentWidth, childParentHeight);
                }
            }
        }

        private static void RecordBindings(PsdDocumentBinding binding, List<PsdLayerInfo> layers, string pathPrefix)
        {
            foreach (var layer in layers)
            {
                var path = string.IsNullOrEmpty(pathPrefix) ? layer.Name : $"{pathPrefix}/{layer.Name}";
                binding.AddBinding(layer.Id, layer.Name, path, (int)layer.LayerType);

                if (layer.Children.Count > 0)
                {
                    RecordBindings(binding, layer.Children, path);
                }
            }
        }

        /// <summary>
        /// 更新绑定路径（用于增量导入后，记录节点的实际位置）
        /// </summary>
        private static void UpdateBindingPaths(PsdDocumentBinding binding, Transform root)
        {
            foreach (var b in binding.GetBindingsForEditor())
            {
                var go = binding.FindGameObject(b.LayerId);
                if (go != null)
                {
                    var actualPath = GetRelativePath(root, go.transform);
                    b.GameObjectPath = actualPath;
                }
            }
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (target == root)
                return "";

            var path = target.name;
            var current = target.parent;
            
            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            
            return path;
        }

        private static void CollectLayerIds(List<PsdLayerInfo> layers, HashSet<int> ids)
        {
            foreach (var layer in layers)
            {
                ids.Add(layer.Id);
                if (layer.Children.Count > 0)
                {
                    CollectLayerIds(layer.Children, ids);
                }
            }
        }

        private static string ComputeFileHash(string filePath)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
