using UnityEngine;

namespace GameDeveloperKit.Grid
{
    /// <summary>
    /// 网格表面接口
    /// </summary>
    public interface IGridSurface
    {
        /// <summary>
        /// 本地坐标转世界坐标
        /// </summary>
        Vector3 LocalToWorld(Vector3 localPosition);

        /// <summary>
        /// 世界坐标转本地坐标
        /// </summary>
        Vector3 WorldToLocal(Vector3 worldPosition);

        /// <summary>
        /// 获取表面法线
        /// </summary>
        Vector3 GetNormal(Vector3 localPosition);

        /// <summary>
        /// 获取表面旋转
        /// </summary>
        Quaternion GetRotation(Vector3 localPosition);
    }
}
