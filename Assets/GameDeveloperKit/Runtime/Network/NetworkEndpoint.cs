using System;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络连接地址。
    /// </summary>
    public sealed class NetworkEndpoint
    {
        public NetworkEndpoint(string address)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Network endpoint address cannot be empty.", nameof(address));
            }

            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException("Network endpoint address must be an absolute URI.", nameof(address));
            }

            Address = address;
            Uri = uri;
        }

        public string Address { get; }

        public Uri Uri { get; }

        public override string ToString()
        {
            return Address;
        }
    }
}
