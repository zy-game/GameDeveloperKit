using GameDeveloperKit.Config;
using GameDeveloperKit.EditorConfiguration;

namespace GameDeveloperKit.TagEditor
{
    /// <summary>
    /// 定义 Tag Catalog Editor Store 类型。tagCatalog 数据持久化在 GDKSetting.json。
    /// </summary>
    internal static class TagCatalogEditorStore
    {
        /// <summary>
        /// 加载 Or Create。
        /// </summary>
        /// <returns>执行结果。</returns>
        public static TagCatalogSettings LoadOrCreate()
        {
            var settings = GdkSettingsEditorStore.LoadOrCreate();
            return settings.TagCatalog;
        }

        /// <summary>
        /// 保存 member。
        /// </summary>
        /// <param name="catalog">catalog 参数。</param>
        public static void Save(TagCatalogSettings catalog)
        {
            if (catalog == null)
            {
                return;
            }

            catalog.EnsureDefaults();
            var settings = GdkSettingsEditorStore.LoadOrCreate();
            settings.TagCatalog = catalog;
            GdkSettingsEditorStore.Save(settings);
        }
    }
}
