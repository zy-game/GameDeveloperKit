using System;
using System.Collections.Generic;
using System.ComponentModel;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Procedure
{
    /// <summary>
    /// 顶层流程基类。
    /// </summary>
    public abstract class ProcedureBase : IReference
    {
        /// <summary>
        /// 初始化流程实例。
        /// </summary>
        /// <returns>初始化任务。</returns>
        public virtual UniTask OnInitializeAsync()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 进入当前流程。
        /// </summary>
        /// <param name="previous">上一个流程。</param>
        /// <param name="userData">切换参数。</param>
        /// <returns>进入任务。</returns>
        public virtual UniTask OnEnterAsync(ProcedureBase previous, object userData)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 离开当前流程。
        /// </summary>
        /// <param name="next">下一个流程。</param>
        /// <param name="userData">切换参数。</param>
        /// <returns>离开任务。</returns>
        public virtual UniTask OnLeaveAsync(ProcedureBase next, object userData)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 更新当前流程。
        /// </summary>
        /// <param name="deltaTime">当前帧间隔。</param>
        /// <param name="unscaledDeltaTime">未缩放帧间隔。</param>
        public virtual void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
        }

        /// <summary>
        /// 释放流程实例。
        /// </summary>
        public virtual void Release()
        {
        }
    }

    public static class ProcedureRegistry
    {
        private static readonly object s_Lock = new object();
        private static readonly Dictionary<Type, Func<ProcedureBase>> s_Factories =
            new Dictionary<Type, Func<ProcedureBase>>();
        private static readonly Dictionary<string, Type> s_TypesByName =
            new Dictionary<string, Type>(StringComparer.Ordinal);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void RegisterGenerated<TProcedure>()
            where TProcedure : ProcedureBase, new()
        {
            var procedureType = typeof(TProcedure);
            var assemblyQualifiedName = procedureType.AssemblyQualifiedName;
            if (string.IsNullOrWhiteSpace(assemblyQualifiedName))
            {
                throw new GameException($"Procedure type '{procedureType.FullName}' has no assembly-qualified name.");
            }

            lock (s_Lock)
            {
                s_Factories[procedureType] = static () => new TProcedure();
                s_TypesByName[assemblyQualifiedName] = procedureType;
            }
        }

        internal static ProcedureBase Create(Type procedureType)
        {
            if (procedureType == null)
            {
                throw new ArgumentNullException(nameof(procedureType));
            }

            Func<ProcedureBase> factory;
            lock (s_Lock)
            {
                if (!s_Factories.TryGetValue(procedureType, out factory))
                {
                    throw new GameException(
                        $"Procedure '{procedureType.FullName}' has no generated factory. " +
                        "Use a public parameterless constructor or register an instance explicitly.");
                }
            }

            return factory();
        }

        internal static Type Resolve(string assemblyQualifiedName)
        {
            if (string.IsNullOrWhiteSpace(assemblyQualifiedName))
            {
                throw new ArgumentException("Procedure type name cannot be empty.", nameof(assemblyQualifiedName));
            }

            lock (s_Lock)
            {
                if (s_TypesByName.TryGetValue(assemblyQualifiedName, out var procedureType))
                {
                    return procedureType;
                }
            }

            throw new GameException(
                $"Procedure type '{assemblyQualifiedName}' has no generated registration.");
        }
    }
}
