using System;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 技能激活状态
    /// </summary>
    public enum AbilityState
    {
        Ready,
        Activating,
        Active,
        Cooldown,
        Blocked
    }

    /// <summary>
    /// 技能实例
    /// </summary>
    public class AbilitySpec : IReference
    {
        private static int _nextId;

        /// <summary>
        /// 实例唯一标识
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        /// 技能定义资源
        /// </summary>
        public AbilityBase Definition { get; private set; }

        /// <summary>
        /// 技能等级
        /// </summary>
        public int Level { get; private set; }

        /// <summary>
        /// 当前技能状态
        /// </summary>
        public AbilityState State { get; private set; }

        /// <summary>
        /// 剩余冷却时间
        /// </summary>
        public float CooldownRemaining { get; private set; }

        /// <summary>
        /// 技能自带标签
        /// </summary>
        public TagContainer AbilityTags { get; } = new();

        /// <summary>
        /// 激活期间授予的标签
        /// </summary>
        public TagContainer GrantedTags { get; } = new();

        public event Action<AbilitySpec> OnActivated;
        public event Action<AbilitySpec> OnEnded;
        public event Action<AbilitySpec> OnCancelled;
        public event Action<AbilitySpec> OnCooldownStarted;
        public event Action<AbilitySpec> OnCooldownEnded;

        /// <summary>
        /// 是否处于可用状态
        /// </summary>
        public bool IsReady => State == AbilityState.Ready;

        /// <summary>
        /// 是否处于激活中或已激活状态
        /// </summary>
        public bool IsActive => State == AbilityState.Active || State == AbilityState.Activating;

        /// <summary>
        /// 是否处于冷却状态
        /// </summary>
        public bool IsOnCooldown => State == AbilityState.Cooldown;

        /// <summary>
        /// 创建技能实例
        /// </summary>
        public static AbilitySpec Create(AbilityBase definition, int level = 1)
        {
            var spec = ReferencePool.Acquire<AbilitySpec>();
            spec.Id = ++_nextId;
            spec.Definition = definition;
            spec.Level = level;
            spec.State = AbilityState.Ready;
            spec.CooldownRemaining = 0f;

            if (definition.AbilityTags != null)
            {
                foreach (var tagName in definition.AbilityTags)
                {
                    spec.AbilityTags.AddTag(GameplayTag.Get(tagName));
                }
            }

            return spec;
        }

        /// <summary>
        /// 检查是否满足激活条件
        /// </summary>
        public bool CanActivate(TagContainer ownerTags, Func<string, float> getAttributeValue)
        {
            if (State != AbilityState.Ready)
                return false;

            if (Definition.ActivationRequiredTags != null)
            {
                foreach (var tagName in Definition.ActivationRequiredTags)
                {
                    if (!ownerTags.HasTag(GameplayTag.Get(tagName)))
                        return false;
                }
            }

            if (Definition.ActivationBlockedTags != null)
            {
                foreach (var tagName in Definition.ActivationBlockedTags)
                {
                    if (ownerTags.HasTag(GameplayTag.Get(tagName)))
                        return false;
                }
            }

            if (Definition.Costs != null && getAttributeValue != null)
            {
                foreach (var cost in Definition.Costs)
                {
                    if (getAttributeValue(cost.AttributeName) < cost.Cost)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 进入激活流程并授予激活标签
        /// </summary>
        public void Activate()
        {
            if (State != AbilityState.Ready)
                return;

            State = AbilityState.Activating;

            if (Definition.ActivationGrantedTags != null)
            {
                foreach (var tagName in Definition.ActivationGrantedTags)
                {
                    GrantedTags.AddTag(GameplayTag.Get(tagName));
                }
            }

            State = AbilityState.Active;
            OnActivated?.Invoke(this);
        }

        /// <summary>
        /// 结束技能并进入冷却
        /// </summary>
        public void End()
        {
            if (!IsActive)
                return;

            GrantedTags.Clear();
            OnEnded?.Invoke(this);

            StartCooldown();
        }

        /// <summary>
        /// 取消技能并进入冷却
        /// </summary>
        public void Cancel()
        {
            if (!IsActive)
                return;

            GrantedTags.Clear();
            OnCancelled?.Invoke(this);

            StartCooldown();
        }

        /// <summary>
        /// 开始冷却计时
        /// </summary>
        public void StartCooldown()
        {
            if (Definition.Cooldown > 0)
            {
                State = AbilityState.Cooldown;
                CooldownRemaining = Definition.Cooldown;
                OnCooldownStarted?.Invoke(this);
            }
            else
            {
                State = AbilityState.Ready;
            }
        }

        /// <summary>
        /// 更新冷却计时
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (State == AbilityState.Cooldown)
            {
                CooldownRemaining -= deltaTime;
                if (CooldownRemaining <= 0f)
                {
                    CooldownRemaining = 0f;
                    State = AbilityState.Ready;
                    OnCooldownEnded?.Invoke(this);
                }
            }
        }

        /// <summary>
        /// 立即重置冷却
        /// </summary>
        public void ResetCooldown()
        {
            if (State == AbilityState.Cooldown)
            {
                CooldownRemaining = 0f;
                State = AbilityState.Ready;
                OnCooldownEnded?.Invoke(this);
            }
        }

        /// <summary>
        /// 清理对象状态用于回收
        /// </summary>
        public void OnClearup()
        {
            Id = 0;
            Definition = null;
            Level = 0;
            State = AbilityState.Ready;
            CooldownRemaining = 0f;
            AbilityTags.Clear();
            GrantedTags.Clear();
            OnActivated = null;
            OnEnded = null;
            OnCancelled = null;
            OnCooldownStarted = null;
            OnCooldownEnded = null;
        }
    }
}
