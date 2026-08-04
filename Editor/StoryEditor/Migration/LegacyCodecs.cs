using GameDeveloperKit.Story.Authoring;

namespace GameDeveloperKit.StoryEditor.Migration
{
    internal static class LegacyNodeKinds
    {
        public const int JumpEpisode = 2;
        public const int MiniGame = 204;
        public const int Qte = 205;
        public const int Unlock = 206;
        public const int SettleEpisode = 207;
        public const int TargetEpisode = 1;

        public static bool RequiresManualLogicReplacement(NodeKind kind)
        {
            var value = (int)kind;
            return value == MiniGame || value == Qte || value == Unlock || value == SettleEpisode;
        }
    }
}
