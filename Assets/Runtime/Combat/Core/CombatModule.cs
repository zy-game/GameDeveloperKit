using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Combat
{
    public interface ICombatModule : IModule
    {
        CueManager Cues { get; }

        void ApplyDamage(Character target, DamageInfo damage);
        void ApplyDamage(Character source, Character target, float baseDamage, DamageType type);
        Character CreateCharacter(CharacterConfig config, Vector3 position, IInputProvider inputProvider = null);
        Character CreateCharacter(CharacterConfig config);
        void DestroyCharacter(Character character);
        List<Character> FindCharacters(Predicate<Character> predicate);
        IReadOnlyList<Character> GetCharacters();
        List<Character> GetCharactersInRange(Vector3 center, float radius);
        Character GetNearestCharacter(Vector3 position, Predicate<Character> filter = null);
        void RegisterCharacter(Character character);
        void UnregisterCharacter(Character character);
    }


    /// <summary>
    /// 战斗模块
    /// </summary>
    public class CombatModule : ICombatModule
    {
        private readonly List<Character> _characters = new();
        private readonly CueManager _cueManager = new();
        private bool useMonoUpdate = true;

        /// <summary>
        /// 表现管理器
        /// </summary>
        public CueManager Cues => _cueManager;

        /// <summary>
        /// 模块启动
        /// </summary>
        public void OnStartup()
        {
            Game.Debug?.Info("[Combat] Module started");
        }

        /// <summary>
        /// 模块更新
        /// </summary>
        public void OnUpdate(float deltaTime)
        {
            if (useMonoUpdate is false)
                return;
            for (int i = _characters.Count - 1; i >= 0; i--)
            {
                _characters[i].Tick(deltaTime);
            }
        }

        /// <summary>
        /// 模块清理
        /// </summary>
        public void OnClearup()
        {
            foreach (var character in _characters)
            {
                character.Clear();
            }
            _characters.Clear();
            _cueManager.Clear();
            Game.Debug?.Info("[Combat] Module cleared");
        }

        /// <summary>
        /// 创建角色（带移动）
        /// </summary>
        public Character CreateCharacter(
            CharacterConfig config,
            Vector3 position,
            IInputProvider inputProvider = null)
        {
            if (config == null)
            {
                Game.Debug?.Error("[Combat] CharacterConfig is null");
                return null;
            }

            // 1. 创建 Character 数据层
            var character = new Character(config.CharacterName, _cueManager);
            character.InitializeFromConfig(config);

            // 2. 创建 GameObject（如果需要移动）
            if (config.EnableMovement)
            {
                var go = new GameObject(config.CharacterName);
                go.transform.position = position;

                // 3. 添加 KinematicCharacterMotor
                var motor = go.AddComponent<KinematicCharacterController.KinematicCharacterMotor>();
                motor.MaxStableSlopeAngle = 45f;
                motor.MaxStepHeight = 0.3f;

                // 4. 直接设置 Character 的 Unity 引用
                character.Motor = motor;
                character.Transform = go.transform;

                // 5. 将 Character 设置为 Motor 的控制器（Character 实现了 ICharacterController）
                motor.CharacterController = character;

                // 6. 设置输入
                character.InputProvider = inputProvider;

                // 7. 配置 Character 的移动参数
                character.RotationSharpness = config.RotationSharpness;

                // 8. 如果需要 Root Motion，添加收集器
                if (character.UseRootMotion)
                {
                    var animator = go.GetComponent<Animator>();
                    if (animator != null)
                    {
                        character.Animator = animator;
                        var collector = go.AddComponent<RootMotionCollector>();
                        collector.Character = character;
                        collector.Animator = animator;
                    }
                }

                // 9. 关联
                character.Owner = go;
            }

            // 10. 注册
            RegisterCharacter(character);

            Game.Debug?.Info($"[Combat] Character created: {character.Name} (ID: {character.Id})");
            return character;
        }

        /// <summary>
        /// 创建角色（无移动）
        /// </summary>
        public Character CreateCharacter(CharacterConfig config)
        {
            if (config == null)
            {
                Game.Debug?.Error("[Combat] CharacterConfig is null");
                return null;
            }

            var character = new Character(config.CharacterName, _cueManager);
            character.InitializeFromConfig(config);
            RegisterCharacter(character);

            Game.Debug?.Info($"[Combat] Character created (no movement): {character.Name} (ID: {character.Id})");
            return character;
        }

        /// <summary>
        /// 注册角色
        /// </summary>
        public void RegisterCharacter(Character character)
        {
            if (character != null && !_characters.Contains(character))
            {
                _characters.Add(character);
            }
        }

        /// <summary>
        /// 注销角色
        /// </summary>
        public void UnregisterCharacter(Character character)
        {
            _characters.Remove(character);
        }

        /// <summary>
        /// 销毁角色
        /// </summary>
        public void DestroyCharacter(Character character)
        {
            if (character == null)
                return;

            // 销毁 GameObject（如果有）
            if (character.Owner is GameObject go)
            {
                UnityEngine.Object.Destroy(go);
            }

            // 清理数据
            UnregisterCharacter(character);
            character.Clear();

            Game.Debug?.Info($"[Combat] Character destroyed: {character.Name} (ID: {character.Id})");
        }

        /// <summary>
        /// 获取所有角色
        /// </summary>
        public IReadOnlyList<Character> GetCharacters() => _characters;

        /// <summary>
        /// 查找角色（通用查询）
        /// </summary>
        public List<Character> FindCharacters(System.Predicate<Character> predicate)
        {
            return _characters.FindAll(predicate);
        }

        /// <summary>
        /// 范围查询
        /// </summary>
        public List<Character> GetCharactersInRange(Vector3 center, float radius)
        {
            var result = new List<Character>();
            float radiusSqr = radius * radius;

            foreach (var character in _characters)
            {
                if ((character.CachedPosition - center).sqrMagnitude <= radiusSqr)
                {
                    result.Add(character);
                }
            }

            return result;
        }

        /// <summary>
        /// 查找最近角色
        /// </summary>
        public Character GetNearestCharacter(Vector3 position, System.Predicate<Character> filter = null)
        {
            Character nearest = null;
            float minDistSqr = float.MaxValue;

            foreach (var character in _characters)
            {
                if (filter != null && !filter(character))
                    continue;

                float distSqr = (character.CachedPosition - position).sqrMagnitude;
                if (distSqr < minDistSqr)
                {
                    minDistSqr = distSqr;
                    nearest = character;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 计算并应用伤害，同时触发伤害表现
        /// </summary>
        public void ApplyDamage(Character target, DamageInfo damage)
        {
            if (target == null || target.IsDead)
                return;

            damage = DamageCalculator.Calculate(damage, target);
            target.ApplyDamage(damage);

            if (!string.IsNullOrEmpty(damage.Type.ToString()))
            {
                var cueTag = GameplayTag.Get($"Cue.Damage.{damage.Type}");
                _cueManager.TriggerCue(CueNotify.Execute(cueTag, damage.Source, target, damage.FinalDamage));
            }
        }

        /// <summary>
        /// 快捷构建伤害并应用
        /// </summary>
        public void ApplyDamage(Character source, Character target, float baseDamage, DamageType type)
        {
            var damage = DamageInfo.Create(baseDamage, type, source);

            if (source != null)
            {
                float critRate = source.CombatAttributes.GetCritRate();
                if (DamageCalculator.RollCritical(critRate))
                {
                    damage.IsCritical = true;
                    damage.CritMultiplier = source.CombatAttributes.GetCritDamage();
                }
            }

            ApplyDamage(target, damage);
        }
    }
}
