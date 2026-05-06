using System;

namespace GameDeveloperKit
{
    public sealed class GameFrameworkException : Exception
    {
        public GameFrameworkException(string message) : base(message)
        {
        }
    }
}
