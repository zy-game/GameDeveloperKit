using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 效果容器，管理实体上的所有效果
    /// </summary>
    public class EffectContainer
    {
        private readonly List<ActiveEffect> _activeEffects = new();
        private readonly List<ActiveEffect> _pendingRemove = new();
        private readonly AttributeSet[] _attributeSets;
        private readonly TagContainer _ownedTags;
        private CueManager _cueManager;

        /// <summary>
        /// 当前激活的效果列表
        /// </summary>
        public IReadOnlyList<ActiveEffect> ActiveEffects => _activeEffects;

        /// <summary>
        /// 拥有的标签容器
        /// </summary>
        public TagContainer OwnedTags => _ownedTags;

        public event Action<ActiveEffect> OnEffectApplied;
        public event Action<ActiveEffect> OnEffectRemoved;
        public event Action<ActiveEffect> OnEffectStackChanged;

        /// <summary>
        /// 创建效果容器
        /// </summary>
        public EffectContainer(TagContainer ownedTags, params AttributeSet[] attributeSets)
        {
            _ownedTags = ownedTags ?? new TagContainer();
            _attributeSets = attributeSets ?? Array.Empty<AttributeSet>();
        }

        /// <summary>
        /// 设置 Cue 管理器（用于触发表现效果）
        /// </summary>
        public void SetCueManager(CueManager cueManager)
        {
            _cueManager = cueManager;
        }

        /// <summary>
        /// 检查是否可应用效果
        /// </summary>
        public bool CanApplyEffect(GameplayEffect effect)
        {
            if (effect == null)
                return false;

            if (effect.RequiredTags != null && effect.RequiredTags.Length > 0)
            {
                foreach (var tagName in effect.RequiredTags)
                {
                    if (!_ownedTags.HasTag(GameplayTag.Get(tagName)))
                        return false;
                }
            }

            if (effect.BlockedTags != null && effect.BlockedTags.Length > 0)
            {
                foreach (var tagName in effect.BlockedTags)
                {
                    if (_ownedTags.HasTag(GameplayTag.Get(tagName)))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 应用效果并返回实例
        /// </summary>
        public ActiveEffect ApplyEffect(GameplayEffect effect, object source, int level = 1)
        {
            if (!CanApplyEffect(effect))
                return null;

            var existing = FindEffect(effect);
            if (existing != null)
            {
                switch (effect.StackPolicy)
                {
                    case EffectStackPolicy.None:
                        return null;

                    case EffectStackPolicy.Refresh:
                        existing.RefreshDuration();
                        return existing;

                    case EffectStackPolicy.Stack:
                        existing.AddStack();
                        existing.RefreshDuration();
                        OnEffectStackChanged?.Invoke(existing);
                        return existing;

                    case EffectStackPolicy.Override:
                        RemoveEffect(existing);
                        break;
                }
            }

            if (effect.RemoveEffectsWithTags != null)
            {
                foreach (var tagName in effect.RemoveEffectsWithTags)
                {
                    RemoveEffectsWithTag(GameplayTag.Get(tagName));
                }
            }

            var activeEffect = ActiveEffect.Create(effect, source, this, level);
            _activeEffects.Add(activeEffect);

            foreach (var tag in activeEffect.GrantedTags)
            {
                _ownedTags.AddTag(tag);
            }

            ApplyModifiers(activeEffect);

            activeEffect.OnExpired += OnEffectExpired;
            activeEffect.OnStackChanged += e => OnEffectStackChanged?.Invoke(e);
            activeEffect.OnPeriodTick += OnEffectPeriodTick;

            if (effect.DurationType == EffectDurationType.Instant)
            {
                ExecuteInstantEffect(activeEffect);
                activeEffect.Expire();
            }

            // 触发效果应用表现
            if (_cueManager != null && effect.Cues != null)
            {
                foreach (var cueDef in effect.Cues)
                {
                    if (cueDef != null && cueDef.CueTags != null)
                    {
                        foreach (var tagName in cueDef.CueTags)
                        {
                            if (!string.IsNullOrEmpty(tagName))
                            {
                                var cueTag = GameplayTag.Get(tagName);
                                var notifyType = effect.DurationType == EffectDurationType.Instant 
                                    ? CueNotifyType.Execute 
                                    : CueNotifyType.Add;
                                _cueManager.TriggerCue(new CueNotify
                                {
                                    CueTag = cueTag,
                                    Type = notifyType,
                                    Source = source,
                                    Target = this
                                });
                            }
                        }
                    }
                }
            }

            OnEffectApplied?.Invoke(activeEffect);
            return activeEffect;
        }

        /// <summary>
        /// 标记移除效果
        /// </summary>
        public bool RemoveEffect(ActiveEffect effect)
        {
            if (effect == null || !_activeEffects.Contains(effect))
                return false;

            effect.Expire();
            return true;
        }

        /// <summary>
        /// 移除指定来源的所有效果
        /// </summary>
        public void RemoveEffectsFromSource(object source)
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                if (_activeEffects[i].Source == source)
                {
                    _activeEffects[i].Expire();
                }
            }
        }

        /// <summary>
        /// 移除拥有指定标签的所有效果
        /// </summary>
        public void RemoveEffectsWithTag(GameplayTag tag)
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                if (_activeEffects[i].GrantedTags.HasTag(tag))
                {
                    _activeEffects[i].Expire();
                }
            }
        }

        /// <summary>
        /// 查找指定定义的效果
        /// </summary>
        public ActiveEffect FindEffect(GameplayEffect definition)
        {
            foreach (var effect in _activeEffects)
            {
                if (effect.Definition == definition && !effect.IsExpired)
                    return effect;
            }
            return null;
        }

        /// <summary>
        /// 是否存在指定定义的效果
        /// </summary>
        public bool HasEffect(GameplayEffect definition)
        {
            return FindEffect(definition) != null;
        }

        /// <summary>
        /// 更新所有效果
        /// </summary>
        public void Tick(float deltaTime)
        {
            _pendingRemove.Clear();

            foreach (var effect in _activeEffects)
            {
                effect.Tick(deltaTime);
                if (effect.IsExpired)
                {
                    _pendingRemove.Add(effect);
                }
            }

            foreach (var effect in _pendingRemove)
            {
                CleanupEffect(effect);
            }
        }

        /// <summary>
        /// 清理所有效果
        /// </summary>
        public void Clear()
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                _activeEffects[i].Expire();
            }

            foreach (var effect in _activeEffects)
            {
                CleanupEffect(effect);
            }
            _activeEffects.Clear();
        }

        /// <summary>
        /// 应用属性修改器
        /// </summary>
        private void ApplyModifiers(ActiveEffect effect)
        {
            if (effect.Definition.Modifiers == null)
                return;

            foreach (var modDef in effect.Definition.Modifiers)
            {
                var modifier = AttributeModifier.Create(
                    modDef.AttributeName,
                    modDef.Operation,
                    modDef.Value * effect.StackCount,
                    modDef.Priority,
                    effect
                );

                foreach (var attrSet in _attributeSets)
                {
                    if (attrSet.HasAttribute(modDef.AttributeName))
                    {
                        attrSet.AddModifier(modifier);
                        effect.AppliedModifiers.Add(modifier);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 移除属性修改器
        /// </summary>
        private void RemoveModifiers(ActiveEffect effect)
        {
            foreach (var modifier in effect.AppliedModifiers)
            {
                foreach (var attrSet in _attributeSets)
                {
                    attrSet.RemoveModifier(modifier);
                }
                ReferencePool.Release(modifier);
            }
            effect.AppliedModifiers.Clear();
        }

        /// <summary>
        /// 执行即时效果
        /// </summary>
        private void ExecuteInstantEffect(ActiveEffect effect)
        {
            // 即时效果的逻辑在这里执行
        }

        /// <summary>
        /// 效果过期回调
        /// </summary>
        private void OnEffectExpired(ActiveEffect effect)
        {
            // 标记为待移除，在Tick中统一处理
        }

        /// <summary>
        /// 周期效果触发回调
        /// </summary>
        private void OnEffectPeriodTick(ActiveEffect effect)
        {
            // 周期效果触发
            ExecuteInstantEffect(effect);
        }

        /// <summary>
        /// 清理效果并触发表现
        /// </summary>
        private void CleanupEffect(ActiveEffect effect)
        {
            _activeEffects.Remove(effect);

            foreach (var tag in effect.GrantedTags)
            {
                _ownedTags.RemoveTag(tag);
            }

            RemoveModifiers(effect);

            // 触发效果移除表现（仅对持续/永久效果）
            if (_cueManager != null && effect.Definition.Cues != null && 
                effect.Definition.DurationType != EffectDurationType.Instant)
            {
                foreach (var cueDef in effect.Definition.Cues)
                {
                    if (cueDef != null && cueDef.CueTags != null)
                    {
                        foreach (var tagName in cueDef.CueTags)
                        {
                            if (!string.IsNullOrEmpty(tagName))
                            {
                                var cueTag = GameplayTag.Get(tagName);
                                _cueManager.TriggerCue(new CueNotify
                                {
                                    CueTag = cueTag,
                                    Type = CueNotifyType.Remove,
                                    Source = effect.Source,
                                    Target = this
                                });
                            }
                        }
                    }
                }
            }

            OnEffectRemoved?.Invoke(effect);
            ReferencePool.Release(effect);
        }
    }
}
