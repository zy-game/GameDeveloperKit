namespace GameDeveloperKit.Data
{
    internal interface IDataContainerBase
    {
        void Remove(string key);
        bool Has(string key);
        string[] GetKeys();
    }
}
