using System;
using System.IO;
using GameDeveloperKit.LubanConfigEditor;
using NUnit.Framework;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;

namespace GameDeveloperKit.Tests
{
    public sealed class LubanGenerationTransactionTests
    {
        private string m_Root;

        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(Path.GetTempPath(), "gdk-luban-transaction-tests", Guid.NewGuid().ToString("N"));
            IODirectory.CreateDirectory(m_Root);
        }

        [TearDown]
        public void TearDown()
        {
            if (IODirectory.Exists(m_Root))
            {
                IODirectory.Delete(m_Root, true);
            }
        }

        [Test]
        public void CommitStagedOutputs_WhenBothOutputsValid_ReplacesBothDirectories()
        {
            var profile = CreateProfile();
            IODirectory.CreateDirectory(profile.OutputCodeDirectory);
            IODirectory.CreateDirectory(profile.OutputDataDirectory);
            IOFile.WriteAllText(Path.Combine(profile.OutputCodeDirectory, "old.cs"), "old");
            IOFile.WriteAllText(Path.Combine(profile.OutputDataDirectory, "old.json"), "old");

            using (var transaction = LubanGenerationTransaction.Create(profile))
            {
                IOFile.WriteAllText(Path.Combine(transaction.StagingCodeDirectory, "new.cs"), "new");
                IOFile.WriteAllText(Path.Combine(transaction.StagingDataDirectory, "new.json"), "new");

                transaction.CommitStagedOutputs();
            }

            Assert.IsFalse(IOFile.Exists(Path.Combine(profile.OutputCodeDirectory, "old.cs")));
            Assert.IsFalse(IOFile.Exists(Path.Combine(profile.OutputDataDirectory, "old.json")));
            Assert.AreEqual("new", IOFile.ReadAllText(Path.Combine(profile.OutputCodeDirectory, "new.cs")));
            Assert.AreEqual("new", IOFile.ReadAllText(Path.Combine(profile.OutputDataDirectory, "new.json")));
        }

        [Test]
        public void CommitStagedOutputs_WhenOneOutputEmpty_PreservesBothDirectories()
        {
            var profile = CreateProfile();
            IODirectory.CreateDirectory(profile.OutputCodeDirectory);
            IODirectory.CreateDirectory(profile.OutputDataDirectory);
            IOFile.WriteAllText(Path.Combine(profile.OutputCodeDirectory, "old.cs"), "old");
            IOFile.WriteAllText(Path.Combine(profile.OutputDataDirectory, "old.json"), "old");

            using (var transaction = LubanGenerationTransaction.Create(profile))
            {
                IOFile.WriteAllText(Path.Combine(transaction.StagingCodeDirectory, "new.cs"), "new");

                Assert.Throws<InvalidOperationException>(() => transaction.CommitStagedOutputs());
            }

            Assert.AreEqual("old", IOFile.ReadAllText(Path.Combine(profile.OutputCodeDirectory, "old.cs")));
            Assert.AreEqual("old", IOFile.ReadAllText(Path.Combine(profile.OutputDataDirectory, "old.json")));
        }

        [Test]
        public void CommitStagedOutputs_WhenSecondCommitFails_RestoresFirstOutput()
        {
            var profile = CreateProfile();
            IODirectory.CreateDirectory(profile.OutputCodeDirectory);
            IODirectory.CreateDirectory(profile.OutputDataDirectory);
            IOFile.WriteAllText(Path.Combine(profile.OutputCodeDirectory, "old.cs"), "old-code");
            IOFile.WriteAllText(Path.Combine(profile.OutputDataDirectory, "old.json"), "old-data");

            using (var transaction = LubanGenerationTransaction.Create(profile))
            {
                IOFile.WriteAllText(Path.Combine(transaction.StagingCodeDirectory, "new.cs"), "new-code");
                IOFile.WriteAllText(Path.Combine(transaction.StagingDataDirectory, "new.json"), "new-data");

                Assert.Throws<IOException>(() => transaction.CommitStagedOutputs(index =>
                {
                    if (index == 1)
                    {
                        throw new IOException("Injected second output failure.");
                    }
                }));
            }

            Assert.AreEqual("old-code", IOFile.ReadAllText(Path.Combine(profile.OutputCodeDirectory, "old.cs")));
            Assert.AreEqual("old-data", IOFile.ReadAllText(Path.Combine(profile.OutputDataDirectory, "old.json")));
            Assert.IsFalse(IOFile.Exists(Path.Combine(profile.OutputCodeDirectory, "new.cs")));
            Assert.IsFalse(IOFile.Exists(Path.Combine(profile.OutputDataDirectory, "new.json")));
        }

        [Test]
        public void Create_WhenOutputsAreNested_Throws()
        {
            var profile = CreateProfile();
            profile.OutputDataDirectory = Path.Combine(profile.OutputCodeDirectory, "Data");

            Assert.Throws<ArgumentException>(() => LubanGenerationTransaction.Create(profile));
        }

        private LubanGenerationProfile CreateProfile()
        {
            var profile = new LubanGenerationProfile
            {
                OutputCodeDirectory = Path.Combine(m_Root, "Code"),
                OutputDataDirectory = Path.Combine(m_Root, "Data")
            };
            profile.EnsureDefaults();
            return profile;
        }
    }
}
