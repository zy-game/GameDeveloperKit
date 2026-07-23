using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.StoryEditor.Authoring;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Excel;
using GameDeveloperKit.StoryEditor.Media;
using GameDeveloperKit.StoryEditor.Migration;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryVolumeAssetTests
    {
        private readonly List<UnityEngine.Object> m_Objects = new List<UnityEngine.Object>();
        private string m_TestFolder;

        [SetUp]
        public void SetUp()
        {
            m_TestFolder = $"Assets/__StoryVolumeAssetTests_{Guid.NewGuid():N}";
            AssetDatabase.CreateFolder("Assets", m_TestFolder.Substring("Assets/".Length));
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < m_Objects.Count; i++)
            {
                if (m_Objects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(m_Objects[i]);
                }
            }

            m_Objects.Clear();
            Undo.ClearAll();
            if (string.IsNullOrWhiteSpace(m_TestFolder) is false)
            {
                AssetDatabase.DeleteAsset(m_TestFolder);
            }
        }

        [Test]
        public void CreateProjectAtPath_CreatesRootAndIndependentFirstVolume()
        {
            var projectPath = $"{m_TestFolder}/Story.asset";

            var project = AuthoringAssetStore.CreateProjectAtPath(projectPath);

            Assert.IsNotNull(project);
            Assert.AreEqual(projectPath, AssetDatabase.GetAssetPath(project));
            Assert.AreEqual(1, project.VolumeAssets.Count);
            Assert.IsFalse(project.HasEmbeddedVolumes);
            var volumePath = AssetDatabase.GetAssetPath(project.VolumeAssets[0]);
            StringAssert.StartsWith($"{m_TestFolder}/Story.Volumes/", volumePath);
            Assert.AreNotEqual(projectPath, volumePath);
        }

        [Test]
        public void CreateDefault_CreatesOneValidVolumeRoute()
        {
            var volume = Track(AuthoringVolumeAsset.CreateDefault("volume_a", "第一卷"));

            Assert.AreEqual("volume_a", volume.Volume.VolumeId);
            Assert.AreEqual(1, volume.Volume.Episodes.Count);
            Assert.AreEqual(1, volume.Volume.Route.Edges.Count);
            Assert.AreEqual(volume.Volume.Episodes[0].EpisodeId, volume.Volume.Route.Edges[0].ToEpisodeId);
        }

        [Test]
        public void ProjectMutation_AddMoveRemove_PreservesExplicitOrder()
        {
            var project = Track(ScriptableObject.CreateInstance<AuthoringAsset>());
            var first = Track(AuthoringVolumeAsset.CreateDefault("volume_a", "第一卷"));
            var second = Track(AuthoringVolumeAsset.CreateDefault("volume_b", "第二卷"));
            var mutation = new AuthoringProjectMutation(project);

            Assert.IsTrue(mutation.TryAdd(first, out var addFirstError), addFirstError);
            Assert.IsTrue(mutation.TryAdd(second, out var addSecondError), addSecondError);
            Assert.IsTrue(mutation.TryMove(second, 0, out var moveError), moveError);
            Assert.AreSame(second, project.VolumeAssets[0]);
            Assert.IsTrue(mutation.TryRemove(first, out var removeError), removeError);
            Assert.AreEqual(1, project.VolumeAssets.Count);
        }

        [Test]
        public void ProjectMutation_AddVolumeOwnedByAnotherProject_RejectsWithoutChanges()
        {
            var borrowed = CreateVolumeAsset("Borrowed.asset", "volume_borrowed");
            var owner = CreateProjectAsset("Owner.asset", "story_owner", borrowed);
            var targetVolume = CreateVolumeAsset("TargetVolume.asset", "volume_target");
            var target = CreateProjectAsset("Target.asset", "story_target", targetVolume);
            var ownerBefore = ReadAssetContents(owner);
            var targetBefore = ReadAssetContents(target);
            var borrowedBefore = ReadAssetContents(borrowed);

            var added = new AuthoringProjectMutation(target).TryAdd(borrowed, out var error);

            Assert.IsFalse(added);
            StringAssert.Contains("another story project", error);
            CollectionAssert.AreEqual(new[] { borrowed }, owner.VolumeAssets);
            CollectionAssert.AreEqual(new[] { targetVolume }, target.VolumeAssets);
            Assert.AreEqual(ownerBefore, ReadAssetContents(owner));
            Assert.AreEqual(targetBefore, ReadAssetContents(target));
            Assert.AreEqual(borrowedBefore, ReadAssetContents(borrowed));
        }

        [Test]
        public void ResolveOwner_WhenVolumeIsOrphaned_ReturnsStablePathError()
        {
            var volume = CreateVolumeAsset("Orphan.asset", "volume_orphan");

            var resolved = AuthoringProjectResolver.TryResolveOwner(volume, out var owner, out var error);

            Assert.IsFalse(resolved);
            Assert.IsNull(owner);
            StringAssert.Contains("not referenced", error);
            StringAssert.Contains(AssetDatabase.GetAssetPath(volume), error);
        }

        [Test]
        public void ResolveOwner_WhenVolumeHasMultipleOwners_ListsBothProjects()
        {
            var volume = CreateVolumeAsset("Shared.asset", "volume_shared");
            var first = CreateProjectAsset("First.asset", "story_first", volume);
            var second = CreateProjectAsset("Second.asset", "story_second", volume);

            var resolved = AuthoringProjectResolver.TryResolveOwner(volume, out var owner, out var error);

            Assert.IsFalse(resolved);
            Assert.IsNull(owner);
            StringAssert.Contains("multiple story projects", error);
            StringAssert.Contains(AssetDatabase.GetAssetPath(first), error);
            StringAssert.Contains(AssetDatabase.GetAssetPath(second), error);
        }

        [Test]
        public void UpdateAndSaveVolume_DirtiesOnlyVolumeAsset()
        {
            var volume = CreateVolumeAsset("Volume.asset", "volume_a");
            var project = CreateProjectAsset("Story.asset", "story", volume);
            AssetDatabase.SaveAssets();

            var mutation = new RouteMutation(project);
            var result = mutation.UpdateVolume(
                volume.Volume.VolumeId,
                new VolumeMetadata("更新后的卷", "卷描述", null));

            Assert.IsTrue(result.Succeeded, result.Message);
            Assert.IsTrue(EditorUtility.IsDirty(volume));
            Assert.IsFalse(EditorUtility.IsDirty(project));

            AuthoringAssetStore.Save(volume);

            Assert.IsFalse(EditorUtility.IsDirty(volume));
            Assert.IsFalse(EditorUtility.IsDirty(project));
        }

        [Test]
        public void UpdateAndSaveVolume_ChangesOnlySelectedVolumeFile()
        {
            var first = CreateVolumeAsset("VolumeA.asset", "volume_a");
            var second = CreateVolumeAsset("VolumeB.asset", "volume_b");
            var project = CreateProjectAsset("Story.asset", "story", first);
            project.ReplaceVolumeAssets(new[] { first, second });
            var programAsset = ScriptableObject.CreateInstance<ProgramAsset>();
            var programPath = $"{m_TestFolder}/StoryProgram.asset";
            AssetDatabase.CreateAsset(programAsset, programPath);
            project.RuntimeProgramAssetPath = programPath;
            EditorUtility.SetDirty(project);
            AssetDatabase.SaveAssets();

            var projectBefore = ReadAssetContents(project);
            var firstBefore = ReadAssetContents(first);
            var secondBefore = ReadAssetContents(second);
            var programBefore = ReadAssetContents(programAsset);

            var mutation = new RouteMutation(project);
            var result = mutation.UpdateVolume(
                first.Volume.VolumeId,
                new VolumeMetadata("更新后的第一卷", "仅修改卷 A", null));
            Assert.IsTrue(result.Succeeded, result.Message);

            AuthoringAssetStore.Save(first);

            Assert.AreNotEqual(firstBefore, ReadAssetContents(first));
            Assert.AreEqual(projectBefore, ReadAssetContents(project));
            Assert.AreEqual(secondBefore, ReadAssetContents(second));
            Assert.AreEqual(programBefore, ReadAssetContents(programAsset));
        }

        [Test]
        public void SplitMigration_ApplyPreservesVolumeContentAndProjectGuid()
        {
            var project = CreateLegacyProject("Legacy.asset");
            var projectPath = AssetDatabase.GetAssetPath(project);
            var projectGuid = AssetDatabase.AssetPathToGUID(projectPath);
            var volumeJson = new List<string>();
            for (var i = 0; i < project.EmbeddedVolumes.Count; i++)
            {
                volumeJson.Add(JsonUtility.ToJson(project.EmbeddedVolumes[i]));
            }

            var result = AssetSplitMigrationService.Apply(project);

            Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));
            Assert.IsFalse(result.IsNoOp);
            Assert.IsFalse(project.HasEmbeddedVolumes);
            Assert.AreEqual(2, project.VolumeAssets.Count);
            Assert.AreEqual(projectGuid, AssetDatabase.AssetPathToGUID(projectPath));
            for (var i = 0; i < project.VolumeAssets.Count; i++)
            {
                Assert.AreEqual(volumeJson[i], JsonUtility.ToJson(project.VolumeAssets[i].Volume));
                Assert.IsFalse(string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(project.VolumeAssets[i])));
            }

            var program = ProgramCompiler.Compile(project, out var report);
            Assert.IsNotNull(program);
            Assert.IsFalse(report.HasErrors, report.Issues.Count == 0 ? string.Empty : report.Issues[0].Message);
        }

        [Test]
        public void SplitMigration_AnalyzePathConflict_DoesNotModifyProjectOrExistingAsset()
        {
            var project = CreateLegacyProject("Conflict.asset");
            var projectJson = EditorJsonUtility.ToJson(project);
            var folder = $"{m_TestFolder}/Conflict.Volumes";
            AssetDatabase.CreateFolder(m_TestFolder, "Conflict.Volumes");
            var occupied = AuthoringVolumeAsset.CreateDefault("occupied", "occupied");
            var occupiedPath = $"{folder}/01_{project.EmbeddedVolumes[0].VolumeId}.asset";
            AssetDatabase.CreateAsset(occupied, occupiedPath);

            var result = AssetSplitMigrationService.Analyze(project);

            Assert.IsTrue(result.HasErrors);
            StringAssert.Contains("already occupied", result.Errors[0]);
            Assert.AreEqual(projectJson, EditorJsonUtility.ToJson(project));
            Assert.AreSame(occupied, AssetDatabase.LoadAssetAtPath<AuthoringVolumeAsset>(occupiedPath));
        }

        [Test]
        public void SplitMigration_WhenSecondVolumeCreationFails_RollsBackAllAssets()
        {
            var project = CreateLegacyProject("Rollback.asset");
            var projectJson = EditorJsonUtility.ToJson(project);
            var analysis = AssetSplitMigrationService.Analyze(project);
            Assert.IsFalse(analysis.HasErrors, string.Join("\n", analysis.Errors));

            var result = AssetSplitMigrationService.Apply(
                project,
                index =>
                {
                    if (index == 1)
                    {
                        throw new InvalidOperationException("Injected migration failure.");
                    }
                });

            Assert.IsTrue(result.HasErrors);
            StringAssert.Contains("Injected migration failure", result.Errors[0]);
            Assert.AreEqual(projectJson, EditorJsonUtility.ToJson(project));
            Assert.IsTrue(project.HasEmbeddedVolumes);
            Assert.AreEqual(0, project.VolumeAssets.Count);
            for (var i = 0; i < analysis.Candidates.Count; i++)
            {
                Assert.IsNull(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(analysis.Candidates[i].AssetPath));
            }

            Assert.IsFalse(AssetDatabase.IsValidFolder($"{m_TestFolder}/Rollback.Volumes"));
        }

        [Test]
        public void SplitMigration_WhenAlreadySplit_ReturnsNoOpWithoutChanges()
        {
            var project = CreateLegacyProject("NoOp.asset");
            var first = AssetSplitMigrationService.Apply(project);
            Assert.IsFalse(first.HasErrors, string.Join("\n", first.Errors));
            var projectJson = EditorJsonUtility.ToJson(project);
            var paths = new List<string>();
            for (var i = 0; i < project.VolumeAssets.Count; i++)
            {
                paths.Add(AssetDatabase.GetAssetPath(project.VolumeAssets[i]));
            }

            var second = AssetSplitMigrationService.Apply(project);

            Assert.IsFalse(second.HasErrors, string.Join("\n", second.Errors));
            Assert.IsTrue(second.IsNoOp);
            Assert.AreEqual(0, second.CreatedAssetPaths.Count);
            Assert.AreEqual(projectJson, EditorJsonUtility.ToJson(project));
            for (var i = 0; i < paths.Count; i++)
            {
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<AuthoringVolumeAsset>(paths[i]));
            }
        }

        [Test]
        public void ImportNewProject_FromMultiVolumeWorkbook_CreatesIndependentVolumeAssets()
        {
            var workbookPath = Path.Combine(Path.GetTempPath(), $"story-volume-import-{Guid.NewGuid():N}.xlsx");
            try
            {
                var source = Track(SampleGraphFixture.Create());
                Exporter.Export(source, workbookPath);

                var project = Importer.ImportNewProject(
                    workbookPath,
                    $"{m_TestFolder}/Imported.asset",
                    out var report);

                Assert.IsNotNull(project);
                Assert.IsFalse(report.HasErrors, report.Issues.Count == 0 ? string.Empty : report.Issues[0].Message);
                Assert.IsFalse(project.HasEmbeddedVolumes);
                Assert.AreEqual(2, project.VolumeAssets.Count);
                Assert.AreEqual(SampleGraphFixture.PrimaryVolumeId, project.VolumeAssets[0].Volume.VolumeId);
                Assert.AreEqual(SampleGraphFixture.SecondaryVolumeId, project.VolumeAssets[1].Volume.VolumeId);
                for (var i = 0; i < project.VolumeAssets.Count; i++)
                {
                    StringAssert.StartsWith(
                        $"{m_TestFolder}/Imported.Volumes/",
                        AssetDatabase.GetAssetPath(project.VolumeAssets[i]));
                }

                var firstVolumeAsset = project.VolumeAssets[0];
                var secondVolumeAsset = project.VolumeAssets[1];
                firstVolumeAsset.Volume.Title = "changed";
                var existingImport = Importer.Import(workbookPath, project);
                Assert.IsFalse(
                    existingImport.HasErrors,
                    existingImport.Issues.Count == 0 ? string.Empty : existingImport.Issues[0].Message);
                Assert.AreSame(firstVolumeAsset, project.VolumeAssets[0]);
                Assert.AreSame(secondVolumeAsset, project.VolumeAssets[1]);
                Assert.AreEqual("第一卷：乡村少年", firstVolumeAsset.Volume.Title);

                var program = ProgramCompiler.Compile(project, out var compileReport);
                Assert.IsNotNull(program);
                Assert.IsFalse(compileReport.HasErrors, compileReport.Issues.Count == 0 ? string.Empty : compileReport.Issues[0].Message);
                Assert.AreEqual(2, program.Volumes.Count);

                var sourceProgram = ProgramCompiler.Compile(source, out var sourceReport);
                Assert.IsNotNull(sourceProgram);
                Assert.IsFalse(sourceReport.HasErrors, sourceReport.Issues.Count == 0 ? string.Empty : sourceReport.Issues[0].Message);
                var sourceProgramAsset = Track(ScriptableObject.CreateInstance<ProgramAsset>());
                var importedProgramAsset = Track(ScriptableObject.CreateInstance<ProgramAsset>());
                sourceProgramAsset.SetProgram(sourceProgram);
                importedProgramAsset.SetProgram(program);
                Assert.AreEqual(
                    EditorJsonUtility.ToJson(sourceProgramAsset),
                    EditorJsonUtility.ToJson(importedProgramAsset));
            }
            finally
            {
                if (System.IO.File.Exists(workbookPath))
                {
                    System.IO.File.Delete(workbookPath);
                }
            }
        }

        [Test]
        public void ImportExistingProject_WhenWorkbookOmitsVolume_LeavesAllAssetFilesUnchanged()
        {
            var workbookPath = Path.Combine(Path.GetTempPath(), $"story-volume-mismatch-{Guid.NewGuid():N}.xlsx");
            try
            {
                var source = Track(SampleGraphFixture.Create());
                source.Volumes.RemoveAt(1);
                Exporter.Export(source, workbookPath);

                var first = CreateVolumeAsset("ImportVolumeA.asset", SampleGraphFixture.PrimaryVolumeId);
                var second = CreateVolumeAsset("ImportVolumeB.asset", SampleGraphFixture.SecondaryVolumeId);
                var project = CreateProjectAsset("ImportStory.asset", source.StoryId, first);
                project.ReplaceVolumeAssets(new[] { first, second });
                EditorUtility.SetDirty(project);
                AssetDatabase.SaveAssets();
                var projectBefore = ReadAssetContents(project);
                var firstBefore = ReadAssetContents(first);
                var secondBefore = ReadAssetContents(second);

                var report = Importer.Import(workbookPath, project);

                Assert.IsTrue(report.HasErrors);
                StringAssert.Contains(
                    "Imported VolumeId set must match the story project",
                    string.Join("\n", report.Issues));
                Assert.AreEqual(projectBefore, ReadAssetContents(project));
                Assert.AreEqual(firstBefore, ReadAssetContents(first));
                Assert.AreEqual(secondBefore, ReadAssetContents(second));
            }
            finally
            {
                if (System.IO.File.Exists(workbookPath))
                {
                    System.IO.File.Delete(workbookPath);
                }
            }
        }

        [Test]
        public void ImportNewProject_WhenVolumePathIsOccupied_LeavesNoPartialProject()
        {
            var workbookPath = Path.Combine(Path.GetTempPath(), $"story-volume-create-failure-{Guid.NewGuid():N}.xlsx");
            try
            {
                var source = Track(SampleGraphFixture.Create());
                Exporter.Export(source, workbookPath);
                var projectPath = $"{m_TestFolder}/FailedImport.asset";
                var volumeFolder = $"{m_TestFolder}/FailedImport.Volumes";
                AssetDatabase.CreateFolder(m_TestFolder, "FailedImport.Volumes");
                var occupiedPath = $"{volumeFolder}/02_{SampleGraphFixture.SecondaryVolumeId}.asset";
                var occupied = AuthoringVolumeAsset.CreateDefault("occupied", "occupied");
                AssetDatabase.CreateAsset(occupied, occupiedPath);
                AssetDatabase.SaveAssets();

                var project = Importer.ImportNewProject(workbookPath, projectPath, out var report);

                Assert.IsNull(project);
                Assert.IsTrue(report.HasErrors);
                StringAssert.Contains("already occupied", string.Join("\n", report.Issues));
                Assert.IsNull(AssetDatabase.LoadAssetAtPath<AuthoringAsset>(projectPath));
                Assert.IsNull(AssetDatabase.LoadAssetAtPath<AuthoringVolumeAsset>(
                    $"{volumeFolder}/01_{SampleGraphFixture.PrimaryVolumeId}.asset"));
                Assert.AreSame(occupied, AssetDatabase.LoadAssetAtPath<AuthoringVolumeAsset>(occupiedPath));
            }
            finally
            {
                if (System.IO.File.Exists(workbookPath))
                {
                    System.IO.File.Delete(workbookPath);
                }
            }
        }

        [Test]
        public void UsageIndex_ForSplitProject_ReportsProjectAndVolumeLocation()
        {
            var reference = new VideoReference(
                new MediaReference(MediaKind.Video, MediaSource.StreamingAssets, null, "story/intro.mp4"),
                VideoFormat.Mp4);
            var volume = CreateVolumeAsset("UsageVolume.asset", "volume_usage");
            var video = new AuthoringNode
            {
                NodeId = "video",
                NodeKind = NodeKind.PlayVideo,
                Title = "Intro video"
            };
            video.Parameters.Add(new AuthoringParameter
            {
                Key = MediaCommandNames.ClipArgument,
                Value = VideoReferenceCodec.Serialize(reference)
            });
            volume.Volume.Episodes[0].Nodes.Add(video);
            var project = CreateProjectAsset("UsageProject.asset", "story_usage", volume);
            var projectPath = AssetDatabase.GetAssetPath(project);
            var volumePath = AssetDatabase.GetAssetPath(volume);
            var index = new UsageIndex(() => new[] { (projectPath, project) });

            index.Rebuild();
            var usages = index.Find(reference.Primary);

            Assert.AreEqual(1, usages.Count);
            Assert.AreEqual(projectPath, usages[0].ProjectAssetPath);
            Assert.AreEqual(volumePath, usages[0].VolumeAssetPath);
            Assert.AreEqual(volumePath, usages[0].AssetPath);
            Assert.AreEqual("story_usage", usages[0].StoryId);
            Assert.AreEqual("volume_usage", usages[0].VolumeId);
            Assert.AreEqual(volume.Volume.Episodes[0].EpisodeId, usages[0].EpisodeId);
            Assert.AreEqual("video", usages[0].NodeId);
        }

        [Test]
        public void CompileProject_UsesVolumeAssetReferenceOrder()
        {
            var project = Track(ScriptableObject.CreateInstance<AuthoringAsset>());
            project.StoryId = "story";
            project.Version = "1.0.0";
            var first = Track(AuthoringVolumeAsset.CreateDefault("volume_a", "第一卷"));
            var second = Track(AuthoringVolumeAsset.CreateDefault("volume_b", "第二卷"));
            project.ReplaceVolumeAssets(new[] { second, first });

            var program = ProgramCompiler.Compile(project, out var report);

            Assert.IsFalse(report.HasErrors, report.Issues.Count == 0 ? string.Empty : report.Issues[0].Message);
            Assert.IsNotNull(program);
            Assert.AreEqual("volume_b", program.Volumes[0].VolumeId);
            Assert.AreEqual("volume_a", program.Volumes[1].VolumeId);

            var repeated = ProgramCompiler.Compile(project, out var repeatedReport);
            Assert.IsNotNull(repeated);
            Assert.IsFalse(repeatedReport.HasErrors, repeatedReport.Issues.Count == 0 ? string.Empty : repeatedReport.Issues[0].Message);
            CollectionAssert.AreEqual(
                program.Volumes.Select(x => x.VolumeId),
                repeated.Volumes.Select(x => x.VolumeId));

            var workbookPath = Path.Combine(Path.GetTempPath(), $"story-volume-order-{Guid.NewGuid():N}.xlsx");
            try
            {
                Exporter.Export(project, workbookPath);
                var workbookReport = new ValidationReport();
                var sheets = Importer.ReadWorkbook(workbookPath, workbookReport);
                Assert.IsFalse(workbookReport.HasErrors);
                Assert.AreEqual("volume_b", sheets["VolumeDefine"].Cell(0, "VolumeId"));
                Assert.AreEqual("volume_a", sheets["VolumeDefine"].Cell(1, "VolumeId"));
            }
            finally
            {
                if (System.IO.File.Exists(workbookPath))
                {
                    System.IO.File.Delete(workbookPath);
                }
            }
        }

        [Test]
        public void PublishSplitProject_WhenVolumeRemovalIsRejected_PreservesPublishedOutputsAndBaseline()
        {
            var first = CreateVolumeAsset("PublishedVolumeA.asset", "volume_a");
            var second = CreateVolumeAsset("PublishedVolumeB.asset", "volume_b");
            var project = CreateProjectAsset("PublishedStory.asset", "story_publish", first);
            project.ReplaceVolumeAssets(new[] { first, second });
            var programPath = $"{m_TestFolder}/PublishedStory.runtime.asset";
            var manifestPath = $"{m_TestFolder}/PublishedStory.runtime.identity-manifest.json";
            var changePath = $"{m_TestFolder}/PublishedStory.runtime.identity-change.json";
            project.RuntimeProgramAssetPath = programPath;
            EditorUtility.SetDirty(project);
            AssetDatabase.SaveAssets();
            var firstBefore = ReadAssetContents(first);
            var secondBefore = ReadAssetContents(second);

            var initialProgram = ProgramCompiler.Compile(project, out var initialReport);
            Assert.IsNotNull(initialProgram);
            Assert.IsFalse(initialReport.HasErrors, initialReport.Issues.Count == 0 ? string.Empty : initialReport.Issues[0].Message);
            var published = ProgramAssetExporter.ExportCompiled(project, initialProgram, _ => true);

            Assert.IsTrue(published.Exported);
            Assert.AreEqual(2, AssetDatabase.LoadAssetAtPath<ProgramAsset>(programPath).ToProgram().Volumes.Count);
            Assert.IsTrue(System.IO.File.Exists(manifestPath));
            Assert.IsTrue(System.IO.File.Exists(changePath));
            Assert.IsTrue(project.TryGetPublishedIdentity(out var initialBaseline, out var initialBaselineError), initialBaselineError);
            Assert.AreEqual(2, initialBaseline.EpisodeIds.Count);
            Assert.AreEqual(firstBefore, ReadAssetContents(first));
            Assert.AreEqual(secondBefore, ReadAssetContents(second));
            var programBefore = ReadAssetContents(AssetDatabase.LoadAssetAtPath<ProgramAsset>(programPath));
            var manifestBefore = System.IO.File.ReadAllText(manifestPath);
            var changeBefore = System.IO.File.ReadAllText(changePath);

            Assert.IsTrue(new AuthoringProjectMutation(project).TryRemove(second, out var removeError), removeError);
            AuthoringAssetStore.Save(project);
            var reducedProgram = ProgramCompiler.Compile(project, out var reducedReport);
            Assert.IsNotNull(reducedProgram);
            Assert.IsFalse(reducedReport.HasErrors);
            var rejected = ProgramAssetExporter.ExportCompiled(project, reducedProgram, _ => false);

            Assert.IsTrue(rejected.Canceled);
            CollectionAssert.AreEqual(new[] { first }, project.VolumeAssets);
            Assert.AreEqual(programBefore, ReadAssetContents(AssetDatabase.LoadAssetAtPath<ProgramAsset>(programPath)));
            Assert.AreEqual(manifestBefore, System.IO.File.ReadAllText(manifestPath));
            Assert.AreEqual(changeBefore, System.IO.File.ReadAllText(changePath));
            Assert.IsTrue(project.TryGetPublishedIdentity(out var rejectedBaseline, out var rejectedBaselineError), rejectedBaselineError);
            Assert.AreEqual(2, rejectedBaseline.EpisodeIds.Count);
            Assert.AreEqual(firstBefore, ReadAssetContents(first));
            Assert.AreEqual(secondBefore, ReadAssetContents(second));
        }

        [Test]
        public void CompileVolume_DoesNotReadInvalidSiblingVolume()
        {
            var project = Track(ScriptableObject.CreateInstance<AuthoringAsset>());
            project.StoryId = "story";
            project.Version = "1.0.0";
            var valid = Track(AuthoringVolumeAsset.CreateDefault("volume_a", "第一卷"));
            var invalid = Track(AuthoringVolumeAsset.CreateDefault("volume_b", "第二卷"));
            invalid.Volume.Route = null;
            project.ReplaceVolumeAssets(new[] { valid, invalid });

            var volume = ProgramCompiler.CompileVolume(project, valid, out var report);

            Assert.IsFalse(report.HasErrors, report.Issues.Count == 0 ? string.Empty : report.Issues[0].Message);
            Assert.IsNotNull(volume);
            Assert.AreEqual("volume_a", volume.VolumeId);
        }

        [Test]
        public void CompileVolume_WhenIdentitiesConflictWithSibling_ReportsAllKindsAndBothAssetPaths()
        {
            var first = CreateVolumeAsset("FirstVolume.asset", "volume_a");
            var second = CreateVolumeAsset("SecondVolume.asset", "volume_b");
            second.Volume.VolumeId = first.Volume.VolumeId;
            second.Volume.Episodes[0].EpisodeId = first.Volume.Episodes[0].EpisodeId;
            second.Volume.Route.Edges[0].EdgeId = first.Volume.Route.Edges[0].EdgeId;
            var project = ScriptableObject.CreateInstance<AuthoringAsset>();
            project.StoryId = "story";
            project.ReplaceVolumeAssets(new[] { first, second });
            AssetDatabase.CreateAsset(project, $"{m_TestFolder}/StoryWithConflict.asset");
            AssetDatabase.SaveAssets();

            var volume = ProgramCompiler.CompileVolume(project, first, out var report);

            Assert.IsNull(volume);
            Assert.IsTrue(report.HasErrors);
            var message = string.Join("\n", report.Issues);
            StringAssert.Contains("Duplicate volume id", message);
            StringAssert.Contains("Duplicate episode id", message);
            StringAssert.Contains("Duplicate route edge id", message);
            StringAssert.Contains(AssetDatabase.GetAssetPath(first), message);
            StringAssert.Contains(AssetDatabase.GetAssetPath(second), message);
        }

        [Test]
        public void CompileProject_WithNullDuplicateAndInvalidVolumeIds_FailsWithoutWrites()
        {
            var first = CreateVolumeAsset("ValidReference.asset", "volume_valid");
            var empty = CreateVolumeAsset("EmptyId.asset", string.Empty);
            var duplicateA = CreateVolumeAsset("DuplicateA.asset", "volume_duplicate");
            var duplicateB = CreateVolumeAsset("DuplicateB.asset", "volume_duplicate");
            var project = CreateProjectAsset("InvalidReferences.asset", "story_invalid_references", first);
            project.ReplaceVolumeAssets(new[] { first, null, first, empty, duplicateA, duplicateB });
            EditorUtility.SetDirty(project);
            AssetDatabase.SaveAssets();
            var projectBefore = ReadAssetContents(project);
            var volumeBefore = new[] { first, empty, duplicateA, duplicateB }
                .Select(ReadAssetContents)
                .ToArray();

            var program = ProgramCompiler.Compile(project, out var report);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors);
            var message = string.Join("\n", report.Issues);
            StringAssert.Contains("reference cannot be null", message);
            StringAssert.Contains("reference cannot be duplicated", message);
            StringAssert.Contains("Volume id cannot be empty", message);
            StringAssert.Contains("Duplicate volume id", message);
            Assert.AreEqual(projectBefore, ReadAssetContents(project));
            CollectionAssert.AreEqual(
                volumeBefore,
                new[] { first, empty, duplicateA, duplicateB }.Select(ReadAssetContents).ToArray());
        }

        [Test]
        public void CompileVolume_WhenRouteTargetsSiblingEpisode_ReportsUnknownEpisode()
        {
            var first = CreateVolumeAsset("CrossVolumeA.asset", "volume_a");
            var second = CreateVolumeAsset("CrossVolumeB.asset", "volume_b");
            first.Volume.Route.Edges[0].ToEpisodeId = second.Volume.Episodes[0].EpisodeId;
            var project = ScriptableObject.CreateInstance<AuthoringAsset>();
            project.StoryId = "story_cross_volume";
            project.ReplaceVolumeAssets(new[] { first, second });
            AssetDatabase.CreateAsset(project, $"{m_TestFolder}/CrossVolumeStory.asset");
            AssetDatabase.SaveAssets();

            var volume = ProgramCompiler.CompileVolume(project, first, out var report);

            Assert.IsNull(volume);
            Assert.IsTrue(report.HasErrors);
            StringAssert.Contains("Route target Episode does not exist", string.Join("\n", report.Issues));
            StringAssert.Contains(second.Volume.Episodes[0].EpisodeId, string.Join("\n", report.Issues));
        }

        [Test]
        public void CompileProject_WhenVolumeHasMultipleOwners_ReportsBothProjectPaths()
        {
            var volume = CreateVolumeAsset("SharedCompile.asset", "volume_shared");
            var first = CreateProjectAsset("FirstCompile.asset", "story_first", volume);
            var second = CreateProjectAsset("SecondCompile.asset", "story_second", volume);

            var program = ProgramCompiler.Compile(first, out var report);

            Assert.IsNull(program);
            Assert.IsTrue(report.HasErrors);
            var message = string.Join("\n", report.Issues);
            StringAssert.Contains("multiple story projects", message);
            StringAssert.Contains(AssetDatabase.GetAssetPath(first), message);
            StringAssert.Contains(AssetDatabase.GetAssetPath(second), message);
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            m_Objects.Add(value);
            return value;
        }

        private AuthoringVolumeAsset CreateVolumeAsset(string fileName, string volumeId)
        {
            var volume = AuthoringVolumeAsset.CreateDefault(volumeId, volumeId);
            AssetDatabase.CreateAsset(volume, $"{m_TestFolder}/{fileName}");
            return volume;
        }

        private AuthoringAsset CreateProjectAsset(
            string fileName,
            string storyId,
            AuthoringVolumeAsset volume)
        {
            var project = ScriptableObject.CreateInstance<AuthoringAsset>();
            project.StoryId = storyId;
            project.ReplaceVolumeAssets(new[] { volume });
            AssetDatabase.CreateAsset(project, $"{m_TestFolder}/{fileName}");
            AssetDatabase.SaveAssets();
            return project;
        }

        private AuthoringAsset CreateLegacyProject(string fileName)
        {
            var project = SampleGraphFixture.Create();
            AssetDatabase.CreateAsset(project, $"{m_TestFolder}/{fileName}");
            AssetDatabase.SaveAssets();
            return project;
        }

        private static string ReadAssetContents(UnityEngine.Object asset)
        {
            return System.IO.File.ReadAllText(Path.GetFullPath(AssetDatabase.GetAssetPath(asset)));
        }
    }
}
