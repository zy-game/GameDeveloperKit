using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace GameDeveloperKit.Config.Internal
{
    internal static class ConfigTableBuilder
    {
        public static IConfigTable Build(ConfigSourceDefinition source, Type rowType, IList rows)
        {
            if (rows == null)
            {
                throw new GameException($"Config source '{source.Name}' deserialized rows are null.");
            }

            var keyAccessor = ResolveKeyAccessor(source, rowType);
            var keyType = keyAccessor.MemberType;
            var rowListType = typeof(List<>).MakeGenericType(rowType);
            var typedRows = (IList)Activator.CreateInstance(rowListType);
            var dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(object), rowType);
            var rowsByKey = (IDictionary)Activator.CreateInstance(dictionaryType);

            foreach (var row in rows)
            {
                if (row == null)
                {
                    throw new GameException($"Config source '{source.Name}' contains a null row.");
                }

                if (!rowType.IsInstanceOfType(row))
                {
                    throw new GameException(
                        $"Config source '{source.Name}' row type mismatch. Expected '{rowType.FullName}', actual '{row.GetType().FullName}'.");
                }

                var key = keyAccessor.GetValue(row);
                if (key == null)
                {
                    throw new GameException(
                        $"Config source '{source.Name}' row type '{rowType.FullName}' key '{keyAccessor.Name}' is null.");
                }

                if (key.GetType() != keyType)
                {
                    throw new GameException(
                        $"Config source '{source.Name}' row type '{rowType.FullName}' key '{keyAccessor.Name}' returned '{key.GetType().FullName}', expected '{keyType.FullName}'.");
                }

                if (rowsByKey.Contains(key))
                {
                    throw new GameException(
                        $"Config source '{source.Name}' contains duplicate key '{key}'.");
                }

                typedRows.Add(row);
                rowsByKey.Add(key, row);
            }

            var tableType = typeof(ConfigTable<>).MakeGenericType(rowType);
            return (IConfigTable)Activator.CreateInstance(
                tableType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[] { source.Name, keyType, typedRows, rowsByKey },
                null);
        }

        private static ConfigMemberAccessor ResolveKeyAccessor(ConfigSourceDefinition source, Type rowType)
        {
            if (!string.IsNullOrWhiteSpace(source.KeyField))
            {
                var member = FindMember(rowType, source.KeyField);
                if (member != null)
                {
                    return member;
                }

                throw new GameException(
                    $"Config source '{source.Name}' row type '{rowType.FullName}' does not contain key field '{source.KeyField}'.");
            }

            ConfigMemberAccessor found = null;
            foreach (var field in rowType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.GetCustomAttribute<ConfigKeyAttribute>() == null)
                {
                    continue;
                }

                if (found != null)
                {
                    throw new GameException(
                        $"Config source '{source.Name}' row type '{rowType.FullName}' has multiple ConfigKeyAttribute members.");
                }

                found = new ConfigMemberAccessor(field);
            }

            foreach (var property in rowType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (property.GetCustomAttribute<ConfigKeyAttribute>() == null)
                {
                    continue;
                }

                if (!property.CanRead)
                {
                    throw new GameException(
                        $"Config source '{source.Name}' row type '{rowType.FullName}' key property '{property.Name}' is not readable.");
                }

                if (found != null)
                {
                    throw new GameException(
                        $"Config source '{source.Name}' row type '{rowType.FullName}' has multiple ConfigKeyAttribute members.");
                }

                found = new ConfigMemberAccessor(property);
            }

            if (found != null)
            {
                return found;
            }

            throw new GameException(
                $"Config source '{source.Name}' row type '{rowType.FullName}' has no key field. Set KeyField or mark one member with ConfigKeyAttribute.");
        }

        public static ConfigMemberAccessor FindMember(Type rowType, string memberName)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase;
            var field = rowType.GetField(memberName, flags);
            if (field != null)
            {
                return new ConfigMemberAccessor(field);
            }

            var property = rowType.GetProperty(memberName, flags);
            if (property != null)
            {
                if (!property.CanRead)
                {
                    throw new GameException(
                        $"Config row type '{rowType.FullName}' property '{property.Name}' is not readable.");
                }

                return new ConfigMemberAccessor(property);
            }

            return null;
        }
    }
}
