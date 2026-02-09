using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Resource;
using UnityEngine;

namespace GameDeveloperKit.UI
{
    public interface IUIManager : IModule
    {
        /// <summary>
        /// UI 根节点 Canvas
        /// </summary>
        Canvas UIRoot { get; }

        /// <summary>
        /// 导航栈
        /// </summary>
        UIStack Stack { get; }

        /// <summary>
        /// 设置 SafeArea（刘海屏适配）
        /// </summary>
        void SetupSafeArea();

        /// <summary>
        /// 更新 SafeArea
        /// </summary>
        void UpdateSafeArea();

        /// <summary>
        /// 打开 UI（按类型）
        /// </summary>
        UniTask<T> OpenFormAsync<T>(params object[] args) where T : class, IUIForm, new();

        /// <summary>
        /// 关闭 UI
        /// </summary>
        void CloseForm<T>() where T : IUIForm;

        /// <summary>
        /// 关闭 UI（按名称）
        /// </summary>
        void CloseForm(string name);

        /// <summary>
        /// 检查 UI 是否打开
        /// </summary>
        bool IsOpen<T>() where T : IUIForm;

        /// <summary>
        /// 检查 UI 是否打开（按名称）
        /// </summary>
        bool IsOpen(string name);

        /// <summary>
        /// 获取form
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T GetForm<T>() where T : IUIForm;

        /// <summary>
        /// 显示指定层级
        /// </summary>
        void Show(params EUILayer[] layers);

        /// <summary>
        /// 隐藏指定层级
        /// </summary>
        void Hide(params EUILayer[] layers);

        /// <summary>
        /// 清理指定层级
        /// </summary>
        void Clearup(params EUILayer[] layers);

        /// <summary>
        /// 返回到指定 UI
        /// </summary>
        void BackTo<T>() where T : IUIForm;

        /// <summary>
        /// 返回上一个 UI
        /// </summary>
        void Back();

        /// <summary>
        /// 默认提示（调用CommonTips）
        /// </summary>
        /// <param name="message">提示内容</param>
        /// <param name="duration">显示时长（秒）</param>
        void DefaultNotice(string message, float duration = 2f);

        /// <summary>
        /// 默认弹窗（调用CommonDialog）
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="content">内容</param>
        /// <param name="confirmText">确认按钮文本</param>
        /// <param name="cancelText">取消按钮文本</param>
        /// <param name="showCancel">是否显示取消按钮</param>
        /// <returns>true=确认，false=取消</returns>
        UniTask<bool> DefaultDialogAsync(string title, string content,
            string confirmText = "确定", string cancelText = "取消", bool showCancel = true);

        /// <summary>
        /// 默认信息弹窗（仅确认按钮）
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="content">内容</param>
        /// <param name="confirmText">确认按钮文本</param>
        UniTask DefaultAlertAsync(string title, string content, string confirmText = "确定");
    }
}