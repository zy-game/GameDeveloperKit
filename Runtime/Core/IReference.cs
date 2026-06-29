namespace GameDeveloperKit
{
    using System;

    /// <summary>
    /// 引用接口，定义了一个资源引用的接口，包含一个Release方法用于释放资源，并且实现了IDisposable接口以便在使用完资源后能够正确地释放它们。这种设计模式有助于管理资源的生命周期，确保在不再需要资源时能够及时释放它们，避免内存泄漏和其他资源管理问题。在实现这个接口的类中，开发者需要提供具体的释放逻辑，以确保资源能够被正确地清理和回收。
    /// </summary>
    public interface IReference : IDisposable
    {
        void Release();

        void IDisposable.Dispose()
        {
            Release();
        }
    }
}
