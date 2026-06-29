using System;

namespace GameDeveloperKit.UI
{
    /// <summary>
    /// UI 层级，基于 int 顺序值。内置预设层级，支持项目自定义层级。
    /// </summary>
    public readonly struct UILayer : IEquatable<UILayer>
    {
        public static readonly UILayer Background = new UILayer(0, nameof(Background));
        public static readonly UILayer Main = new UILayer(100, nameof(Main));
        public static readonly UILayer Window = new UILayer(200, nameof(Window));
        public static readonly UILayer Loading = new UILayer(300, nameof(Loading));
        public static readonly UILayer Message = new UILayer(400, nameof(Message));
        public static readonly UILayer StoryPlayback = new UILayer(500, nameof(StoryPlayback));

        public int Order { get; }
        public string Name { get; }

        /// <summary>
        /// 创建自定义 UI 层级。
        /// </summary>
        /// <param name="order">排序值，值越大层级越高。</param>
        /// <param name="name">层级名称。</param>
        public UILayer(int order, string name)
        {
            Order = order;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public bool Equals(UILayer other) => Order == other.Order;
        public override bool Equals(object obj) => obj is UILayer other && Equals(other);
        public override int GetHashCode() => Order;
        public override string ToString() => Name ?? Order.ToString();

        public static bool operator ==(UILayer left, UILayer right) => left.Order == right.Order;
        public static bool operator !=(UILayer left, UILayer right) => left.Order != right.Order;

        public static UILayer FromOrder(int order)
        {
            // Fast path for built-in layers.
            if (order == Background.Order) return Background;
            if (order == Main.Order) return Main;
            if (order == Window.Order) return Window;
            if (order == Loading.Order) return Loading;
            if (order == Message.Order) return Message;
            if (order == StoryPlayback.Order) return StoryPlayback;
            return new UILayer(order, order.ToString());
        }
    }
}
