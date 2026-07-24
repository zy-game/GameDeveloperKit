using System;
using System.IO;
using GameDeveloperKit.MediaEditor;
using NUnit.Framework;
using IOFile = System.IO.File;

namespace GameDeveloperKit.Tests
{
    public sealed class HlsOutputTransactionTests
    {
        private string m_Root;
        private string m_Target;
        private string m_Working;

        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(Path.GetTempPath(), "gdk-hls-transaction-" + Guid.NewGuid().ToString("N"));
            m_Target = Path.Combine(m_Root, "Assets", "StreamingAssets", "videos", "intro");
            m_Working = Path.Combine(m_Root, "Library", "job");
            Directory.CreateDirectory(m_Target);
            IOFile.WriteAllText(Path.Combine(m_Target, "old.txt"), "old");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_Root))
            {
                Directory.Delete(m_Root, true);
            }
        }

        [Test]
        public void Commit_WhenTargetExists_ReplacesWholeDirectory()
        {
            using (var transaction = new HlsOutputTransaction(
                       m_Target,
                       m_Working,
                       true,
                       new DirectoryOperations()))
            {
                IOFile.WriteAllText(Path.Combine(transaction.StagingDirectory, "master.m3u8"), "new");
                transaction.Commit();
            }

            Assert.IsFalse(IOFile.Exists(Path.Combine(m_Target, "old.txt")));
            Assert.IsTrue(IOFile.Exists(Path.Combine(m_Target, "master.m3u8")));
            Assert.IsFalse(Directory.Exists(m_Working));
        }

        [Test]
        public void Commit_WhenStagingMoveFails_RestoresPreviousTarget()
        {
            var operations = new FailingMoveDirectoryOperations(2);
            using (var transaction = new HlsOutputTransaction(
                       m_Target,
                       m_Working,
                       true,
                       operations))
            {
                IOFile.WriteAllText(Path.Combine(transaction.StagingDirectory, "master.m3u8"), "new");

                Assert.Throws<IOException>(() => transaction.Commit());
            }

            Assert.IsTrue(IOFile.Exists(Path.Combine(m_Target, "old.txt")));
            Assert.IsFalse(IOFile.Exists(Path.Combine(m_Target, "master.m3u8")));
            Assert.IsFalse(Directory.Exists(m_Working));
        }

        [Test]
        public void Acquire_WhenTargetIsAlreadyLeased_RejectsSecondLease()
        {
            using (HlsOutputLease.Acquire(m_Target))
            {
                Assert.Throws<InvalidOperationException>(() => HlsOutputLease.Acquire(m_Target));
            }

            using (HlsOutputLease.Acquire(m_Target))
            {
                Assert.Pass();
            }
        }

        private class DirectoryOperations : IHlsDirectoryOperations
        {
            public bool Exists(string path)
            {
                return Directory.Exists(path);
            }

            public void Create(string path)
            {
                Directory.CreateDirectory(path);
            }

            public virtual void Move(string source, string destination)
            {
                Directory.Move(source, destination);
            }

            public void Delete(string path)
            {
                Directory.Delete(path, true);
            }
        }

        private sealed class FailingMoveDirectoryOperations : DirectoryOperations
        {
            private readonly int m_FailingMove;
            private int m_MoveCount;

            public FailingMoveDirectoryOperations(int failingMove)
            {
                m_FailingMove = failingMove;
            }

            public override void Move(string source, string destination)
            {
                m_MoveCount++;
                if (m_MoveCount == m_FailingMove)
                {
                    throw new IOException("Injected move failure.");
                }

                base.Move(source, destination);
            }
        }
    }
}
