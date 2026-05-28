using System;

namespace GameDeveloperKit.UI
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class UIOption : Attribute
    {
        public UIOption(string uiPath, UILayer layer = UILayer.Background)
        {
            Path = uiPath;
            Layer = layer;
        }

        public string Path { get; }

        public UILayer Layer { get; }
    }
}
