using System;
using UnityEngine;

namespace GameDeveloperKit
{
    /// <summary>
    /// Disposable扩展方法
    /// </summary>
    public static class DisposableExtensions
    {
        /// <summary>
        /// 将订阅添加到CompositeDisposable
        /// </summary>
        public static T AddTo<T>(this T disposable, CompositeDisposable composite) where T : IDisposable
        {
            composite?.Add(disposable);
            return disposable;
        }

        /// <summary>
        /// 将订阅绑定到GameObject生命周期
        /// </summary>
        public static T AddTo<T>(this T disposable, GameObject gameObject) where T : IDisposable
        {
            if (gameObject == null)
            {
                disposable?.Dispose();
                return disposable;
            }

            var trigger = gameObject.GetComponent<DisposableTrigger>();
            if (trigger == null)
                trigger = gameObject.AddComponent<DisposableTrigger>();
            
            trigger.Add(disposable);
            return disposable;
        }

        /// <summary>
        /// 将订阅绑定到Component所在GameObject的生命周期
        /// </summary>
        public static T AddTo<T>(this T disposable, Component component) where T : IDisposable
        {
            return disposable.AddTo(component?.gameObject);
        }
    }

    /// <summary>
    /// 用于在GameObject销毁时自动释放订阅的组件
    /// </summary>
    internal class DisposableTrigger : MonoBehaviour
    {
        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        public void Add(IDisposable disposable)
        {
            _disposables.Add(disposable);
        }

        private void OnDestroy()
        {
            _disposables.Dispose();
        }
    }
}
