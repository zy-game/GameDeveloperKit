namespace GameDeveloperKit.Config
{
    public sealed class ConfigSerializerContext
    {
        public ConfigSerializerContext(ConfigSourceDefinition source, ConfigSourcePayload payload)
        {
            Source = source;
            Payload = payload;
        }

        public ConfigSourceDefinition Source { get; }

        public ConfigSourcePayload Payload { get; }
    }
}
