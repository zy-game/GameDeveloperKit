using System;

namespace GameDeveloperKit.Data.Internal
{
    internal static class DataPathUtility
    {
        /// <summary>
        /// 获取 Index Path。
        /// </summary>
        public static string GetIndexPath(DataSlot slot)
        {
            return $"data/{NormalizeSegment(slot.TypeKey)}/{NormalizeSegment(slot.Key)}/index.json";
        }

        /// <summary>
        /// 获取 Version Path。
        /// </summary>
        public static string GetVersionPath(DataSlot slot, string version)
        {
            return $"data/{NormalizeSegment(slot.TypeKey)}/{NormalizeSegment(slot.Key)}/versions/{NormalizeSegment(version)}.json";
        }

        /// <summary>
        /// 执行 Normalize Segment。
        /// </summary>
        private static string NormalizeSegment(string value)
        {
            return Uri.EscapeDataString(value);
        }
    }
}
