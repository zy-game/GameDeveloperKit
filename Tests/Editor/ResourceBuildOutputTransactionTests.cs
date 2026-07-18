using System;
using System.IO;
using GameDeveloperKit.ResourceEditor;
using Newtonsoft.Json;
using NUnit.Framework;
using IOFile = System.IO.File;

namespace GameDeveloperKit.Tests
{
    public sealed class ResourceBuildOutputTransactionTests
    {
        [SetUp]
        public void SetUp()
        {
            DeleteJournal();
        }

        [TearDown]
        public void TearDown()
        {
            DeleteJournal();
        }

        [Test]
        public void Commit_WhenLaterOutputLocked_RestoresAllPreviousOutputs()
        {
            var root = CreateTestRoot();
            var firstTarget = Path.Combine(root, "first.txt");
            var secondTarget = Path.Combine(root, "second.txt");
            try
            {
                IOFile.WriteAllText(firstTarget, "old-first");
                IOFile.WriteAllText(secondTarget, "old-second");
                using (var transaction = GameDeveloperKit.ResourceEditor.Build.OutputTransaction.Begin())
                {
                    var firstStaging = transaction.StageFile(firstTarget);
                    var secondStaging = transaction.StageFile(secondTarget);
                    IOFile.WriteAllText(firstStaging, "new-first");
                    IOFile.WriteAllText(secondStaging, "new-second");

                    using (new FileStream(secondTarget, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        Assert.Throws<IOException>(() => transaction.Commit());
                    }
                }

                Assert.AreEqual("old-first", IOFile.ReadAllText(firstTarget));
                Assert.AreEqual("old-second", IOFile.ReadAllText(secondTarget));
                Assert.IsFalse(IOFile.Exists(GameDeveloperKit.ResourceEditor.Build.OutputTransaction.GetStagingPath(firstTarget)));
                Assert.IsFalse(IOFile.Exists(GameDeveloperKit.ResourceEditor.Build.OutputTransaction.GetBackupPath(firstTarget)));
                Assert.IsFalse(IOFile.Exists(GameDeveloperKit.ResourceEditor.Build.OutputTransaction.GetStagingPath(secondTarget)));
                Assert.IsFalse(IOFile.Exists(GameDeveloperKit.ResourceEditor.Build.OutputTransaction.GetBackupPath(secondTarget)));
                Assert.IsFalse(IOFile.Exists(Path.GetFullPath(GameDeveloperKit.ResourceEditor.Build.OutputTransaction.JournalPath)));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void RecoverPending_WhenPreparedCommitInterrupted_RestoresLastKnownGoodOutputs()
        {
            var root = CreateTestRoot();
            var committedTarget = Path.Combine(root, "committed.txt");
            var untouchedTarget = Path.Combine(root, "untouched.txt");
            var committedStaging = GameDeveloperKit.ResourceEditor.Build.OutputTransaction.GetStagingPath(committedTarget);
            var committedBackup = GameDeveloperKit.ResourceEditor.Build.OutputTransaction.GetBackupPath(committedTarget);
            var untouchedStaging = GameDeveloperKit.ResourceEditor.Build.OutputTransaction.GetStagingPath(untouchedTarget);
            var untouchedBackup = GameDeveloperKit.ResourceEditor.Build.OutputTransaction.GetBackupPath(untouchedTarget);
            try
            {
                IOFile.WriteAllText(committedTarget, "new-committed");
                IOFile.WriteAllText(committedBackup, "old-committed");
                IOFile.WriteAllText(untouchedTarget, "old-untouched");
                IOFile.WriteAllText(untouchedStaging, "new-untouched");
                WriteJournal(
                    "prepared",
                    CreateRecord(committedTarget, committedStaging, committedBackup, true),
                    CreateRecord(untouchedTarget, untouchedStaging, untouchedBackup, true));

                GameDeveloperKit.ResourceEditor.Build.OutputTransaction.RecoverPending();

                Assert.AreEqual("old-committed", IOFile.ReadAllText(committedTarget));
                Assert.AreEqual("old-untouched", IOFile.ReadAllText(untouchedTarget));
                Assert.IsFalse(IOFile.Exists(committedStaging));
                Assert.IsFalse(IOFile.Exists(committedBackup));
                Assert.IsFalse(IOFile.Exists(untouchedStaging));
                Assert.IsFalse(IOFile.Exists(untouchedBackup));
                Assert.IsFalse(IOFile.Exists(Path.GetFullPath(GameDeveloperKit.ResourceEditor.Build.OutputTransaction.JournalPath)));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void RecoverPending_WhenCommitRecorded_KeepsNewOutputAndRemovesBackup()
        {
            var root = CreateTestRoot();
            var target = Path.Combine(root, "committed.txt");
            var staging = GameDeveloperKit.ResourceEditor.Build.OutputTransaction.GetStagingPath(target);
            var backup = GameDeveloperKit.ResourceEditor.Build.OutputTransaction.GetBackupPath(target);
            try
            {
                IOFile.WriteAllText(target, "new");
                IOFile.WriteAllText(backup, "old");
                WriteJournal("committed", CreateRecord(target, staging, backup, true));

                GameDeveloperKit.ResourceEditor.Build.OutputTransaction.RecoverPending();

                Assert.AreEqual("new", IOFile.ReadAllText(target));
                Assert.IsFalse(IOFile.Exists(backup));
                Assert.IsFalse(IOFile.Exists(Path.GetFullPath(GameDeveloperKit.ResourceEditor.Build.OutputTransaction.JournalPath)));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void StageFile_WhenTargetInsideStagedDirectory_RejectsOverlappingOutputs()
        {
            var root = CreateTestRoot();
            var targetDirectory = Path.Combine(root, "output");
            try
            {
                using (var transaction = GameDeveloperKit.ResourceEditor.Build.OutputTransaction.Begin())
                {
                    transaction.StageDirectory(targetDirectory, false);

                    var exception = Assert.Throws<InvalidOperationException>(() =>
                        transaction.StageFile(Path.Combine(targetDirectory, "manifest.json")));

                    StringAssert.Contains("targets overlap", exception.Message);
                    Assert.IsFalse(Directory.Exists(targetDirectory));
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string CreateTestRoot()
        {
            var root = Path.GetFullPath(Path.Combine(
                "Temp/GameDeveloperKit.ResourceEditor.Build.OutputTransactionTests",
                Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            return root;
        }

        private static object CreateRecord(
            string targetPath,
            string stagingPath,
            string backupPath,
            bool hadTarget)
        {
            return new
            {
                TargetPath = targetPath.Replace('\\', '/'),
                StagingPath = stagingPath.Replace('\\', '/'),
                BackupPath = backupPath.Replace('\\', '/'),
                IsDirectory = false,
                HadTarget = hadTarget
            };
        }

        private static void WriteJournal(string state, params object[] entries)
        {
            var path = Path.GetFullPath(GameDeveloperKit.ResourceEditor.Build.OutputTransaction.JournalPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            IOFile.WriteAllText(path, JsonConvert.SerializeObject(new { State = state, Entries = entries }));
        }

        private static void DeleteJournal()
        {
            var path = Path.GetFullPath(GameDeveloperKit.ResourceEditor.Build.OutputTransaction.JournalPath);
            if (IOFile.Exists(path))
            {
                IOFile.Delete(path);
            }

            if (IOFile.Exists(path + ".tmp"))
            {
                IOFile.Delete(path + ".tmp");
            }
        }
    }
}
