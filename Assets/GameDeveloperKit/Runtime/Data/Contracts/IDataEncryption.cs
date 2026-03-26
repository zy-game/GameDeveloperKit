namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 数据加密接口，定义数据加密和解密的基本契约。
    /// </summary>
    public interface IDataEncryption
    {
        /// <summary>
        /// 加密数据。
        /// </summary>
        /// <param name="data">要加密的数据。</param>
        /// <returns>加密后的数据。</returns>
        byte[] Encrypt(byte[] data);

        /// <summary>
        /// 解密数据。
        /// </summary>
        /// <param name="data">要解密的数据。</param>
        /// <returns>解密后的数据。</returns>
        byte[] Decrypt(byte[] data);
    }
}
