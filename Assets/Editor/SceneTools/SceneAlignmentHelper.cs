using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.SceneTools
{
    /// <summary>
    /// Scene视图对齐工具，提供多物体对齐功能
    /// </summary>
    public static class SceneAlignmentHelper
    {
        public enum AlignmentType
        {
            Left,
            Center,
            Right
        }

        /// <summary>
        /// 检查是否可以执行对齐操作（需要选中2个及以上物体）
        /// </summary>
        public static bool CanAlign()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length >= 2;
        }

        /// <summary>
        /// 执行对齐操作
        /// </summary>
        public static void Align(AlignmentType alignmentType)
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length < 2)
                return;

            Undo.RecordObjects(GetTransforms(selectedObjects), $"Align {alignmentType}");

            switch (alignmentType)
            {
                case AlignmentType.Left:
                    AlignLeft(selectedObjects);
                    break;
                case AlignmentType.Center:
                    AlignCenter(selectedObjects);
                    break;
                case AlignmentType.Right:
                    AlignRight(selectedObjects);
                    break;
            }
        }

        private static Transform[] GetTransforms(GameObject[] objects)
        {
            var transforms = new Transform[objects.Length];
            for (int i = 0; i < objects.Length; i++)
            {
                transforms[i] = objects[i].transform;
            }
            return transforms;
        }

        /// <summary>
        /// 左对齐：以最左边物体的左边界为基准
        /// </summary>
        private static void AlignLeft(GameObject[] objects)
        {
            float minX = float.MaxValue;

            // 找到最左边的边界
            foreach (var obj in objects)
            {
                var bounds = GetObjectBounds(obj);
                if (bounds.min.x < minX)
                {
                    minX = bounds.min.x;
                }
            }

            // 对齐所有物体的左边界
            foreach (var obj in objects)
            {
                var bounds = GetObjectBounds(obj);
                var offset = minX - bounds.min.x;
                var pos = obj.transform.position;
                pos.x += offset;
                obj.transform.position = pos;
            }
        }

        /// <summary>
        /// 居中对齐：以所有物体中心点的平均值为基准
        /// </summary>
        private static void AlignCenter(GameObject[] objects)
        {
            float sumCenterX = 0f;

            // 计算所有物体中心点的平均X值
            foreach (var obj in objects)
            {
                var bounds = GetObjectBounds(obj);
                sumCenterX += bounds.center.x;
            }

            float avgCenterX = sumCenterX / objects.Length;

            // 对齐所有物体的中心点
            foreach (var obj in objects)
            {
                var bounds = GetObjectBounds(obj);
                var offset = avgCenterX - bounds.center.x;
                var pos = obj.transform.position;
                pos.x += offset;
                obj.transform.position = pos;
            }
        }

        /// <summary>
        /// 右对齐：以最右边物体的右边界为基准
        /// </summary>
        private static void AlignRight(GameObject[] objects)
        {
            float maxX = float.MinValue;

            // 找到最右边的边界
            foreach (var obj in objects)
            {
                var bounds = GetObjectBounds(obj);
                if (bounds.max.x > maxX)
                {
                    maxX = bounds.max.x;
                }
            }

            // 对齐所有物体的右边界
            foreach (var obj in objects)
            {
                var bounds = GetObjectBounds(obj);
                var offset = maxX - bounds.max.x;
                var pos = obj.transform.position;
                pos.x += offset;
                obj.transform.position = pos;
            }
        }

        /// <summary>
        /// 获取物体的边界框
        /// </summary>
        private static Bounds GetObjectBounds(GameObject obj)
        {
            // 优先使用RectTransform（UI物体）
            var rectTransform = obj.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                var corners = new Vector3[4];
                rectTransform.GetWorldCorners(corners);
                var bounds = new Bounds(corners[0], Vector3.zero);
                for (int i = 1; i < 4; i++)
                {
                    bounds.Encapsulate(corners[i]);
                }
                return bounds;
            }

            // 尝试使用Renderer
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                return renderer.bounds;
            }

            // 尝试使用Collider
            var collider = obj.GetComponent<Collider>();
            if (collider != null)
            {
                return collider.bounds;
            }

            // 默认使用位置点作为边界
            return new Bounds(obj.transform.position, Vector3.zero);
        }
    }
}
