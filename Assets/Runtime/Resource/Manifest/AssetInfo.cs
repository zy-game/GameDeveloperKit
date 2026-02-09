using System;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源清单
    /// </summary>
    [Serializable]
    public class AssetInfo
    {
        /// <summary>
        /// 资源名称（用于寻址）
        /// </summary>
        public string name;
        
        /// <summary>
        /// 资源地址（用于寻址）
        /// </summary>
        public string address;
        
        /// <summary>
        /// 资源GUID（用于寻址）
        /// </summary>
        public string guid;
        
        /// <summary>
        /// 资源路径（相对于 Assets/ 的完整路径，用于编辑器加载）
        /// 例如：Assets/GameContent/Prefabs/Player.prefab
        /// </summary>
        public string path;
        
        /// <summary>
        /// 资源标签（用于批量加载和筛选）
        /// </summary>
        public string[] labels;
        
        /// <summary>
        /// 资源类型全名（用于按类型加载）
        /// 例如：GameDeveloperKit.Combat.AbilityBase
        /// </summary>
        public string type;
    }
}