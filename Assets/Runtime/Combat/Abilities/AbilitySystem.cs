using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 技能系统组件
    /// </summary>
    public class AbilitySystem
    {
        private readonly List<AbilitySpec> _abilities = new();
        private readonly List<AbilitySpec> _activeAbilities = new();
        private readonly EffectContainer _effectContainer;
        private readonly TagContainer _ownedTags;
        private readonly AttributeSet[] _attributeSets;
        private CueManager _cueManager;

        /// <summary>
        /// 所有已授予的技能
        /// </summary>
        public IReadOnlyList<AbilitySpec> Abilities => _abilities;

        /// <summary>
        /// 当前激活中的技能
        /// </summary>
        public IReadOnlyList<AbilitySpec> ActiveAbilities => _activeAbilities;

        /// <summary>
        /// 效果容器
        /// </summary>
        public EffectContainer Effects => _effectContainer;

        /// <summary>
        /// 当前拥有的标签
        /// </summary>
        public TagContainer OwnedTags => _ownedTags;

        public event Action<AbilitySpec> OnAbilityGranted;
        public event Action<AbilitySpec> OnAbilityRemoved;
        public event Action<AbilitySpec> OnAbilityActivated;
        public event Action<AbilitySpec> OnAbilityEnded;

        /// <summary>
        /// 创建技能系统组件
        /// </summary>
        public AbilitySystem(params AttributeSet[] attributeSets)
        {
            _attributeSets = attributeSets ?? Array.Empty<AttributeSet>();
            _ownedTags = new TagContainer();
            _effectContainer = new EffectContainer(_ownedTags, _attributeSets);
        }

        /// <summary>
        /// 设置 Cue 管理器（用于触发表现效果）
        /// </summary>
        public void SetCueManager(CueManager cueManager)
        {
            _cueManager = cueManager;
            _effectContainer.SetCueManager(cueManager);
        }

        /// <summary>
        /// 授予技能
        /// </summary>
        public AbilitySpec GiveAbility(AbilityBase ability, int level = 1)
        {
            if (ability == null)
                return null;

            var existing = FindAbility(ability);
            if (existing != null)
                return existing;

            var spec = AbilitySpec.Create(ability, level);
            _abilities.Add(spec);

            spec.OnActivated += OnAbilityActivatedInternal;
            spec.OnEnded += OnAbilityEndedInternal;
            spec.OnCancelled += OnAbilityEndedInternal;

            OnAbilityGranted?.Invoke(spec);
            return spec;
        }

        /// <summary>
        /// 移除技能
        /// </summary>
        public bool RemoveAbility(AbilityBase ability)
        {
            var spec = FindAbility(ability);
            if (spec == null)
                return false;

            if (spec.IsActive)
            {
                spec.Cancel();
            }

            _abilities.Remove(spec);
            OnAbilityRemoved?.Invoke(spec);
            ReferencePool.Release(spec);
            return true;
        }

        /// <summary>
        /// 查找指定技能定义对应的实例
        /// </summary>
        public AbilitySpec FindAbility(AbilityBase ability)
        {
            foreach (var spec in _abilities)
            {
                if (spec.Definition == ability)
                    return spec;
            }
            return null;
        }

        /// <summary>
        /// 通过标签查找技能
        /// </summary>
        public AbilitySpec FindAbilityByTag(GameplayTag tag)
        {
            foreach (var spec in _abilities)
            {
                if (spec.AbilityTags.HasTag(tag))
                    return spec;
            }
            return null;
        }

        /// <summary>
        /// 尝试激活指定技能实例
        /// </summary>
        public bool TryActivateAbility(AbilitySpec spec)
        {
            if (spec == null || !_abilities.Contains(spec))
                return false;

            if (!spec.CanActivate(_ownedTags, GetAttributeValue))
                return false;

            if (spec.Definition.CancelAbilitiesWithTags != null)
            {
                foreach (var tagName in spec.Definition.CancelAbilitiesWithTags)
                {
                    CancelAbilitiesWithTag(GameplayTag.Get(tagName));
                }
            }

            CommitCost(spec);
            spec.Activate();

            foreach (var tag in spec.GrantedTags)
            {
                _ownedTags.AddTag(tag);
            }

            // 触发技能激活表现
            if (_cueManager != null && spec.Definition.ActivationCues != null)
            {
                foreach (var cueDef in spec.Definition.ActivationCues)
                {
                    if (cueDef != null && cueDef.CueTags != null)
                    {
                        foreach (var tagName in cueDef.CueTags)
                        {
                            if (!string.IsNullOrEmpty(tagName))
                            {
                                var cueTag = GameplayTag.Get(tagName);
                                _cueManager.TriggerCue(CueNotify.Execute(cueTag, this, null));
                            }
                        }
                    }
                }
            }

            if (spec.Definition.EffectsToApply != null)
            {
                foreach (var effect in spec.Definition.EffectsToApply)
                {
                    _effectContainer.ApplyEffect(effect, spec);
                }
            }

            return true;
        }

        /// <summary>
        /// 尝试激活指定技能定义
        /// </summary>
        public bool TryActivateAbility(AbilityBase ability)
        {
            var spec = FindAbility(ability);
            return TryActivateAbility(spec);
        }

        /// <summary>
        /// 取消指定技能实例
        /// </summary>
        public void CancelAbility(AbilitySpec spec)
        {
            if (spec == null || !spec.IsActive)
                return;

            spec.Cancel();
        }

        /// <summary>
        /// 取消所有带指定标签的激活技能
        /// </summary>
        public void CancelAbilitiesWithTag(GameplayTag tag)
        {
            foreach (var spec in _activeAbilities)
            {
                if (spec.AbilityTags.HasTag(tag))
                {
                    spec.Cancel();
                }
            }
        }

        /// <summary>
        /// 取消全部激活技能
        /// </summary>
        public void CancelAllAbilities()
        {
            for (int i = _activeAbilities.Count - 1; i >= 0; i--)
            {
                _activeAbilities[i].Cancel();
            }
        }

        /// <summary>
        /// 更新技能与效果
        /// </summary>
        public void Tick(float deltaTime)
        {
            foreach (var spec in _abilities)
            {
                spec.Tick(deltaTime);
            }

            _effectContainer.Tick(deltaTime);
        }

        /// <summary>
        /// 获取属性当前值
        /// </summary>
        public float GetAttributeValue(string attributeName)
        {
            foreach (var attrSet in _attributeSets)
            {
                if (attrSet.HasAttribute(attributeName))
                    return attrSet.GetCurrentValue(attributeName);
            }
            return 0f;
        }

        /// <summary>
        /// 设置属性基础值
        /// </summary>
        public void SetAttributeBaseValue(string attributeName, float value)
        {
            foreach (var attrSet in _attributeSets)
            {
                if (attrSet.HasAttribute(attributeName))
                {
                    attrSet.SetBaseValue(attributeName, value);
                    return;
                }
            }
        }

        /// <summary>
        /// 应用游戏效果
        /// </summary>
        public ActiveEffect ApplyEffect(GameplayEffect effect, object source = null, int level = 1)
        {
            return _effectContainer.ApplyEffect(effect, source ?? this, level);
        }

        /// <summary>
        /// 移除效果
        /// </summary>
        public void RemoveEffect(ActiveEffect effect)
        {
            _effectContainer.RemoveEffect(effect);
        }

        /// <summary>
        /// 清理全部技能与效果
        /// </summary>
        public void Clear()
        {
            CancelAllAbilities();

            foreach (var spec in _abilities)
            {
                ReferencePool.Release(spec);
            }
            _abilities.Clear();
            _activeAbilities.Clear();

            _effectContainer.Clear();
            _ownedTags.Clear();
        }

        /// <summary>
        /// 提交技能消耗
        /// </summary>
        private void CommitCost(AbilitySpec spec)
        {
            if (spec.Definition.Costs == null)
                return;

            foreach (var cost in spec.Definition.Costs)
            {
                float current = GetAttributeValue(cost.AttributeName);
                SetAttributeBaseValue(cost.AttributeName, current - cost.Cost);
            }
        }

        /// <summary>
        /// 技能激活回调
        /// </summary>
        private void OnAbilityActivatedInternal(AbilitySpec spec)
        {
            _activeAbilities.Add(spec);
            OnAbilityActivated?.Invoke(spec);
        }

        /// <summary>
        /// 技能结束或取消回调
        /// </summary>
        private void OnAbilityEndedInternal(AbilitySpec spec)
        {
            _activeAbilities.Remove(spec);

            foreach (var tag in spec.GrantedTags)
            {
                _ownedTags.RemoveTag(tag);
            }

            _effectContainer.RemoveEffectsFromSource(spec);
            OnAbilityEnded?.Invoke(spec);
        }
    }
}
