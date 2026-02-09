using GameDeveloperKit.Config;
using UnityEngine;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 道具配置基类 - 纯框架层配置
    /// 业务层可继承此类扩展为装备、药剂、皮肤等具体道具类型
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Item Config", fileName = "ItemConfig")]
    public class ItemConfig : ScriptableObject, IConfigData
    {
        [Header("基本信息")]
        [Tooltip("道具ID")]
        public string ItemId;

        [Tooltip("道具名称")]
        public string ItemName;

        [Tooltip("道具描述")]
        [TextArea(2, 4)]
        public string Description;

        [Tooltip("道具图标")]
        public Sprite Icon;

        [Header("堆叠设置")]
        [Tooltip("是否可堆叠")]
        public bool Stackable = true;

        [Tooltip("最大堆叠数量")]
        public int MaxStackCount = 99;

        [Header("使用设置")]
        [Tooltip("是否可使用")]
        public bool Usable;

        [Tooltip("使用冷却时间(秒)")]
        public float UseCooldown;

        [Tooltip("使用时应用的效果")]
        public GameplayEffect[] UseEffects;

        /// <summary>
        /// 配置ID（优先使用 ItemId，否则使用资源名称）
        /// </summary>
        public string Id => string.IsNullOrEmpty(ItemId) ? name : ItemId;
    }
}
