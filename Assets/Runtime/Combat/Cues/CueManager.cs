using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 表现通知类型
    /// </summary>
    public enum CueNotifyType
    {
        /// <summary>
        /// 一次性执行
        /// </summary>
        Execute,

        /// <summary>
        /// 开始（持续性）
        /// </summary>
        Add,

        /// <summary>
        /// 结束（持续性）
        /// </summary>
        Remove
    }

    /// <summary>
    /// 表现通知
    /// </summary>
    public struct CueNotify
    {
        /// <summary>
        /// 触发的标签
        /// </summary>
        public GameplayTag CueTag;

        /// <summary>
        /// 通知类型
        /// </summary>
        public CueNotifyType Type;

        /// <summary>
        /// 来源对象
        /// </summary>
        public object Source;

        /// <summary>
        /// 目标对象
        /// </summary>
        public object Target;

        /// <summary>
        /// 数值幅度
        /// </summary>
        public float Magnitude;

        /// <summary>
        /// 触发位置
        /// </summary>
        public UnityEngine.Vector3 Location;

        /// <summary>
        /// 触发法线方向
        /// </summary>
        public UnityEngine.Vector3 Normal;

        /// <summary>
        /// 构建一次性执行通知
        /// </summary>
        public static CueNotify Execute(GameplayTag tag, object source, object target, float magnitude = 0f)
        {
            return new CueNotify
            {
                CueTag = tag,
                Type = CueNotifyType.Execute,
                Source = source,
                Target = target,
                Magnitude = magnitude
            };
        }

        /// <summary>
        /// 构建持续开始通知
        /// </summary>
        public static CueNotify Add(GameplayTag tag, object source, object target)
        {
            return new CueNotify
            {
                CueTag = tag,
                Type = CueNotifyType.Add,
                Source = source,
                Target = target
            };
        }

        /// <summary>
        /// 构建持续结束通知
        /// </summary>
        public static CueNotify Remove(GameplayTag tag, object source, object target)
        {
            return new CueNotify
            {
                CueTag = tag,
                Type = CueNotifyType.Remove,
                Source = source,
                Target = target
            };
        }
    }

    /// <summary>
    /// 表现处理器接口
    /// </summary>
    public interface ICueHandler
    {
        /// <summary>
        /// 是否可处理指定标签
        /// </summary>
        bool CanHandle(GameplayTag tag);

        /// <summary>
        /// 处理表现通知
        /// </summary>
        void HandleCue(CueNotify notify);
    }

    /// <summary>
    /// 表现管理器
    /// </summary>
    public class CueManager
    {
        private readonly List<ICueHandler> _handlers = new();

        /// <summary>
        /// 表现触发事件
        /// </summary>
        public event Action<CueNotify> OnCueTriggered;

        /// <summary>
        /// 注册表现处理器
        /// </summary>
        public void RegisterHandler(ICueHandler handler)
        {
            if (handler != null && !_handlers.Contains(handler))
            {
                _handlers.Add(handler);
            }
        }

        /// <summary>
        /// 注销表现处理器
        /// </summary>
        public void UnregisterHandler(ICueHandler handler)
        {
            _handlers.Remove(handler);
        }

        /// <summary>
        /// 触发表现通知
        /// </summary>
        public void TriggerCue(CueNotify notify)
        {
            if (!notify.CueTag.IsValid)
                return;

            OnCueTriggered?.Invoke(notify);

            foreach (var handler in _handlers)
            {
                if (handler.CanHandle(notify.CueTag))
                {
                    handler.HandleCue(notify);
                }
            }
        }

        /// <summary>
        /// 通过标签名称触发表现通知
        /// </summary>
        public void TriggerCue(string tagName, CueNotifyType type, object source, object target, float magnitude = 0f)
        {
            var notify = new CueNotify
            {
                CueTag = GameplayTag.Get(tagName),
                Type = type,
                Source = source,
                Target = target,
                Magnitude = magnitude
            };
            TriggerCue(notify);
        }

        /// <summary>
        /// 清空处理器
        /// </summary>
        public void Clear()
        {
            _handlers.Clear();
        }
    }
}
