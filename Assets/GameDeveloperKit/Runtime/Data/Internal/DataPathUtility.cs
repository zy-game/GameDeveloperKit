using System;

namespace GameDeveloperKit.Data.Internal
{
    /// <summary>
    /// 定义 Data Path Utility 类型。
    /// </summary>
    internal static class DataPathUtility
    {
        /// <summary>
        /// 获取 Index Path。
        /// </summary>
        /// <param name="slot">slot 参数。</param>
        /// <returns>执行结果。</returns>
        public static string GetIndexPath(DataSlot slot)
        {
            return $"data/{NormalizeSegment(slot.TypeKey)}/{NormalizeSegment(slot.Key)}/index.json";
        }

        /// <summary>
        /// 获取 Version Path。
        /// </summary>
        /// <param name="slot">slot 参数。</param>
        /// <param name="version">version 参数。</param>
        /// <returns>执行结果。</returns>
        public static string GetVersionPath(DataSlot slot, string version)
        {
            return $"data/{NormalizeSegment(slot.TypeKey)}/{NormalizeSegment(slot.Key)}/versions/{NormalizeSegment(version)}.json";
        }

        /// <summary>
        /// 执行 Normalize Segment。
        /// </summary>
        /// <param name="value">value 参数。</param>
        /// <returns>执行结果。</returns>
        private static string NormalizeSegment(string value)
        {
            return Uri.EscapeDataString(value);
        }
    }
}
