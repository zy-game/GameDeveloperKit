namespace GameDeveloperKit.Localization
{
    public readonly struct LocalizationChangedEventArgs
    {
        public LocalizationChangedEventArgs(string previousLocale, string currentLocale)
        {
            PreviousLocale = previousLocale;
            CurrentLocale = currentLocale;
        }

        public string PreviousLocale { get; }

        public string CurrentLocale { get; }
    }
}
