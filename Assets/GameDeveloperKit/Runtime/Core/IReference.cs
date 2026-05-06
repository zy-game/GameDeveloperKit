namespace GameDeveloperKit
{
    using System;

    public interface IReference : IDisposable
    {
        void Release();

        void IDisposable.Dispose()
        {
            Release();
        }
    }
}
