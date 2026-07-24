using System;
using System.Collections.Generic;
using System.IO;

namespace GameDeveloperKit.MediaEditor
{
    internal sealed class HlsOutputLease : IDisposable
    {
        private static readonly object s_Lock = new object();
        private static readonly HashSet<string> s_Targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly string m_Target;
        private bool m_Disposed;

        private HlsOutputLease(string target)
        {
            m_Target = target;
        }

        public static HlsOutputLease Acquire(string targetDirectory)
        {
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                throw new ArgumentException("Target directory cannot be empty.", nameof(targetDirectory));
            }

            var target = Path.GetFullPath(targetDirectory);
            lock (s_Lock)
            {
                if (s_Targets.Add(target) is false)
                {
                    throw new InvalidOperationException($"另一个 HLS 转码任务正在写入：{target}");
                }
            }

            return new HlsOutputLease(target);
        }

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            lock (s_Lock)
            {
                s_Targets.Remove(m_Target);
            }

            m_Disposed = true;
        }
    }
}
