using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 基于CueDefinition资源的表现处理器
    /// </summary>
    public class CueDefinitionHandler : ICueHandler
    {
        private readonly Dictionary<string, List<CueDefinition>> _definitionsByTag = new();
        private readonly Func<CueDefinition, CueNotify, bool> _conditionChecker;
        private readonly Action<CueDefinition, CueNotify> _executor;

        /// <summary>
        /// 构建表现处理器并按标签分组
        /// </summary>
        public CueDefinitionHandler(
            IEnumerable<CueDefinition> definitions,
            Func<CueDefinition, CueNotify, bool> conditionChecker = null,
            Action<CueDefinition, CueNotify> executor = null)
        {
            _conditionChecker = conditionChecker;
            _executor = executor;

            foreach (var def in definitions)
            {
                if (def == null || def.CueTags == null) continue;

                foreach (var tag in def.CueTags)
                {
                    if (string.IsNullOrEmpty(tag)) continue;

                    if (!_definitionsByTag.TryGetValue(tag, out var list))
                    {
                        list = new List<CueDefinition>();
                        _definitionsByTag[tag] = list;
                    }
                    list.Add(def);
                }
            }
        }

        /// <summary>
        /// 是否可处理指定标签
        /// </summary>
        public bool CanHandle(GameplayTag tag)
        {
            return tag.IsValid && _definitionsByTag.ContainsKey(tag.Name);
        }

        /// <summary>
        /// 处理表现通知
        /// </summary>
        public void HandleCue(CueNotify notify)
        {
            if (!notify.CueTag.IsValid) return;

            if (_definitionsByTag.TryGetValue(notify.CueTag.Name, out var definitions))
            {
                foreach (var def in definitions)
                {
                    if (_conditionChecker != null && !_conditionChecker(def, notify))
                        continue;

                    if (_executor != null)
                    {
                        _executor(def, notify);
                    }
                    else
                    {
                        ExecuteDefault(def, notify);
                    }
                }
            }
        }

        /// <summary>
        /// 默认执行逻辑
        /// </summary>
        protected virtual void ExecuteDefault(CueDefinition definition, CueNotify notify)
        {
            // 默认实现 - 子类可以重写
            // 这里只是示例，实际项目中需要根据具体需求实现
            Debug.Log($"[Cue] {definition.CueName} triggered by tag {notify.CueTag.Name}");
        }
    }
}
