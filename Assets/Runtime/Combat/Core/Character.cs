using System;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 角色 - 战斗实体的核心容器
    /// 框架层不包含业务属性（Team、Type 等由使用者扩展）
    /// 直接实现 ICharacterController 接口，包含移动逻辑
    /// </summary>
    public class Character : IDamageReceiver, ICharacterController
    {
        private static int _nextId;

        /// <summary>
        /// 角色唯一标识
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// 角色名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 外部持有者（如 GameObject）
        /// </summary>
        public object Owner { get; set; }

        /// <summary>
        /// 技能系统组件
        /// </summary>
        public AbilitySystem AbilitySystem { get; }

        /// <summary>
        /// 生命属性集
        /// </summary>
        public HealthAttributeSet HealthAttributes { get; }

        /// <summary>
        /// 战斗属性集
        /// </summary>
        public CombatAttributeSet CombatAttributes { get; }

        /// <summary>
        /// 资源属性集
        /// </summary>
        public ResourceAttributeSet ResourceAttributes { get; }

        /// <summary>
        /// 移动属性集（可选，调用 EnableMovement 后创建）
        /// </summary>
        public MovementAttributeSet MovementAttributes { get; private set; }

        /// <summary>
        /// 输入提供者（可选）
        /// </summary>
        public IInputProvider InputProvider { get; set; }

        /// <summary>
        /// 角色配置引用
        /// </summary>
        public CharacterConfig Config { get; private set; }

        /// <summary>
        /// 是否死亡
        /// </summary>
        public bool IsDead { get; private set; }

        /// <summary>
        /// 是否在地面上（只读同步）
        /// </summary>
        public bool IsGrounded { get; set; }

        /// <summary>
        /// 缓存的位置（可选，从 Transform 同步）
        /// </summary>
        public Vector3 CachedPosition { get; set; }

        public event Action<Character, DamageInfo> OnDamageReceived;
        public event Action<Character, float> OnHealed;
        public event Action<Character> OnDeath;
        public event Action<Character> OnRevive;

        public Character()
        {
            
        }

        /// <summary>
        /// 创建角色
        /// </summary>
        public Character(string name = null, CueManager cueManager = null)
        {
            Id = ++_nextId;
            Name = name ?? $"Character_{Id}";

            HealthAttributes = new HealthAttributeSet();
            CombatAttributes = new CombatAttributeSet();
            ResourceAttributes = new ResourceAttributeSet();

            AbilitySystem = new AbilitySystem(HealthAttributes, CombatAttributes, ResourceAttributes);
            
            if (cueManager != null)
            {
                AbilitySystem.SetCueManager(cueManager);
            }
        }

        /// <summary>
        /// 从配置初始化角色
        /// </summary>
        public void InitializeFromConfig(CharacterConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            
            Config = config;
            Name = config.CharacterName;
            
            // 初始化生命属性
            HealthAttributes.SetMaxHealth(config.MaxHealth);
            HealthAttributes.SetHealth(config.MaxHealth);
            HealthAttributes.SetBaseValue(HealthAttributeSet.HealthRegen, config.HealthRegen);
            
            // 初始化战斗属性
            CombatAttributes.SetBaseValue(CombatAttributeSet.Attack, config.Attack);
            CombatAttributes.SetBaseValue(CombatAttributeSet.Defense, config.Defense);
            CombatAttributes.SetBaseValue(CombatAttributeSet.CritRate, config.CritRate);
            CombatAttributes.SetBaseValue(CombatAttributeSet.CritDamage, config.CritDamage);
            
            // 初始化资源属性
            ResourceAttributes.SetBaseValue(ResourceAttributeSet.MaxMana, config.MaxMana);
            ResourceAttributes.SetBaseValue(ResourceAttributeSet.Mana, config.MaxMana);
            ResourceAttributes.SetBaseValue(ResourceAttributeSet.ManaRegen, config.ManaRegen);
            
            // 启用移动
            if (config.EnableMovement)
            {
                EnableMovement();
                MovementAttributes.SetBaseValue(MovementAttributeSet.MoveSpeed, config.MoveSpeed);
                MovementAttributes.SetBaseValue(MovementAttributeSet.JumpHeight, config.JumpHeight);
                MovementAttributes.SetBaseValue(MovementAttributeSet.Gravity, config.Gravity);
                MovementAttributes.SetBaseValue(MovementAttributeSet.Acceleration, config.Acceleration);
                MovementAttributes.SetBaseValue(MovementAttributeSet.Mass, config.Mass);
            }
            
            // 授予技能
            if (config.InitialAbilities != null)
            {
                foreach (var ability in config.InitialAbilities)
                {
                    if (ability != null)
                    {
                        AbilitySystem.GiveAbility(ability);
                    }
                }
            }
        }

        /// <summary>
        /// 获取当前生命值
        /// </summary>
        public float GetHealth() => HealthAttributes.GetHealth();

        /// <summary>
        /// 获取最大生命值
        /// </summary>
        public float GetMaxHealth() => HealthAttributes.GetMaxHealth();

        /// <summary>
        /// 获取防御力
        /// </summary>
        public float GetDefense() => CombatAttributes.GetDefense();

        /// <summary>
        /// 获取指定伤害类型的抗性
        /// </summary>
        public float GetResistance(DamageType type)
        {
            return 0f;
        }

        /// <summary>
        /// 应用伤害并触发相关事件
        /// </summary>
        public void ApplyDamage(DamageInfo damage)
        {
            if (IsDead)
                return;

            float healthBefore = GetHealth();
            float newHealth = healthBefore - damage.FinalDamage;
            HealthAttributes.SetHealth(newHealth);

            OnDamageReceived?.Invoke(this, damage);

            if (newHealth <= 0f && !IsDead)
            {
                Die();
            }
        }

        /// <summary>
        /// 回复生命值
        /// </summary>
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f)
                return;

            float current = GetHealth();
            float max = GetMaxHealth();
            float newHealth = Math.Min(current + amount, max);
            HealthAttributes.SetHealth(newHealth);

            OnHealed?.Invoke(this, newHealth - current);
        }

        /// <summary>
        /// 进入死亡状态
        /// </summary>
        public void Die()
        {
            if (IsDead)
                return;

            IsDead = true;
            AbilitySystem.CancelAllAbilities();
            OnDeath?.Invoke(this);
        }

        /// <summary>
        /// 复活并设置生命百分比
        /// </summary>
        public void Revive(float healthPercent = 1f)
        {
            if (!IsDead)
                return;

            IsDead = false;
            float maxHealth = GetMaxHealth();
            HealthAttributes.SetHealth(maxHealth * healthPercent);
            OnRevive?.Invoke(this);
        }

        /// <summary>
        /// 每帧更新技能与生命回复
        /// </summary>
        public virtual void Tick(float deltaTime)
        {
            if (IsDead)
                return;

            // 处理输入
            InputProvider?.ProcessInput(this, deltaTime);

            // 更新技能系统
            AbilitySystem.Tick(deltaTime);

            // 生命回复
            float regen = HealthAttributes.GetHealthRegen();
            if (regen > 0f)
            {
                Heal(regen * deltaTime);
            }
        }

        /// <summary>
        /// 清理角色状态
        /// </summary>
        public void Clear()
        {
            AbilitySystem.Clear();
            IsDead = false;
        }

        /// <summary>
        /// 启用移动能力（创建 MovementAttributeSet）
        /// </summary>
        public void EnableMovement()
        {
            if (MovementAttributes == null)
            {
                MovementAttributes = new MovementAttributeSet();
                IsGrounded = false;
            }
        }

        #region Movement State (Internal)

        // 移动输入和状态（由 CharacterControllerAdapter 或业务层设置）
        internal Vector3 MoveInput;
        internal bool JumpRequested;
        internal Vector3 ExternalVelocity;
        internal bool HasExternalVelocity;

        // Unity 相关引用（由 CharacterControllerAdapter 设置）
        internal KinematicCharacterMotor Motor;
        internal Transform Transform;
        internal Animator Animator;

        // Root Motion 数据
        internal Vector3 RootMotionPositionDelta;
        internal Quaternion RootMotionRotationDelta = Quaternion.identity;

        // 配置
        public float RotationSharpness = 10f;
        public float TerminalVelocity = -50f;
        public float AirControlFactor = 0.5f;
        public bool UseRootMotion = false;
        public float RootMotionBlendWeight = 1f;

        /// <summary>
        /// 状态变化回调（业务层可订阅）
        /// </summary>
        public System.Action<Character, Vector3, bool> OnStateChanged;

        #endregion

        #region ICharacterController Implementation

        public virtual void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (MovementAttributes == null)
                return;

            float moveSpeed = MovementAttributes.GetMoveSpeed();
            float acceleration = MovementAttributes.GetAcceleration();
            float gravity = MovementAttributes.GetGravity();
            float jumpHeight = MovementAttributes.GetJumpHeight();

            // 如果有外部速度，直接使用（业务层控制冲刺、击退等）
            if (HasExternalVelocity)
            {
                currentVelocity = ExternalVelocity;
                
                // 空中时应用重力
                if (Motor != null && !Motor.GroundingStatus.IsStableOnGround)
                {
                    currentVelocity.y += gravity * deltaTime;
                    if (currentVelocity.y < TerminalVelocity)
                        currentVelocity.y = TerminalVelocity;
                }
                return;
            }

            // Root Motion 处理
            if (UseRootMotion && Animator != null && Animator.applyRootMotion)
            {
                Vector3 rootMotionVelocity = RootMotionPositionDelta / deltaTime;
                Vector3 inputVelocity = MoveInput * moveSpeed;
                currentVelocity = Vector3.Lerp(inputVelocity, rootMotionVelocity, RootMotionBlendWeight);
                RootMotionPositionDelta = Vector3.zero;

                // 跳跃
                if (JumpRequested && Motor != null && Motor.GroundingStatus.IsStableOnGround)
                {
                    float jumpSpeed = Mathf.Sqrt(2f * jumpHeight * Mathf.Abs(gravity));
                    currentVelocity.y = jumpSpeed;
                    JumpRequested = false;
                    Motor.ForceUnground();
                }
                return;
            }

            if (Motor == null)
                return;

            // 地面移动
            if (Motor.GroundingStatus.IsStableOnGround)
            {
                Vector3 targetVelocity = MoveInput * moveSpeed;
                float sharpness = acceleration;
                currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, 1f - Mathf.Exp(-sharpness * deltaTime));

                // 跳跃
                if (JumpRequested)
                {
                    float jumpSpeed = Mathf.Sqrt(2f * jumpHeight * Mathf.Abs(gravity));
                    currentVelocity.y = jumpSpeed;
                    JumpRequested = false;
                    Motor.ForceUnground();
                }
            }
            else
            {
                // 空中：应用重力
                currentVelocity += Vector3.up * gravity * deltaTime;
                if (currentVelocity.y < TerminalVelocity)
                    currentVelocity.y = TerminalVelocity;

                // 空中控制
                Vector3 airInput = MoveInput * moveSpeed * AirControlFactor;
                currentVelocity.x = Mathf.Lerp(currentVelocity.x, airInput.x, deltaTime * 5f);
                currentVelocity.z = Mathf.Lerp(currentVelocity.z, airInput.z, deltaTime * 5f);
            }
        }

        public virtual void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            if (Motor == null)
                return;

            // Root Motion 旋转
            if (UseRootMotion && Animator != null && Animator.applyRootMotion && RootMotionRotationDelta != Quaternion.identity)
            {
                Quaternion inputRotation = currentRotation;
                if (MoveInput.sqrMagnitude > 0.01f)
                {
                    Vector3 targetDirection = MoveInput.normalized;
                    inputRotation = Quaternion.LookRotation(targetDirection, Motor.CharacterUp);
                }
                
                Quaternion rootMotionRotation = currentRotation * RootMotionRotationDelta;
                currentRotation = Quaternion.Slerp(inputRotation, rootMotionRotation, RootMotionBlendWeight);
                RootMotionRotationDelta = Quaternion.identity;
            }
            else if (MoveInput.sqrMagnitude > 0.01f)
            {
                Vector3 targetDirection = MoveInput.normalized;
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection, Motor.CharacterUp);
                currentRotation = Quaternion.Slerp(currentRotation, targetRotation, 1f - Mathf.Exp(-RotationSharpness * deltaTime));
            }
        }

        public virtual void BeforeCharacterUpdate(float deltaTime) { }

        public virtual void PostGroundingUpdate(float deltaTime)
        {
            if (Motor != null)
            {
                IsGrounded = Motor.GroundingStatus.IsStableOnGround;
            }
        }

        public virtual void AfterCharacterUpdate(float deltaTime)
        {
            if (Transform != null)
            {
                CachedPosition = Transform.position;
            }

            // 触发状态变化回调
            if (Motor != null)
            {
                OnStateChanged?.Invoke(this, Motor.Velocity, Motor.GroundingStatus.IsStableOnGround);
            }
        }

        public virtual bool IsColliderValidForCollisions(Collider coll) => true;

        public virtual void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }

        public virtual void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }

        public virtual void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }

        public virtual void OnDiscreteCollisionDetected(Collider hitCollider) { }

        #endregion

        #region Public Movement API

        /// <summary>
        /// 设置移动输入（归一化方向）
        /// </summary>
        public virtual void SetMoveInput(Vector3 input)
        {
            MoveInput = input;
        }

        /// <summary>
        /// 请求跳跃
        /// </summary>
        public virtual void Jump()
        {
            JumpRequested = true;
        }

        /// <summary>
        /// 设置外部速度（用于冲刺、击退等）
        /// </summary>
        public virtual void SetExternalVelocity(Vector3 velocity)
        {
            ExternalVelocity = velocity;
            HasExternalVelocity = true;
        }

        /// <summary>
        /// 清除外部速度
        /// </summary>
        public virtual void ClearExternalVelocity()
        {
            ExternalVelocity = Vector3.zero;
            HasExternalVelocity = false;
        }

        /// <summary>
        /// 瞬移到指定位置
        /// </summary>
        public virtual void Teleport(Vector3 position)
        {
            if (Motor != null)
            {
                Motor.SetPosition(position);
            }
        }

        /// <summary>
        /// 强制离地
        /// </summary>
        public virtual void ForceUnground()
        {
            if (Motor != null)
            {
                Motor.ForceUnground();
            }
        }

        /// <summary>
        /// 获取当前速度
        /// </summary>
        public virtual Vector3 GetVelocity()
        {
            return Motor != null ? Motor.Velocity : Vector3.zero;
        }

        #endregion
    }
}
