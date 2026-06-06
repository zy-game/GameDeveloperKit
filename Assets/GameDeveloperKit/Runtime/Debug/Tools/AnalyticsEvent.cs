using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Logger
{
    public readonly struct AnalyticsEvent
    {
        public AnalyticsEvent(string name, DateTimeOffset timestamp, IReadOnlyDictionary<string, object> properties)
        {
            Name = name;
            Timestamp = timestamp;
            Properties = properties;
        }

        public string Name { get; }

        public DateTimeOffset Timestamp { get; }

        public IReadOnlyDictionary<string, object> Properties { get; }
    }
}
