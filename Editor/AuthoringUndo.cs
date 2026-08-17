using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit
{
    internal static class AuthoringUndo
    {
        private sealed class PlainObjectUndoEntry
        {
            public PlainObjectUndoEntry(object target, string label, object before, object after)
            {
                Target = target;
                Label = label;
                Before = before;
                After = after;
            }

            public object Target { get; }

            public string Label { get; }

            public object Before { get; }

            public object After { get; }

            public bool IsReverted { get; set; }
        }

        private static readonly List<PlainObjectUndoEntry> s_PlainEntries = new List<PlainObjectUndoEntry>();

        private static readonly JsonSerializerSettings s_CloneSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        };

        private static ScriptableObject s_Anchor;

        private static bool s_Subscribed;

        private static ScriptableObject Anchor
        {
            get
            {
                if (s_Anchor == null)
                {
                    s_Anchor = ScriptableObject.CreateInstance<ScriptableObject>();
                    s_Anchor.hideFlags = HideFlags.HideAndDontSave;
                }

                return s_Anchor;
            }
        }

        private static void EnsureSubscribed()
        {
            if (s_Subscribed)
            {
                return;
            }

            Undo.undoRedoEvent += HandleUndoRedoEvent;
            Undo.willFlushUndoRecord += ClearPlainEntries;
            s_Subscribed = true;
        }

        public static void Mutate(UnityEngine.Object target, string name, Action mutation)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Undo name cannot be empty.", nameof(name));
            }

            if (mutation == null)
            {
                throw new ArgumentNullException(nameof(mutation));
            }

            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(name);
            Undo.RegisterCompleteObjectUndo(target, name);
            mutation();
            EditorUtility.SetDirty(target);
            Undo.CollapseUndoOperations(group);
        }

        /// <summary>
        /// 对纯数据对象执行带撤销的修改。通过深克隆快照 + 占位对象登记 Unity Undo 条目，
        /// 撤销/重做时按操作名匹配快照并恢复。
        /// </summary>
        public static void Mutate(object target, string name, Action mutation)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Undo name cannot be empty.", nameof(name));
            }

            if (mutation == null)
            {
                throw new ArgumentNullException(nameof(mutation));
            }

            EnsureSubscribed();
            var before = DeepClone(target);
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(name);
            mutation();
            Undo.RegisterCompleteObjectUndo(Anchor, name);
            Undo.CollapseUndoOperations(group);
            s_PlainEntries.Add(new PlainObjectUndoEntry(target, name, before, DeepClone(target)));
        }

        public static void Record(UnityEngine.Object target, string name)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            Undo.RecordObject(target, name);
        }

        private static void HandleUndoRedoEvent(in UndoRedoInfo info)
        {
            var undoName = info.undoName;
            var isRedo = info.isRedo;
            if (string.IsNullOrEmpty(undoName))
            {
                return;
            }

            if (isRedo)
            {
                var entry = FindLast(candidate =>
                    string.Equals(candidate.Label, undoName, StringComparison.Ordinal) && candidate.IsReverted);
                if (entry == null)
                {
                    return;
                }

                Apply(entry.After, entry.Target);
                entry.IsReverted = false;
                return;
            }

            var undone = FindLast(candidate =>
                string.Equals(candidate.Label, undoName, StringComparison.Ordinal) && candidate.IsReverted is false);
            if (undone == null)
            {
                return;
            }

            Apply(undone.Before, undone.Target);
            undone.IsReverted = true;
        }

        private static PlainObjectUndoEntry FindLast(Func<PlainObjectUndoEntry, bool> predicate)
        {
            for (var i = s_PlainEntries.Count - 1; i >= 0; i--)
            {
                if (predicate(s_PlainEntries[i]))
                {
                    return s_PlainEntries[i];
                }
            }

            return null;
        }

        private static object DeepClone(object source)
        {
            var json = JsonConvert.SerializeObject(source, s_CloneSettings);
            return JsonConvert.DeserializeObject(json, s_CloneSettings);
        }

        private static void Apply(object source, object target)
        {
            foreach (var field in target.GetType().GetFields(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.IsInitOnly)
                {
                    continue;
                }

                field.SetValue(target, field.GetValue(source));
            }
        }

        private static void ClearPlainEntries()
        {
            s_PlainEntries.Clear();
        }
    }
}
