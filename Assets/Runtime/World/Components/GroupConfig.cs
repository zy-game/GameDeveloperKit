using Massive;

namespace GameDeveloperKit.World
{
    /// <summary>
    /// System组配置
    /// 用于声明System对哪些组件组合感兴趣
    /// </summary>
    public class GroupConfig
    {
        public IIncludeSelector IncludeSelector { get; }
        public IExcludeSelector ExcludeSelector { get; }

        public GroupConfig(IIncludeSelector includeSelector)
            : this(includeSelector, null)
        {
        }

        public GroupConfig(IExcludeSelector excludeSelector)
            : this(null, excludeSelector)
        {
        }

        public GroupConfig(IIncludeSelector includeSelector, IExcludeSelector excludeSelector)
        {
            IncludeSelector = includeSelector;
            ExcludeSelector = excludeSelector;
        }
    }
}
