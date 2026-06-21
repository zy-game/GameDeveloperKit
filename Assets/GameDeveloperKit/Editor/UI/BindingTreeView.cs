using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace GameDeveloperKit.UIEditor
{
    /// <summary>
    /// UIDocument 绑定目标树视图。
    /// </summary>
    internal sealed class BindingTreeView : TreeView
    {
        /// <summary>
        /// 定义 Component Column Width 常量。
        /// </summary>
        private const float ComponentColumnWidth = 150f;

        /// <summary>
        /// 存储 Entries。
        /// </summary>
        private List<BindingTreeEntry> m_Entries = new List<BindingTreeEntry>();
        /// <summary>
        /// 存储 Current Selection Mapping Index。
        /// </summary>
        private int m_CurrentSelectionMappingIndex = -1;

        /// <summary>
        /// 初始化 Binding Tree View。
        /// </summary>
        /// <param name="state">state 参数。</param>
        public BindingTreeView(TreeViewState state) : base(state)
        {
            showBorder = true;
            showAlternatingRowBackgrounds = false;
            rowHeight = 20f;
            Reload();
        }

        /// <summary>
        /// 定义 Mapping Selection Changed 事件。
        /// </summary>
        public event Action<int> MappingSelectionChanged;

        /// <summary>
        /// 定义 Component Dropdown Requested 事件。
        /// </summary>
        public event Action<Rect, int> ComponentDropdownRequested;

        /// <summary>
        /// 定义 Component Label Requested 事件。
        /// </summary>
        public event Func<int, string> ComponentLabelRequested;

        /// <summary>
        /// 设置 Entries。
        /// </summary>
        /// <param name="entries">entries 参数。</param>
        public void SetEntries(List<BindingTreeEntry> entries)
        {
            m_Entries = entries ?? new List<BindingTreeEntry>();
            Reload();
        }

        /// <summary>
        /// 设置选中 Mapping。
        /// </summary>
        /// <param name="mappingIndex">mapping Index 参数。</param>
        public void SetSelectedMappingIndex(int mappingIndex)
        {
            if (m_CurrentSelectionMappingIndex == mappingIndex)
            {
                return;
            }

            m_CurrentSelectionMappingIndex = mappingIndex;
            if (mappingIndex < 0)
            {
                SetSelection(Array.Empty<int>());
                return;
            }

            SetSelection(new[] { GetItemId(mappingIndex) }, TreeViewSelectionOptions.RevealAndFrame);
        }

        /// <summary>
        /// 构建 Root。
        /// </summary>
        /// <returns>执行结果。</returns>
        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root", children = new List<TreeViewItem>() };
            var itemLookup = new Dictionary<GameObject, BindingTreeItem>();
            var items = new List<BindingTreeItem>();
            foreach (var entry in m_Entries)
            {
                var item = new BindingTreeItem(GetItemId(entry.MappingIndex), 0, GetDisplayName(entry), entry);
                items.Add(item);
                if (entry.Target != null && itemLookup.ContainsKey(entry.Target) is false)
                {
                    itemLookup.Add(entry.Target, item);
                }
            }

            foreach (var item in items)
            {
                var parent = item.Entry.Target == null ? null : FindNearestBoundParent(item.Entry.Target.transform.parent, itemLookup);
                if (parent == null)
                {
                    root.AddChild(item);
                }
                else
                {
                    parent.AddChild(item);
                }
            }

            SetupDepthsFromParentsAndChildren(root);
            return root;
        }

        /// <summary>
        /// 执行 Row GUI。
        /// </summary>
        /// <param name="args">args 参数。</param>
        protected override void RowGUI(RowGUIArgs args)
        {
            var item = (BindingTreeItem)args.item;
            var rowRect = args.rowRect;
            var componentRect = new Rect(rowRect.xMax - ComponentColumnWidth, rowRect.y + 1f, ComponentColumnWidth - 4f, rowRect.height - 2f);
            args.rowRect = new Rect(rowRect.x, rowRect.y, Mathf.Max(20f, rowRect.width - ComponentColumnWidth - 8f), rowRect.height);
            base.RowGUI(args);

            using (new EditorGUI.DisabledScope(item.Entry.Target == null))
            {
                if (GUI.Button(componentRect, ComponentLabelRequested?.Invoke(item.Entry.MappingIndex) ?? "Nothing", EditorStyles.popup))
                {
                    ComponentDropdownRequested?.Invoke(componentRect, item.Entry.MappingIndex);
                }
            }
        }

        /// <summary>
        /// 执行 Selection Changed。
        /// </summary>
        /// <param name="selectedIds">selected Ids 参数。</param>
        protected override void SelectionChanged(IList<int> selectedIds)
        {
            if (selectedIds == null || selectedIds.Count == 0)
            {
                MappingSelectionChanged?.Invoke(-1);
                return;
            }

            var selected = FindItem(selectedIds[0], rootItem) as BindingTreeItem;
            m_CurrentSelectionMappingIndex = selected?.Entry.MappingIndex ?? -1;
            MappingSelectionChanged?.Invoke(m_CurrentSelectionMappingIndex);
        }

        /// <summary>
        /// 查找 Nearest Bound Parent。
        /// </summary>
        /// <param name="transform">transform 参数。</param>
        /// <param name="itemLookup">item Lookup 参数。</param>
        /// <returns>执行结果。</returns>
        private static BindingTreeItem FindNearestBoundParent(Transform transform, Dictionary<GameObject, BindingTreeItem> itemLookup)
        {
            var cursor = transform;
            while (cursor != null)
            {
                if (itemLookup.TryGetValue(cursor.gameObject, out var item))
                {
                    return item;
                }

                cursor = cursor.parent;
            }

            return null;
        }

        /// <summary>
        /// 获取 Item Id。
        /// </summary>
        /// <param name="mappingIndex">mapping Index 参数。</param>
        /// <returns>执行结果。</returns>
        private static int GetItemId(int mappingIndex)
        {
            return mappingIndex + 1;
        }

        /// <summary>
        /// 获取 Display Name。
        /// </summary>
        /// <param name="entry">entry 参数。</param>
        /// <returns>执行结果。</returns>
        private static string GetDisplayName(BindingTreeEntry entry)
        {
            if (entry.Target != null)
            {
                return entry.Target.name;
            }

            return string.IsNullOrWhiteSpace(entry.MappingName) ? "(New Binding)" : entry.MappingName + " (Missing Target)";
        }

        /// <summary>
        /// 定义 Binding Tree Entry 类型。
        /// </summary>
        internal readonly struct BindingTreeEntry
        {
            /// <summary>
            /// 初始化 Binding Tree Entry。
            /// </summary>
            /// <param name="mappingIndex">mapping Index 参数。</param>
            /// <param name="mappingName">mapping Name 参数。</param>
            /// <param name="entry">entry 参数。</param>
            public BindingTreeEntry(int mappingIndex, string mappingName, GameObject target)
            {
                MappingIndex = mappingIndex;
                MappingName = mappingName;
                Target = target;
            }

            /// <summary>
            /// Mapping 索引。
            /// </summary>
            public int MappingIndex { get; }

            /// <summary>
            /// Mapping 名称。
            /// </summary>
            public string MappingName { get; }

            /// <summary>
            /// 目标对象。
            /// </summary>
            public GameObject Target { get; }
        }

        /// <summary>
        /// 定义 Binding Tree Item 类型。
        /// </summary>
        private sealed class BindingTreeItem : TreeViewItem
        {
            /// <summary>
            /// 初始化 Binding Tree Item。
            /// </summary>
            /// <param name="id">id 参数。</param>
            /// <param name="depth">depth 参数。</param>
            /// <param name="displayName">display Name 参数。</param>
            /// <param name="target">target 参数。</param>
            public BindingTreeItem(int id, int depth, string displayName, BindingTreeEntry entry) : base(id, depth, displayName)
            {
                Entry = entry;
            }

            /// <summary>
            /// 存储 Entry。
            /// </summary>
            public BindingTreeEntry Entry { get; }
        }
    }
}
