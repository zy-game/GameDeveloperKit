using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using IODirectory = System.IO.Directory;
using IOPath = System.IO.Path;

namespace GameDeveloperKit.LubanConfigEditor
{
    internal sealed class LubanGenerationTransaction : IDisposable
    {
        private readonly List<OutputDirectory> m_Outputs;
        private bool m_Committed;

        private LubanGenerationTransaction(List<OutputDirectory> outputs)
        {
            m_Outputs = outputs;
        }

        public string StagingCodeDirectory => m_Outputs[0].StagingPath;

        public string StagingDataDirectory => m_Outputs[1].StagingPath;

        public static LubanGenerationTransaction Create(LubanGenerationProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var codePath = LubanCommandRunner.GetAbsoluteProjectPath(profile.OutputCodeDirectory);
            var dataPath = LubanCommandRunner.GetAbsoluteProjectPath(profile.OutputDataDirectory);
            EnsureDistinct(codePath, dataPath);
            var transactionId = Guid.NewGuid().ToString("N");
            var outputs = new List<OutputDirectory>
            {
                OutputDirectory.Create(codePath, transactionId),
                OutputDirectory.Create(dataPath, transactionId)
            };
            foreach (var output in outputs)
            {
                output.Prepare();
            }

            return new LubanGenerationTransaction(outputs);
        }

        public async UniTask<LubanRunReport> RunAsync(
            string releasePath,
            LubanWorkspaceProfile workspace,
            LubanGenerationProfile profile,
            CancellationToken cancellationToken)
        {
            var preview = LubanCommandPreview.CreateGenerate(
                releasePath,
                workspace,
                profile,
                StagingCodeDirectory,
                StagingDataDirectory);
            var report = await LubanCommandRunner.RunAsync(preview, cancellationToken);
            if (!report.Success)
            {
                return report;
            }

            try
            {
                CommitStagedOutputs();
                return report;
            }
            catch (Exception exception)
            {
                return LubanRunReport.CreateFailure(
                    report.Command,
                    report.WorkingDirectory,
                    $"Luban 输出提交失败：{exception.Message}",
                    report.StandardOutput,
                    report.StandardError,
                    report.ExitCode,
                    report.Elapsed);
            }
        }

        public void Dispose()
        {
            if (!m_Committed)
            {
                foreach (var output in m_Outputs)
                {
                    output.DeleteStaging();
                    output.RestoreBackup();
                }
            }

            foreach (var output in m_Outputs)
            {
                output.DeleteBackup();
            }
        }

        private void ValidateOutputs()
        {
            foreach (var output in m_Outputs)
            {
                if (!IODirectory.Exists(output.StagingPath) ||
                    IODirectory.EnumerateFiles(output.StagingPath, "*", SearchOption.AllDirectories).Any() is false)
                {
                    throw new InvalidOperationException($"Luban staging output is empty: {output.StagingPath}");
                }
            }
        }

        internal void CommitStagedOutputs(Action<int> beforeCommit = null)
        {
            ValidateOutputs();
            var committed = new List<OutputDirectory>();
            try
            {
                for (var i = 0; i < m_Outputs.Count; i++)
                {
                    beforeCommit?.Invoke(i);
                    var output = m_Outputs[i];
                    output.Commit();
                    committed.Add(output);
                }

                m_Committed = true;
            }
            catch
            {
                for (var i = committed.Count - 1; i >= 0; i--)
                {
                    committed[i].RollbackCommitted();
                }

                foreach (var output in m_Outputs.Except(committed))
                {
                    output.RestoreBackup();
                }

                throw;
            }
        }

        private static void EnsureDistinct(string codePath, string dataPath)
        {
            var code = NormalizeDirectory(codePath);
            var data = NormalizeDirectory(dataPath);
            if (string.Equals(code, data, StringComparison.OrdinalIgnoreCase) ||
                code.StartsWith(data + "/", StringComparison.OrdinalIgnoreCase) ||
                data.StartsWith(code + "/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Luban code and data output directories must be distinct and non-nested.");
            }
        }

        private static string NormalizeDirectory(string path)
        {
            return IOPath.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
        }

        private sealed class OutputDirectory
        {
            private OutputDirectory(string targetPath, string stagingPath, string backupPath)
            {
                TargetPath = targetPath;
                StagingPath = stagingPath;
                BackupPath = backupPath;
            }

            public string TargetPath { get; }

            public string StagingPath { get; }

            public string BackupPath { get; }

            public static OutputDirectory Create(string targetPath, string transactionId)
            {
                var absoluteTarget = IOPath.GetFullPath(targetPath);
                var parent = IOPath.GetDirectoryName(absoluteTarget) ?? throw new InvalidOperationException(
                    $"Luban output has no parent directory: {absoluteTarget}");
                var name = IOPath.GetFileName(absoluteTarget);
                return new OutputDirectory(
                    absoluteTarget,
                    IOPath.Combine(parent, $".{name}.gdk-luban-staging-{transactionId}"),
                    IOPath.Combine(parent, $".{name}.gdk-luban-backup-{transactionId}"));
            }

            public void Prepare()
            {
                DeleteDirectory(StagingPath);
                DeleteDirectory(BackupPath);
                IODirectory.CreateDirectory(StagingPath);
            }

            public void Commit()
            {
                if (IODirectory.Exists(TargetPath))
                {
                    IODirectory.Move(TargetPath, BackupPath);
                }

                try
                {
                    IODirectory.Move(StagingPath, TargetPath);
                }
                catch
                {
                    RestoreBackup();
                    throw;
                }
            }

            public void RollbackCommitted()
            {
                DeleteDirectory(TargetPath);
                RestoreBackup();
            }

            public void RestoreBackup()
            {
                if (!IODirectory.Exists(BackupPath))
                {
                    return;
                }

                DeleteDirectory(TargetPath);
                IODirectory.Move(BackupPath, TargetPath);
            }

            public void DeleteStaging()
            {
                DeleteDirectory(StagingPath);
            }

            public void DeleteBackup()
            {
                DeleteDirectory(BackupPath);
            }

            private static void DeleteDirectory(string path)
            {
                if (IODirectory.Exists(path))
                {
                    IODirectory.Delete(path, true);
                }
            }
        }
    }
}
