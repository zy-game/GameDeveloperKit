using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.UI
{
    /// <summary>
    /// UI 表单接口
    /// </summary>
    public interface IUIForm
    {
        /// <summary>
        /// UI 层级
        /// </summary>
        EUILayer Layer { get; }

        /// <summary>
        /// UI 模式
        /// </summary>
        EUIMode Mode { get; }

        /// <summary>
        /// 是否进入堆栈（仅 Window 为 true）
        /// </summary>
        bool ToStack { get; }

        /// <summary>
        /// UI 状态
        /// </summary>
        UIStatus Status { get; }

        /// <summary>
        /// 游戏对象
        /// </summary>
        GameObject GameObject { get; }

        /// <summary>
        /// Transform
        /// </summary>
        Transform Transform { get; }
        /// <summary>
        /// Canvas 组件
        /// </summary>
        Canvas Canvas { get; }

        /// <summary>
        /// Animator 组件
        /// </summary>
        Animator Animator { get; }
        /// <summary>
        /// 显示
        /// </summary>
        void Show();

        /// <summary>
        /// 隐藏
        /// </summary>
        void Hide();

        /// <summary>
        /// 刷新
        /// </summary>
        void Refresh(params object[] args);

        /// <summary>
        /// 删除
        /// </summary>
        void Destory();

        /// <summary>
        /// 设置全屏背景
        /// </summary>
        void SetFullScreenBackground();

        /// <summary>
        /// 创建UI
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="args"></param>
        UniTask<bool> OnCreate(Transform parent, params object[] args);
        
        /// <summary>
        /// 从克隆的GameObject创建UI（用于列表项等动态创建场景）
        /// </summary>
        /// <param name="clonedGameObject">已克隆的GameObject</param>
        /// <param name="args">初始化参数</param>
        void OnCreateFromClone(GameObject clonedGameObject, params object[] args);
    }
}
