using System.Linq;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Playback;
using NUnit.Framework;
using UnityEditor;
using StoryProgram = GameDeveloperKit.Story.Model.Program;

namespace GameDeveloperKit.Tests
{
    /// <summary>
    /// 用真实 new_story 数据验证预热图解析（无网络，纯数据链路）。
    /// 当前结构：入口集(playvideo→playvideo_2→transition) → 选项集(playvideo_2→parallel→playvideo循环→选项)。
    /// </summary>
    public sealed class StoryPrewarmDataTests
    {
        private const string AssetPath = "Assets/Bundles/Story/Data/new_story.asset";
        private const string VolumeId = "6690b85cb3cf4dce8c07321a5bd14dfe";
        private const string EntryEpisodeId = "ad3ebd69f2f941a1a64a1fa5873e8f4d";
        private const string ChoiceEpisodeId = "0749a1f1774248c9af2cd42f19beab84";

        [Test]
        public void NewStory_NextVideo_ResolvesSecondVideoInSameEpisode()
        {
            var program = LoadProgram();
            var volume = program.Volumes.First(value => value.VolumeId == VolumeId);
            var episode = volume.Episodes.First(value => value.EpisodeId == EntryEpisodeId);

            var runner = new Runner(program, null);
            var frame = runner.Start(volume.VolumeId, episode.EpisodeId);
            var firstVideo = frame.Instructions.OfType<StoryInstruction.PlayVideo>().FirstOrDefault();
            Assert.IsNotNull(firstVideo, "入口集初始帧应包含视频指令。");

            var next = EpisodeVideoPrewarmer.FindNextVideoInstruction(frame, firstVideo.Step, null);

            Assert.IsNotNull(next, "同一集内应解析出第二个视频。");
            Assert.AreEqual("videos/media-06cf36d961524599/master.m3u8", next.Reference.Primary.Value);
        }

        [Test]
        public void NewStory_NextVideo_CrossEpisode_ResolvesChoiceEpisodeFirstVideo()
        {
            var program = LoadProgram();
            var volume = program.Volumes.First(value => value.VolumeId == VolumeId);
            var episode = volume.Episodes.First(value => value.EpisodeId == EntryEpisodeId);

            var runner = new Runner(program, null);
            var frame = runner.Start(volume.VolumeId, episode.EpisodeId);
            var secondVideoStep = episode.Steps.FirstOrDefault(value => value?.Kind == StepKind.PlayVideo && value.StepId != "playvideo");
            Assert.IsNotNull(secondVideoStep, "入口集应包含第二个视频步骤。");

            var next = EpisodeVideoPrewarmer.FindNextVideoInstruction(frame, secondVideoStep, null);

            Assert.IsNotNull(next, "过渡后应跨集解析出选项集第一个视频。");
            Assert.AreEqual("videos/media-d670128643824732/master.m3u8", next.Reference.Primary.Value);
        }

        [Test]
        public void NewStory_NextVideo_ThroughParallel_ResolvesLoopVideo()
        {
            var program = LoadProgram();
            var volume = program.Volumes.First(value => value.VolumeId == VolumeId);
            var episode = volume.Episodes.First(value => value.EpisodeId == ChoiceEpisodeId);

            var runner = new Runner(program, null);
            var frame = runner.Start(volume.VolumeId, episode.EpisodeId);
            var storyVideo = frame.Instructions.OfType<StoryInstruction.PlayVideo>().FirstOrDefault();
            Assert.IsNotNull(storyVideo, "选项集初始帧应包含正常剧情视频指令。");
            Assert.AreEqual("videos/media-d670128643824732/master.m3u8", storyVideo.Reference.Primary.Value);

            var loopVideo = EpisodeVideoPrewarmer.FindNextVideoInstruction(frame, storyVideo.Step, null);

            Assert.IsNotNull(loopVideo, "正常剧情视频后应经 parallel 分支解析出循环视频。");
            Assert.AreEqual("videos/media-729629d8b25649e9/master.m3u8", loopVideo.Reference.Primary.Value);
        }

        [Test]
        public void NewStory_ChoicePrewarm_ResolvesAllBranchVideos()
        {
            var program = LoadProgram();
            var volume = program.Volumes.First(value => value.VolumeId == VolumeId);

            var runner = new Runner(program, null);
            var frame = runner.Start(volume.VolumeId, ChoiceEpisodeId);
            var instructions = EpisodeVideoPrewarmer.CollectChoiceVideoInstructions(frame, null);

            Assert.AreEqual(3, instructions.Count, "选项集应有 3 个选项视频。");
            var paths = instructions
                .Select(value => value.Reference.Primary.Value)
                .ToHashSet();
            Assert.IsTrue(paths.Contains("videos/media-11f4e121b0ae4cb2/master.m3u8"));
            Assert.IsTrue(paths.Contains("videos/media-531ec6880d664347/master.m3u8"));
            Assert.IsTrue(paths.Contains("videos/media-fc000b67049244e1/master.m3u8"));
        }

        [Test]
        public void NewStory_ChoicePrewarm_EachChoiceResolvesItsVideo()
        {
            var program = LoadProgram();
            var volume = program.Volumes.First(value => value.VolumeId == VolumeId);
            var choiceEpisode = volume.Episodes.First(value => value.EpisodeId == ChoiceEpisodeId);

            var choiceStep = choiceEpisode.Steps.FirstOrDefault(value => value?.Kind == StepKind.Choice);
            Assert.IsNotNull(choiceStep, "选项集应包含选项步骤。");
            Assert.AreEqual(3, choiceStep.Choices.Count);

            var runner = new Runner(program, null);
            var frame = runner.Start(volume.VolumeId, ChoiceEpisodeId);
            var collected = EpisodeVideoPrewarmer.CollectChoiceVideoInstructions(frame, null);
            Assert.AreEqual(3, collected.Count);
            for (var i = 0; i < choiceStep.Choices.Count; i++)
            {
                Assert.IsTrue(
                    collected.Any(value => value.Reference.Primary.Value.EndsWith("/" + GetChoiceVideoId(choiceStep.Choices[i].ExitId) + "/master.m3u8")),
                    $"选项 {choiceStep.Choices[i].ChoiceId} 应解析出视频。");
            }
        }

        private static string GetChoiceVideoId(string exitId)
        {
            return exitId switch
            {
                "aed7b2335da048898506761a69e559e4" => "media-11f4e121b0ae4cb2",
                "33a84435daa248ba87b3c2c1a01a4637" => "media-531ec6880d664347",
                "4cb2ee9418cd4cfe950309171bee084c" => "media-fc000b67049244e1",
                _ => exitId
            };
        }

        private static StoryProgram LoadProgram()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ProgramAsset>(AssetPath);
            Assert.IsNotNull(asset, $"缺少剧情数据资产: {AssetPath}");
            return asset.ToProgram();
        }
    }
}
