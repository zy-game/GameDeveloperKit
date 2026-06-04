using System;

namespace GameDeveloperKit.Data.Internal
{
    internal static class DataPathUtility
    {
        public static string GetIndexPath(DataSlot slot)
        {
            return $"data/{NormalizeSegment(slot.TypeKey)}/{NormalizeSegment(slot.Key)}/index.json";
        }

        public static string GetVersionPath(DataSlot slot, string version)
        {
            return $"data/{NormalizeSegment(slot.TypeKey)}/{NormalizeSegment(slot.Key)}/versions/{NormalizeSegment(version)}.json";
        }

        private static string NormalizeSegment(string value)
        {
            return Uri.EscapeDataString(value);
        }
    }
}
