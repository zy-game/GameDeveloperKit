using System;
using GameDeveloperKit.Procedure;
using GameDeveloperKit.Resource;
using UnityEngine;

namespace GameDeveloperKit
{
    public class Startup : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("自定义 Procedure 类型的完全限定名 (例如: MyGame.Procedures.MyGameplayProcedure)")]
        private string customProcedureTypeName = "";

        [SerializeField]
        [Tooltip("在 PreloadProcedure 中需要预加载的首包（BasePackage）列表")]
        private string[] _preloadBasePackages = Array.Empty<string>();

        [SerializeField]
        [Tooltip("资源加载模式")]
        private GameDeveloperKit.Resource.EResourceMode _resourceMode = GameDeveloperKit.Resource.EResourceMode.EditorSimulator;

        [SerializeField]
        [Tooltip("资源更新地址")]
        private string _resourceUpdateUrl = "http://localhost:8080";

        [SerializeField]
        [Tooltip("Web服务器地址")]
        private string _webServerUrl = "http://localhost:8080";

        /// <summary>
        /// 获取自定义 Procedure 类型名
        /// </summary>
        public string CustomProcedureTypeName => customProcedureTypeName;

        /// <summary>
        /// 获取需要预加载的首包列表
        /// </summary>
        public string[] PreloadBasePackages => _preloadBasePackages ?? Array.Empty<string>();

        /// <summary>
        /// 资源加载模式
        /// </summary>
        public EResourceMode ResourceMode => _resourceMode;

        /// <summary>
        /// 资源更新地址
        /// </summary>
        public string ResourceUpdateUrl => _resourceUpdateUrl;

        /// <summary>
        /// Web服务器地址
        /// </summary>
        public string WebServerUrl => _webServerUrl;

        private void Start()
        {
            Game.Startup(this);
            GameObject.DontDestroyOnLoad(this.gameObject);
        }

        private void Update()
        {
            Game.Update();
        }

        private void OnApplicationQuit()
        {
            Game.Shutdown();
        }
    }
}