namespace GameDeveloperKit.Files
{
    /// <summary>
    /// 原始字节数据引用句柄
    /// 用于VFS模块返回字节数据
    /// </summary>
    public class RawHandle : BaseHandle
    {
        private byte[] _bytes;
        private string _name;
        private string _address;

        /// <summary>
        /// 资源字节数据
        /// </summary>
        public byte[] Bytes => _bytes;

        /// <summary>
        /// 资源名称
        /// </summary>
        public override string Name => _name;

        /// <summary>
        /// 资源地址
        /// </summary>
        public override string Address => _address;

        /// <summary>
        /// 资源GUID
        /// </summary>
        public override string GUID => _name;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="bytes">字节数据</param>
        /// <param name="name">名称</param>
        public RawHandle(byte[] bytes, string name)
        {
            _bytes = bytes;
            _name = name;
            _address = name;
        }

        protected override void OnDispose()
        {
            // VFS 模块处理释放逻辑
            Game.File?.UnloadHandle(this);
        }

        public override void OnClearup()
        {
            base.OnClearup();
            _bytes = null;
            _name = null;
            _address = null;
        }
    }
}
