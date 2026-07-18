using System;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit
{
    internal static class AuthoringUndo
    {
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

        public static void Record(UnityEngine.Object target, string name)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            Undo.RecordObject(target, name);
        }
    }
}
