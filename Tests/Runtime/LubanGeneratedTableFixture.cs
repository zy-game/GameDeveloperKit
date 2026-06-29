using GameDeveloperKit.Config;
using Luban;
using Luban.SimpleJSON;
using Newtonsoft.Json;

namespace cfg
{
    public partial class Tables
    {
        public Tbtest Tbtest { get; }

        public Tables(System.Func<string, JSONNode> loader)
        {
            Tbtest = new Tbtest(loader("tbtest"));
            ResolveRef();
        }

        private void ResolveRef()
        {
            Tbtest.ResolveRef(this);
        }
    }

    public partial class Tbtest
    {
        private readonly System.Collections.Generic.Dictionary<int, test> m_DataMap;
        private readonly System.Collections.Generic.List<test> m_DataList;

        public Tbtest(JSONNode buffer)
        {
            var count = buffer.Count;
            m_DataMap = new System.Collections.Generic.Dictionary<int, test>(count);
            m_DataList = new System.Collections.Generic.List<test>(count);

            foreach (var element in buffer.Children)
            {
                if (!element.IsObject)
                {
                    throw new SerializationException();
                }

                var value = test.Deserializetest(element);
                m_DataList.Add(value);
                m_DataMap.Add(value.Id, value);
            }
        }

        public System.Collections.Generic.IReadOnlyDictionary<int, test> DataMap => m_DataMap;
        public System.Collections.Generic.IReadOnlyList<test> DataList => m_DataList;

        public test GetOrDefault(int key)
        {
            return m_DataMap.TryGetValue(key, out var value) ? value : default;
        }

        public test Get(int key)
        {
            return m_DataMap[key];
        }

        public test this[int key] => m_DataMap[key];

        public void ResolveRef(Tables tables)
        {
            foreach (var value in m_DataList)
            {
                value.ResolveRef(tables);
            }
        }
    }

    [TableOption("Packages/com.gamedeveloperkit.framework/Tests/Runtime/LubanGeneratedTableFixture.json")]
    public sealed partial class test : BeanBase, IConfig
    {
        public test(JSONNode buffer)
        {
            if (!buffer["id"].IsNumber)
            {
                throw new SerializationException();
            }

            if (!buffer["name"].IsString)
            {
                throw new SerializationException();
            }

            if (!buffer["desc"].IsString)
            {
                throw new SerializationException();
            }

            Id = buffer["id"];
            Name = buffer["name"];
            Desc = buffer["desc"];
        }

        [JsonConstructor]
        public test(int id, string name, string desc)
        {
            Id = id;
            Name = name;
            Desc = desc;
        }

        public static test Deserializetest(JSONNode buffer)
        {
            return new test(buffer);
        }

        [JsonProperty("id")]
        public int Id { get; }

        [JsonProperty("name")]
        public string Name { get; }

        [JsonProperty("desc")]
        public string Desc { get; }

        public const int __ID__ = 3556498;

        public override int GetTypeId()
        {
            return __ID__;
        }

        public Key key => new Key(nameof(Id), Id);

        public void ResolveRef(Tables tables)
        {
        }
    }
}
