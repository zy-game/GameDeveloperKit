using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Config.Internal;
using GameDeveloperKit.Config.Serializers;
using UnityEngine;

namespace GameDeveloperKit.Config
{
    public sealed class ConfigModule : GameModuleBase
    {
        private readonly Dictionary<string, ConfigSourceDefinition> m_Sources =
            new Dictionary<string, ConfigSourceDefinition>(StringComparer.Ordinal);

        private readonly Dictionary<ConfigFormat, IConfigSerializer> m_Serializers =
            new Dictionary<ConfigFormat, IConfigSerializer>();

        private readonly Dictionary<string, IConfigTable> m_Tables =
            new Dictionary<string, IConfigTable>(StringComparer.Ordinal);

        private readonly Dictionary<string, UniTaskCompletionSource<IConfigTable>> m_PendingLoads =
            new Dictionary<string, UniTaskCompletionSource<IConfigTable>>(StringComparer.Ordinal);

        public override async UniTask Startup()
        {
            Clear();
            RegisterBuiltInSerializers();

            var settings = Resources.Load<ConfigSettings>("ConfigSettings");
            if (settings == null)
            {
                return;
            }

            foreach (var source in settings.Sources)
            {
                RegisterSource(source);
            }

            foreach (var source in settings.Sources)
            {
                if (source != null && source.Preload)
                {
                    await LoadTableBySourceAsync(source);
                }
            }
        }

        public override UniTask Shutdown()
        {
            Clear();
            return UniTask.CompletedTask;
        }

        public void RegisterSource(ConfigSourceDefinition source)
        {
            ValidateSource(source);

            if (m_Sources.ContainsKey(source.Name))
            {
                throw new GameException($"Config source '{source.Name}' has already been registered.");
            }

            m_Sources.Add(source.Name, source);
        }

        public void RegisterSerializer(IConfigSerializer serializer)
        {
            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            m_Serializers[serializer.Format] = serializer;
        }

        public async UniTask<ConfigTable<TRow>> LoadTableAsync<TRow>(string name)
        {
            ValidateName(name);

            if (m_Tables.TryGetValue(name, out var cached))
            {
                return CastTable<TRow>(name, cached);
            }

            if (m_PendingLoads.TryGetValue(name, out var pending))
            {
                return CastTable<TRow>(name, await pending.Task);
            }

            if (!m_Sources.TryGetValue(name, out var source))
            {
                throw new GameException($"Config source '{name}' is not registered.");
            }

            var completionSource = new UniTaskCompletionSource<IConfigTable>();
            m_PendingLoads.Add(name, completionSource);

            try
            {
                var table = await LoadTableBySourceAsync(source);
                completionSource.TrySetResult(table);
                return CastTable<TRow>(name, table);
            }
            catch (Exception exception)
            {
                completionSource.TrySetException(exception);
                try
                {
                    await completionSource.Task;
                }
                catch
                {
                }

                throw;
            }
            finally
            {
                m_PendingLoads.Remove(name);
            }
        }

        public ConfigTable<TRow> GetTable<TRow>(string name)
        {
            ValidateName(name);

            if (!m_Tables.TryGetValue(name, out var table))
            {
                throw new GameException($"Config table '{name}' is not loaded.");
            }

            return CastTable<TRow>(name, table);
        }

        public bool TryGetTable<TRow>(string name, out ConfigTable<TRow> table)
        {
            ValidateName(name);

            if (m_Tables.TryGetValue(name, out var value))
            {
                table = CastTable<TRow>(name, value);
                return true;
            }

            table = null;
            return false;
        }

        public bool TryGetRow<TRow>(string name, object key, out TRow row)
        {
            var table = GetTable<TRow>(name);
            return table.TryGet(key, out row);
        }

        public void Unload(string name)
        {
            ValidateName(name);
            m_Tables.Remove(name);
        }

        internal bool HasSource(string name)
        {
            return m_Sources.ContainsKey(name);
        }

        private async UniTask<IConfigTable> LoadTableBySourceAsync(ConfigSourceDefinition source)
        {
            if (!m_Serializers.TryGetValue(source.Format, out var serializer))
            {
                throw new GameException($"Config source '{source.Name}' format '{source.Format}' has no serializer.");
            }

            var rowType = ConfigTypeUtility.ResolveRowType(source);
            var payload = await ConfigPayloadResolver.ResolveAsync(source);
            var context = new ConfigSerializerContext(source, payload);
            var rows = await serializer.DeserializeAsync(context, rowType);
            var table = ConfigTableBuilder.Build(source, rowType, rows);
            m_Tables[source.Name] = table;
            return table;
        }

        private void RegisterBuiltInSerializers()
        {
            RegisterSerializer(new JsonConfigSerializer());
            RegisterSerializer(new CsvConfigSerializer());
            RegisterSerializer(new XmlConfigSerializer());
            RegisterSerializer(new ScriptableObjectConfigSerializer());
        }

        private void Clear()
        {
            m_PendingLoads.Clear();
            m_Tables.Clear();
            m_Sources.Clear();
            m_Serializers.Clear();
        }

        private static ConfigTable<TRow> CastTable<TRow>(string name, IConfigTable table)
        {
            if (table is ConfigTable<TRow> typed)
            {
                return typed;
            }

            throw new GameException(
                $"Config table '{name}' row type mismatch. Expected '{typeof(TRow).FullName}', actual '{table.RowType.FullName}'.");
        }

        private static void ValidateSource(ConfigSourceDefinition source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            ValidateName(source.Name);

            if (string.IsNullOrWhiteSpace(source.RowTypeName))
            {
                throw new ArgumentException($"Config source '{source.Name}' row type cannot be empty.", nameof(source));
            }

            if (string.IsNullOrWhiteSpace(source.Location))
            {
                throw new ArgumentException($"Config source '{source.Name}' location cannot be empty.", nameof(source));
            }
        }

        private static void ValidateName(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Config name cannot be empty.", nameof(name));
            }
        }
    }
}
