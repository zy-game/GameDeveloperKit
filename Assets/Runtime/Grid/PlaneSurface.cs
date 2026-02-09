using UnityEngine;

namespace GameDeveloperKit.Grid
{
    /// <summary>
    /// 平面表面
    /// </summary>
    public class PlaneSurface : IGridSurface
    {
        /// <summary>
        /// 表面原点
        /// </summary>
        public Vector3 Origin { get; set; }

        /// <summary>
        /// 表面旋转
        /// </summary>
        public Quaternion Rotation { get; set; }

        /// <summary>
        /// 表面法线
        /// </summary>
        public Vector3 Normal => Rotation * Vector3.up;

        public PlaneSurface() : this(Vector3.zero, Quaternion.identity) { }

        public PlaneSurface(Vector3 origin, Quaternion rotation)
        {
            Origin = origin;
            Rotation = rotation;
        }

        public Vector3 LocalToWorld(Vector3 localPosition)
        {
            return Origin + Rotation * localPosition;
        }

        public Vector3 WorldToLocal(Vector3 worldPosition)
        {
            return Quaternion.Inverse(Rotation) * (worldPosition - Origin);
        }

        public Vector3 GetNormal(Vector3 localPosition)
        {
            return Normal;
        }

        public Quaternion GetRotation(Vector3 localPosition)
        {
            return Rotation;
        }
    }
}
