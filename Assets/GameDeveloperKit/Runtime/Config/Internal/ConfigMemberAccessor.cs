using System;
using System.Reflection;

namespace GameDeveloperKit.Config.Internal
{
    internal sealed class ConfigMemberAccessor
    {
        private readonly FieldInfo m_Field;
        private readonly PropertyInfo m_Property;

        public ConfigMemberAccessor(FieldInfo field)
        {
            m_Field = field;
            MemberType = field.FieldType;
            Name = field.Name;
        }

        public ConfigMemberAccessor(PropertyInfo property)
        {
            m_Property = property;
            MemberType = property.PropertyType;
            Name = property.Name;
        }

        public string Name { get; }

        public Type MemberType { get; }

        public bool CanWrite => m_Field != null || m_Property.CanWrite;

        public object GetValue(object instance)
        {
            return m_Field != null ? m_Field.GetValue(instance) : m_Property.GetValue(instance);
        }

        public void SetValue(object instance, object value)
        {
            if (m_Field != null)
            {
                m_Field.SetValue(instance, value);
                return;
            }

            m_Property.SetValue(instance, value);
        }
    }
}
