using System.Collections.Generic;

namespace Varwin
{
    /// <summary>
    /// Класс, описывающий данные поля в инспекторе.
    /// </summary>
    public class InspectorPropertyData
    {
        public string ComponentPropertyName { get; set; }
        public PropertyValue PropertyValue { get; set; }

        public static InspectorPropertyData CreateWithValue(string componentPropertyName, object value)
        {
            return new InspectorPropertyData
            {
                ComponentPropertyName = componentPropertyName,
                PropertyValue = new PropertyValue
                {
                    Value = value,
                    ResourceGuid = null,
                    ResourceGuids = null
                }
            };
        }
    }

    /// <summary>
    /// Класс, описывающий значение поля в инспекторе.
    /// </summary>
    public class PropertyValue
    {
        public string ResourceGuid { get; set; }

        public object Value;

        public List<string> ResourceGuids { get; set; }

        public object GetPropertyRealValue(int index = -1)
        {
            if (Value != null)
            {
                return Value;
            }

            if (index != -1)
            {
                return index >= ResourceGuids.Count ? null : ResourceGuids[index];
            }

            return ResourceGuid;
        }

        public string GetPropertyValueString() => Value != null ? Value.ToString() : ResourceGuid;
    }

    /// <summary>
    /// Класс с мета-инфой, чтобы сязать поле инспектора с объектом, которому оно принадлежит.
    /// </summary>
    public class InspectorPropertyInfo
    {
        public int ControllerId { get; }
        public ObjectController Controller { get; }
        public InspectorProperty Property { get; }

        public object PreModifyValue { get; set; }

        public object PreviousValue { get; set; }

        public InspectorPropertyInfo(ObjectController controller, InspectorProperty property)
        {
            ControllerId = controller.Id;
            Controller = controller;
            Property = property;
        }
    }
}