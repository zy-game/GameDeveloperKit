using System;

namespace GameDeveloperKit.Story.Text
{
    internal sealed class LocalizationTextResolver : ITextResolver
    {
        private readonly Func<string, string> m_ResolveKey;

        public LocalizationTextResolver(Func<string, string> resolveKey = null)
        {
            m_ResolveKey = resolveKey ?? (key => App.Localization.GetText(key));
        }

        public string Resolve(TextReference reference)
        {
            return reference.Mode == TextMode.Literal
                ? reference.Value
                : m_ResolveKey(reference.Value);
        }
    }
}
