using System;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 磁盘空间不足异常
    /// </summary>
    public class InsufficientDiskSpaceException : Exception
    {
        /// <summary>
        /// 所需字节数
        /// </summary>
        public long RequiredBytes { get; }
        
        /// <summary>
        /// 可用字节数
        /// </summary>
        public long AvailableBytes { get; }
        
        public InsufficientDiskSpaceException(string message) : base(message)
        {
        }
        
        public InsufficientDiskSpaceException(string message, long requiredBytes, long availableBytes) 
            : base(message)
        {
            RequiredBytes = requiredBytes;
            AvailableBytes = availableBytes;
        }
        
        public InsufficientDiskSpaceException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}
