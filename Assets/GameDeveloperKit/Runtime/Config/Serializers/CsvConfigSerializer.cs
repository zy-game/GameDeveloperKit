using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Config.Internal;

namespace GameDeveloperKit.Config.Serializers
{
    public sealed class CsvConfigSerializer : IConfigSerializer
    {
        public ConfigFormat Format => ConfigFormat.Csv;

        public UniTask<IList> DeserializeAsync(ConfigSerializerContext context, Type rowType)
        {
            try
            {
                var records = ParseRecords(context.Payload.Text);
                if (records.Count == 0)
                {
                    throw new GameException($"Config source '{context.Source.Name}' CSV has no header.");
                }

                var headers = records[0];
                if (headers.Count == 0)
                {
                    throw new GameException($"Config source '{context.Source.Name}' CSV header is empty.");
                }

                var members = new ConfigMemberAccessor[headers.Count];
                for (var i = 0; i < headers.Count; i++)
                {
                    var header = headers[i];
                    if (string.IsNullOrWhiteSpace(header))
                    {
                        throw new GameException($"Config source '{context.Source.Name}' CSV header contains an empty column.");
                    }

                    var member = ConfigTableBuilder.FindMember(rowType, header);
                    if (member == null)
                    {
                        throw new GameException(
                            $"Config source '{context.Source.Name}' CSV header '{header}' has no matching member on row type '{rowType.FullName}'.");
                    }

                    if (!member.CanWrite)
                    {
                        throw new GameException(
                            $"Config source '{context.Source.Name}' CSV header '{header}' matches read-only member on row type '{rowType.FullName}'.");
                    }

                    members[i] = member;
                }

                var listType = typeof(List<>).MakeGenericType(rowType);
                var rows = (IList)Activator.CreateInstance(listType);
                for (var rowIndex = 1; rowIndex < records.Count; rowIndex++)
                {
                    var record = records[rowIndex];
                    if (record.Count == 1 && string.IsNullOrEmpty(record[0]))
                    {
                        continue;
                    }

                    if (record.Count != headers.Count)
                    {
                        throw new GameException(
                            $"Config source '{context.Source.Name}' CSV row {rowIndex + 1} has {record.Count} columns, expected {headers.Count}.");
                    }

                    var row = Activator.CreateInstance(rowType);
                    for (var column = 0; column < headers.Count; column++)
                    {
                        var value = ConvertValue(record[column], members[column].MemberType, context.Source, rowType, headers[column]);
                        members[column].SetValue(row, value);
                    }

                    rows.Add(row);
                }

                return UniTask.FromResult(rows);
            }
            catch (GameException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new GameException(
                    $"Config source '{context.Source.Name}' CSV deserialize failed. Location: {context.Source.Location}, row type: {rowType.FullName}.",
                    exception);
            }
        }

        private static object ConvertValue(string value, Type targetType, ConfigSourceDefinition source, Type rowType, string memberName)
        {
            try
            {
                if (targetType == typeof(string))
                {
                    return value;
                }

                if (targetType.IsEnum)
                {
                    return Enum.Parse(targetType, value);
                }

                var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
                if (string.IsNullOrEmpty(value) && Nullable.GetUnderlyingType(targetType) != null)
                {
                    return null;
                }

                return Convert.ChangeType(value, type);
            }
            catch (Exception exception)
            {
                throw new GameException(
                    $"Config source '{source.Name}' CSV value '{value}' cannot convert to '{targetType.FullName}' for row type '{rowType.FullName}' member '{memberName}'.",
                    exception);
            }
        }

        private static List<List<string>> ParseRecords(string text)
        {
            var records = new List<List<string>>();
            var record = new List<string>();
            var field = new System.Text.StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }

                    continue;
                }

                if (c == '"')
                {
                    inQuotes = true;
                    continue;
                }

                if (c == ',')
                {
                    record.Add(field.ToString());
                    field.Clear();
                    continue;
                }

                if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    record.Add(field.ToString());
                    field.Clear();
                    records.Add(record);
                    record = new List<string>();
                    continue;
                }

                field.Append(c);
            }

            if (inQuotes)
            {
                throw new GameException("CSV field quote is not closed.");
            }

            record.Add(field.ToString());
            records.Add(record);
            return records;
        }
    }
}
