using UnityEngine;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// Root Motion 数据收集器（可选）
    /// 只在需要 Root Motion 时添加此组件
    /// </summary>
    public class RootMotionCollector : MonoBehaviour
    {
        public Character Character;
        public Animator Animator;

        private void OnAnimatorMove()
        {
            if (Character != null && Character.UseRootMotion && Animator != null)
            {
                Character.RootMotionPositionDelta += Animator.deltaPosition;
                Character.RootMotionRotationDelta = Animator.deltaRotation * Character.RootMotionRotationDelta;
            }
        }
    }
}
