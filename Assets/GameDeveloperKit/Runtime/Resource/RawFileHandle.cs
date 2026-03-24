using System;

namespace GameDeveloperKit.Runtime
{
    public sealed class RawFileHandle : IDisposable
    {
        private readonly ResourcePackage.RawFileRecord _record;
        private bool _released;

        internal RawFileHandle(ResourcePackage.RawFileRecord record)
        {
            _record = record ?? throw new ArgumentNullException(nameof(record));
        }

        public string PackageName => _record.PackageName;

        public ResourceLocation Location => _record.Location.Clone();

        public byte[] Data => _record.Data;

        public string Text => _record.Text;

        public void Release()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            _record.Release();
        }

        public void Dispose()
        {
            Release();
        }
    }
}
