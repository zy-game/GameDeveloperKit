using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Config.Internal;

namespace GameDeveloperKit.Config.Serializers
{
    public sealed class XmlConfigSerializer : IConfigSerializer
    {
        public ConfigFormat Format => ConfigFormat.Xml;

        public UniTask<IList> DeserializeAsync(ConfigSerializerContext context, Type rowType)
        {
            try
            {
                var document = XDocument.Parse(context.Payload.Text);
                var root = document.Root;
                if (root == null || !string.Equals(root.Name.LocalName, "rows", StringComparison.OrdinalIgnoreCase))
                {
                    throw new GameException($"Config source '{context.Source.Name}' XML root must be <rows>.");
                }

                var listType = typeof(List<>).MakeGenericType(rowType);
                var rows = (IList)Activator.CreateInstance(listType);
                foreach (var rowElement in root.Elements("row"))
                {
                    var row = Activator.CreateInstance(rowType);
                    foreach (var element in rowElement.Elements())
                    {
                        var member = ConfigTableBuilder.FindMember(rowType, element.Name.LocalName);
                        if (member == null)
                        {
                            throw new GameException(
                                $"Config source '{context.Source.Name}' XML element '{element.Name.LocalName}' has no matching member on row type '{rowType.FullName}'.");
                        }

                        if (!member.CanWrite)
                        {
                            throw new GameException(
                                $"Config source '{context.Source.Name}' XML element '{element.Name.LocalName}' matches read-only member on row type '{rowType.FullName}'.");
                        }

                        var value = ConvertValue(element.Value, member.MemberType, context.Source, rowType, member.Name);
                        member.SetValue(row, value);
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
                    $"Config source '{context.Source.Name}' XML deserialize failed. Location: {context.Source.Location}, row type: {rowType.FullName}.",
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
                    $"Config source '{source.Name}' XML value '{value}' cannot convert to '{targetType.FullName}' for row type '{rowType.FullName}' member '{memberName}'.",
                    exception);
            }
        }
    }
}
