using System.Collections.Generic;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 战斗管理器接口
    /// </summary>
    public interface ICombatManager : IModule
    {
        /// <summary>
        /// 表现管理器
        /// </summary>
        CueManager Cues { get; }

        /// <summary>
        /// 注册角色
        /// </summary>
        void RegisterCharacter(Character character);

        /// <summary>
        /// 注销角色
        /// </summary>
        void UnregisterCharacter(Character character);

        /// <summary>
        /// 获取所有角色
        /// </summary>
        IReadOnlyList<Character> GetCharacters();

        /// <summary>
        /// 应用伤害
        /// </summary>
        void ApplyDamage(Character target, DamageInfo damage);
    }
}
