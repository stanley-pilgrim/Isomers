using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DesperateDevs.Utils;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Varwin.Core;
using Varwin.Core.Behaviours;
using Varwin.Data;
using Varwin.Data.ServerData;
using Varwin.Public;
using Varwin.WWW;
using Object = UnityEngine.Object;

namespace Varwin
{
    public struct ChangeArrayMember
    {
        public int Index;
        public object Value;
        public Type ValueType;
    }

    public struct CreateArrayMember
    {
        public object NewMember;
        public int Index;

        public override string ToString()
        {
            return NewMember.ToString();
        }
    }

    public struct RemoveArrayMember
    {
        public List<int> Indices;
    }

    public class InspectorController
    {
        public const int InvalidArrayIndex = -1;

        /// <summary>
        /// Event after changed property value
        /// </summary>
        public event Action<string, object> PropertyValueChanged;

        /// <summary>
        /// Properties with VarwinInspector
        /// </summary>
        public Dictionary<string, InspectorProperty> InspectorProperties { get; }

        /// <summary>
        /// Methods with VarwinInspector
        /// </summary>
        public Dictionary<string, InspectorMethod> InspectorMethods { get; }

        /// <summary>
        /// Set of Inspector Components types
        /// </summary>
        public HashSet<Type> InspectorComponentsTypes { get; }

        public List<string> ObjectBehaviours { get; }

        private Dictionary<string, InspectorProperty> SerializableProperties { get; }
        private Dictionary<string, ResourceObject> UsingResources { get; set; }
        private List<InspectorPropertyData> InspectorPropertiesData { get; }

        private VarwinObjectDescriptor _varwinObjectDescriptor;

        private readonly Dictionary<PropertyInfo, PropertyWrapper> _propertyWrappers = new();
        private readonly Dictionary<string, object> _propertyDefaults = new();
        private readonly ObjectController _objectController;

        public InspectorController(ObjectController objectController, List<InspectorPropertyData> propertyDatas)
        {
            _objectController = objectController;

            _varwinObjectDescriptor = objectController.VarwinObjectDescriptor;
            SerializableProperties = new Dictionary<string, InspectorProperty>();
            InspectorProperties = new Dictionary<string, InspectorProperty>();
            InspectorMethods = new Dictionary<string, InspectorMethod>();
            InspectorComponentsTypes = new HashSet<Type>();
            ObjectBehaviours = new List<string>();

            ObjectBehaviours.AddRange(BehavioursCollection.GetBehaviours(objectController));

            if (_varwinObjectDescriptor && _varwinObjectDescriptor.AddBehavioursAtRuntime)
            {
                ObjectBehaviours.AddRange(BehavioursCollection.AddBehaviours(objectController));
                _varwinObjectDescriptor.Components.SetupRuntimeBehaviours(objectController.RootGameObject);
            }

            InspectorPropertiesData = new List<InspectorPropertyData>();
            if (propertyDatas != null)
            {
                foreach (var inspectorPropertyData in propertyDatas)
                {
                    InspectorPropertiesData.Add(new InspectorPropertyData
                    {
                        ComponentPropertyName = inspectorPropertyData.ComponentPropertyName,
                        PropertyValue = new PropertyValue
                        {
                            ResourceGuids = inspectorPropertyData.PropertyValue?.ResourceGuids,
                            ResourceGuid = inspectorPropertyData.PropertyValue?.ResourceGuid,
                            Value = inspectorPropertyData.PropertyValue?.Value
                        }
                    });
                }
            }
        }

        ~InspectorController()
        {
            Destroy();
        }

        public void Destroy()
        {
            foreach (InspectorPropertyData inspectorPropertyData in InspectorPropertiesData.Where(x => x.PropertyValue != null))
            {
                inspectorPropertyData.PropertyValue.Value = null;
                inspectorPropertyData.PropertyValue = null;
            }

            InspectorPropertiesData.Clear();

            foreach (var serializableProperty in SerializableProperties.Where(x => x.Value != null))
            {
                if (serializableProperty.Value.ComponentReference != null)
                {
                    serializableProperty.Value.ComponentReference.Component = null;
                    serializableProperty.Value.ComponentReference = null;
                }

                if (serializableProperty.Value?.Data?.PropertyValue != null)
                {
                    serializableProperty.Value.Data.PropertyValue.Value = null;
                    serializableProperty.Value.Data.PropertyValue = null;
                }

                serializableProperty.Value.Data = null;
            }

            SerializableProperties.Clear();

            foreach (var inspectorProperty in InspectorProperties.Where(x => x.Value != null))
            {
                if (inspectorProperty.Value.ComponentReference != null)
                {
                    inspectorProperty.Value.ComponentReference.Component = null;
                    inspectorProperty.Value.ComponentReference = null;
                }

                if (inspectorProperty.Value?.Data?.PropertyValue != null)
                {
                    inspectorProperty.Value.Data.PropertyValue.Value = null;
                    inspectorProperty.Value.Data.PropertyValue = null;
                }

                inspectorProperty.Value.Data = null;
            }

            SerializableProperties.Clear();

            _varwinObjectDescriptor = null;
        }

        public void InitInspectorFields()
        {
            if (!_varwinObjectDescriptor || _varwinObjectDescriptor.Components == null || _varwinObjectDescriptor.Components.Count == 0)
            {
                return;
            }

            RefreshFromComponents();
        }

        public void InitResources()
        {
            UsingResources = new Dictionary<string, ResourceObject>();

            var usingResourcesGuid = new List<string>();

            foreach (InspectorPropertyData propertyInfo in InspectorPropertiesData)
            {
                if (propertyInfo.PropertyValue.ResourceGuids != null)
                {
                    usingResourcesGuid.AddRange(propertyInfo.PropertyValue.ResourceGuids.Where(x => !string.IsNullOrEmpty(x)));
                }

                string resourceGuid = propertyInfo.PropertyValue.ResourceGuid;

                if (!string.IsNullOrEmpty(resourceGuid))
                {
                    usingResourcesGuid.Add(resourceGuid);
                }
            }

            foreach (string guid in usingResourcesGuid)
            {
                ResourceObject resource = GameStateData.GetResource(guid);
                if (resource == null)
                {
                    Debug.LogError($"Resource {guid} not found");
                    continue;
                }

                if (!UsingResources.ContainsKey(guid))
                {
                    AddUsingResource(resource);
                }
            }

            RequestManager.Instance.StartCoroutine(SetPropertiesWithDelay());
        }

        public object GetInspectorPropertyValue(string componentPropertyName)
        {
            var serializablePropertyPair = SerializableProperties.FirstOrDefault(x => x.Key == componentPropertyName);

            InspectorProperty serializableProperty = serializablePropertyPair.Value;

            if (serializableProperty.Data == null)
            {
                return null;
            }

            return serializableProperty.IsResource
                ? serializableProperty.Data.PropertyValue.ResourceGuid
                : serializableProperty.Data.PropertyValue.Value;
        }

        public object GetInspectorPropertyRealValue(string componentPropertyName)
        {
            KeyValuePair<string, InspectorProperty> serializablePropertyPair = SerializableProperties.FirstOrDefault(x => x.Key == componentPropertyName);
            InspectorProperty serializableProperty = serializablePropertyPair.Value;

            return _propertyWrappers[serializableProperty.PropertyInfo].Getter(serializableProperty.ComponentReference.Component);
        }

        public T GetInspectorPropertyValue<T>(string componentPropertyName)
        {
            KeyValuePair<string, InspectorProperty> serializableProperty = SerializableProperties.FirstOrDefault(x => x.Key == componentPropertyName);
            return (T) _propertyWrappers[serializableProperty.Value.PropertyInfo].Getter(serializableProperty.Value.ComponentReference.Component);
        }

        private static IList ConvertArrayToList(object array)
        {
            Type itemType = array.GetType().GetElementType();
            Type genericListType = typeof(List<>).MakeGenericType(itemType);
            return Activator.CreateInstance(genericListType, array) as IList;
        }

        public void SetInspectorPropertyValue(string componentPropertyName, object value, bool isArrayMember = false, int index = InvalidArrayIndex)
        {
            if (!isArrayMember)
            {
                SetInspectorPropertyValueSingle(componentPropertyName, value);
                return;
            }

            var inspectorPropertyData = InspectorPropertiesData.FirstOrDefault(x => x.ComponentPropertyName == componentPropertyName);
            var valueType = inspectorPropertyData?.PropertyValue?.Value?.GetType();
            if (valueType is { IsArray: true })
            {
                inspectorPropertyData.PropertyValue.Value = ConvertArrayToList(inspectorPropertyData.PropertyValue.Value);
            }

            switch (value)
            {
                case CreateArrayMember createArrayMember:
                    var listExists = inspectorPropertyData?.PropertyValue?.Value != null;
                    if (!listExists)
                    {
                        Type newListType = typeof(List<>).MakeGenericType(createArrayMember.NewMember.GetType());
                        var newList = (IList) Activator.CreateInstance(newListType);
                        newList.Add(createArrayMember.NewMember);

                        if (inspectorPropertyData == null)
                        {
                            inspectorPropertyData = InspectorPropertyData.CreateWithValue(componentPropertyName, newList);
                            InspectorPropertiesData.Add(inspectorPropertyData);
                        }
                        else
                        {
                            inspectorPropertyData.PropertyValue = new PropertyValue()
                            {
                                ResourceGuid = null,
                                ResourceGuids = null,
                                Value = newList
                            };
                        }

                        InspectorProperties[componentPropertyName].Data = inspectorPropertyData;
                        SerializableProperties[componentPropertyName].Data = inspectorPropertyData;
                    }
                    else
                    {
                        (inspectorPropertyData.PropertyValue.Value as IList)?.Insert(createArrayMember.Index, createArrayMember.NewMember);
                    }

                    break;
                case RemoveArrayMember removeArrayMember:
                {
                    if (inspectorPropertyData != null)
                    {
                        int[] indices = removeArrayMember.Indices.OrderBy(x => x).ToArray();
                        for (int indexToRemove = indices.Length - 1; indexToRemove >= 0; indexToRemove--)
                        {
                            (inspectorPropertyData.PropertyValue.Value as IList)?.RemoveAt(indices[indexToRemove]);
                        }
                    }

                    break;
                }
                case ChangeArrayMember changeArrayMember:
                {
                    if (inspectorPropertyData != null)
                    {
                        Converter.CastValue(changeArrayMember.ValueType, changeArrayMember.Value, out object convertedItem);
                        ((IList) inspectorPropertyData.PropertyValue.Value)[index] = convertedItem;
                    }

                    break;
                }
            }

            InspectorProperty serializedProperty = GetSerializedInspectorProperty(componentPropertyName);
            if (serializedProperty == null)
            {
                return;
            }

            var propertyValue = inspectorPropertyData?.PropertyValue?.Value;
            if (inspectorPropertyData == null)
            {
                serializedProperty.Data = InspectorPropertyData.CreateWithValue(serializedProperty.Name, propertyValue);
                InspectorPropertiesData.Add(serializedProperty.Data);
            }
            else
            {
                serializedProperty.Data = inspectorPropertyData;
            }

            var isArray = serializedProperty.PropertyInfo.PropertyType.IsArray;
            if (isArray)
            {
                SetArrayPropertyValue(serializedProperty.ComponentReference, serializedProperty.PropertyInfo, propertyValue);
            }
            else
            {
                SetListPropertyValue(serializedProperty.ComponentReference, serializedProperty.PropertyInfo, propertyValue);
            }

            PropertyValueChanged?.Invoke(componentPropertyName, propertyValue);
        }

        public void SetSerializablePropertyValueSingle(string propertyName, object value)
        {
            var findedProperties = SerializableProperties.Where(propertyData => propertyData.Value.PropertyInfo.Name == propertyName);
            foreach (var propertyData in findedProperties)
            {
                propertyData.Value.Data.PropertyValue.Value = value;
                propertyData.Value.Data.PropertyValue.ResourceGuid = null;
            }
        }

        private void SetInspectorPropertyValueSingle(string componentPropertyName, object value)
        {
            var existingProperty = false;

            foreach (InspectorPropertyData propertyData in InspectorPropertiesData.Where(propertyData => propertyData.ComponentPropertyName == componentPropertyName))
            {
                propertyData.PropertyValue.Value = value;
                propertyData.PropertyValue.ResourceGuid = null;
                existingProperty = true;
            }

            if (!existingProperty)
            {
                var data = new InspectorPropertyData
                {
                    ComponentPropertyName = componentPropertyName,
                    PropertyValue = new PropertyValue
                    {
                        ResourceGuid = null,
                        Value = value
                    }
                };

                InspectorPropertiesData.Add(data);
            }

            SetPropertyValue(componentPropertyName, value);
        }

        public void SetInspectorPropertyResourceValue(string componentPropertyName, object value, bool isArrayMember = false, int index = InvalidArrayIndex)
        {
            if (!isArrayMember)
            {
                SetInspectorPropertyResourceValueSingle(componentPropertyName, (string) value);
                return;
            }

            InspectorPropertyData inspectorPropertyData = InspectorPropertiesData.FirstOrDefault(x => x.ComponentPropertyName == componentPropertyName);

            switch (value)
            {
                case CreateArrayMember newMember:
                {
                    if (inspectorPropertyData == null)
                    {
                        inspectorPropertyData = new InspectorPropertyData
                        {
                            ComponentPropertyName = componentPropertyName,
                            PropertyValue = new PropertyValue
                            {
                                Value = null,
                                ResourceGuids = new List<string>()
                            }
                        };

                        InspectorPropertiesData.Add(inspectorPropertyData);
                        InspectorProperties[componentPropertyName].Data = inspectorPropertyData;
                        SerializableProperties[componentPropertyName].Data = inspectorPropertyData;
                    }

                    if (inspectorPropertyData.PropertyValue.ResourceGuids == null)
                    {
                        inspectorPropertyData.PropertyValue.Value = null;
                        inspectorPropertyData.PropertyValue.ResourceGuids = new List<string>();
                    }

                    var guid = (string) newMember.NewMember;
                    inspectorPropertyData.PropertyValue.ResourceGuids.Insert(newMember.Index, guid);

                    if (!string.IsNullOrEmpty(guid))
                    {
                        SetInspectorPropertyResourceValue(componentPropertyName, guid, true, newMember.Index);
                    }

                    SetPropertyResourcesValue(componentPropertyName, inspectorPropertyData.PropertyValue.ResourceGuids);

                    break;
                }

                case RemoveArrayMember removeArrayMember:
                {
                    if (inspectorPropertyData != null)
                    {
                        int[] indices = removeArrayMember.Indices.OrderBy(x => x).ToArray();
                        for (int indexToRemove = indices.Length - 1; indexToRemove >= 0; indexToRemove--)
                        {
                            string removedGuid = inspectorPropertyData.PropertyValue.ResourceGuids[indices[indexToRemove]];
                            inspectorPropertyData.PropertyValue.ResourceGuids.RemoveAt(indices[indexToRemove]);

                            if (!string.IsNullOrEmpty(removedGuid) && UsingResources.ContainsKey(removedGuid) && !IsResourceInUse(removedGuid))
                            {
                                RemoveUsingResource(removedGuid);
                            }
                        }
                    }

                    SetPropertyResourcesValue(componentPropertyName, inspectorPropertyData.PropertyValue.ResourceGuids);

                    break;
                }
                case string resourceGuid:
                {
                    if (inspectorPropertyData != null)
                    {
                        string oldResourceGuid = inspectorPropertyData.PropertyValue.ResourceGuids[index];
                        inspectorPropertyData.PropertyValue.ResourceGuids[index] = resourceGuid;

                        if (!string.IsNullOrEmpty(resourceGuid))
                        {
                            API.GetResourceByGuid(resourceGuid, GetResourceCallback);
                        }

                        void GetResourceCallback(ResourceDto resourceData)
                        {
                            if (string.IsNullOrEmpty(resourceData.Guid))
                            {
                                Debug.LogError($"Resource with {resourceGuid} not found", _objectController.VarwinObjectDescriptor);
                                return;
                            }

                            InspectorProperty inspectorProperty = GetSerializedInspectorProperty(componentPropertyName);

                            if (inspectorProperty.OnDemand)
                            {
                                resourceData.OnDemand = true;
                            }

                            LoaderAdapter.LoadResources(resourceData);
                            ProjectData.ResourcesLoaded += SetArrayValue;
                        }

                        void SetArrayValue()
                        {
                            ProjectData.ResourcesLoaded -= SetArrayValue;

                            var needToRemoveOldResource = !string.IsNullOrEmpty(oldResourceGuid) 
                                                          && UsingResources.ContainsKey(oldResourceGuid) 
                                                          && !IsResourceInUse(oldResourceGuid); 
                            if (needToRemoveOldResource)
                            {
                                RemoveUsingResource(oldResourceGuid);
                            }

                            var needToAddNewResource = !string.IsNullOrEmpty(resourceGuid) && !UsingResources.ContainsKey(resourceGuid);
                            if (needToAddNewResource)
                            {
                                AddUsingResource(resourceGuid);
                            }

                            SetPropertyResourcesValue(componentPropertyName, inspectorPropertyData.PropertyValue.ResourceGuids);
                        }
                    }

                    break;
                }
                case null:
                {
                    if (inspectorPropertyData != null)
                    {
                        string removedGuid = inspectorPropertyData.PropertyValue.ResourceGuids[index];
                        inspectorPropertyData.PropertyValue.ResourceGuids[index] = null;

                        if (UsingResources.ContainsKey(removedGuid) && !IsResourceInUse(removedGuid))
                        {
                            RemoveUsingResource(removedGuid);
                        }
                    }

                    SetPropertyResourcesValue(componentPropertyName, inspectorPropertyData.PropertyValue.ResourceGuids);
                    break;
                }
            }

            ClearNotUsingResources();
        }

        private void SetInspectorPropertyResourceValueSingle(string componentPropertyName, string resourceGuid)
        {
            if (string.IsNullOrEmpty(resourceGuid))
            {
                RemoveUsingResourceByPropertyName(componentPropertyName);
                return;
            }

            ResourceObject resourceObject = GameStateData.GetResource(resourceGuid);

            if (resourceObject == null)
            {
                void GetResourceCallback(ResourceDto resourceData)
                {
                    if (string.IsNullOrEmpty(resourceData.Guid))
                    {
                        Debug.LogError($"Resource with {resourceGuid} not found", _objectController.VarwinObjectDescriptor);
                        return;
                    }

                    InspectorProperty inspectorProperty = GetSerializedInspectorProperty(componentPropertyName);

                    if (inspectorProperty.OnDemand)
                    {
                        resourceData.OnDemand = true;
                    }

                    LoaderAdapter.LoadResources(resourceData);
                    ProjectData.ResourcesLoaded += SetValue;
                }

                API.GetResourceByGuid(resourceGuid, GetResourceCallback);
            }
            else
            {
                SetValue();
            }

            void SetValue()
            {
                ProjectData.ResourcesLoaded -= SetValue;

                if (!UsingResources.ContainsKey(resourceGuid))
                {
                    AddUsingResource(resourceGuid);
                }

                var existingProperty = false;

                foreach (InspectorPropertyData resourcePropertyData in InspectorPropertiesData)
                {
                    if (resourcePropertyData.ComponentPropertyName != componentPropertyName)
                    {
                        continue;
                    }

                    if (resourcePropertyData.PropertyValue.ResourceGuid == null)
                    {
                        continue;
                    }

                    if (resourcePropertyData.PropertyValue.ResourceGuid == resourceGuid)
                    {
                        //Don't do anything
                        //Property already set
                        return;
                    }

                    resourcePropertyData.PropertyValue.ResourceGuid = resourceGuid;
                    resourcePropertyData.PropertyValue.Value = null;
                    existingProperty = true;
                }

                if (!existingProperty)
                {
                    InspectorPropertiesData.Add(new InspectorPropertyData
                    {
                        ComponentPropertyName = componentPropertyName,
                        PropertyValue = new PropertyValue
                        {
                            ResourceGuid = resourceGuid,
                            Value = null
                        }
                    });
                }

                SetPropertyResourceValue(componentPropertyName, resourceGuid);
            }
        }

        private void SetPropertyValue(string componentPropertyName, object value)
        {
            InspectorProperty inspectorProperty = GetSerializedInspectorProperty(componentPropertyName);

            if (inspectorProperty == null)
            {
                return;
            }

            inspectorProperty.Data = new InspectorPropertyData
            {
                ComponentPropertyName = inspectorProperty.Name,
                PropertyValue = new PropertyValue
                {
                    ResourceGuid = null,
                    Value = value
                }
            };

            var data = InspectorPropertiesData.FirstOrDefault(x => x.ComponentPropertyName == componentPropertyName);
            if (data != null)
            {
                data.PropertyValue = inspectorProperty.Data.PropertyValue;
            }
            else
            {
                InspectorPropertiesData.Add(inspectorProperty.Data);
            }

            SetValueToPropertyWrapper(inspectorProperty.ComponentReference, inspectorProperty.PropertyInfo, value);

            PropertyValueChanged?.Invoke(componentPropertyName, value);
        }

        private void SetPropertyResourceValue(string componentPropertyName, string resourceGuid)
        {
            InspectorProperty inspectorProperty = GetSerializedInspectorProperty(componentPropertyName);

            if (inspectorProperty == null)
            {
                return;
            }

            object value = UsingResources[resourceGuid].Value;

            inspectorProperty.Data = new InspectorPropertyData
            {
                ComponentPropertyName = inspectorProperty.Name,
                PropertyValue = new PropertyValue
                {
                    ResourceGuid = resourceGuid,
                    Value = null
                }
            };

            InspectorPropertyData data = InspectorPropertiesData.FirstOrDefault(x => x.ComponentPropertyName == componentPropertyName);
            if (data != null)
            {
                data.PropertyValue = inspectorProperty.Data.PropertyValue;
            }
            else
            {
                InspectorPropertiesData.Add(inspectorProperty.Data);
            }

            SetValueToPropertyWrapper(inspectorProperty.ComponentReference, inspectorProperty.PropertyInfo, value);

            PropertyValueChanged?.Invoke(componentPropertyName, resourceGuid);
        }

        private void SetPropertyResourcesValue(string componentPropertyName, List<string> resourceGuids)
        {
            InspectorProperty inspectorProperty = GetSerializedInspectorProperty(componentPropertyName);

            if (inspectorProperty == null)
            {
                return;
            }

            var value = new List<object>();
            for (int i = 0; i < resourceGuids.Count; i++)
            {
                string resourceGuid = resourceGuids[i];
                if (!string.IsNullOrEmpty(resourceGuid) && UsingResources.ContainsKey(resourceGuid) && UsingResources[resourceGuid]?.Data != null)
                {
                    value.Add(!UsingResources[resourceGuid].Data.OnDemand
                        ? UsingResources[resourceGuid].Value
                        : UsingResources[resourceGuid].Data);
                }
                else
                {
                    value.Add(null);
                }
            }

            inspectorProperty.Data = new InspectorPropertyData
            {
                ComponentPropertyName = inspectorProperty.Name,
                PropertyValue = new PropertyValue
                {
                    ResourceGuids = resourceGuids,
                    Value = null
                }
            };

            InspectorPropertyData data = InspectorPropertiesData.FirstOrDefault(x => x.ComponentPropertyName == componentPropertyName);
            if (data != null)
            {
                data.PropertyValue = inspectorProperty.Data.PropertyValue;
            }
            else
            {
                InspectorPropertiesData.Add(inspectorProperty.Data);
            }

            SetValueToPropertyWrapper(inspectorProperty.ComponentReference, inspectorProperty.PropertyInfo, value);

            PropertyValueChanged?.Invoke(componentPropertyName, resourceGuids);
        }

        public List<ResourceDto> GetUsingResourcesData()
        {
            if (UsingResources == null)
            {
                return new();
            }

            ClearNotUsingResources();
            return UsingResources.Values.Select(resourcesValue => resourcesValue?.Data).ToList();
        }

        public List<string> GetOnDemandedResourceGuids()
        {
            var demanded = SerializableProperties
                .Where(x => x.Value != null)
                .Where(x => x.Value.OnDemand)
                .Where(x => x.Value.Data?.PropertyValue?.ResourceGuid != null)
                .Select(x => x.Value.Data.PropertyValue.ResourceGuid).ToList();

            var demandedArrays = SerializableProperties
                .Where(x => x.Value != null)
                .Where(x => x.Value.OnDemand)
                .Where(x => x.Value.Data?.PropertyValue?.ResourceGuids != null)
                .SelectMany(x => x.Value.Data.PropertyValue.ResourceGuids).ToList();

            var result = new List<string>();
            result.AddRange(demanded);
            result.AddRange(demandedArrays);

            return result;
        }

        public List<InspectorPropertyData> GetInspectorPropertiesData()
        {
            var serializables = SerializableProperties.Where(x => !InspectorProperties.ContainsKey(x.Key));

            foreach (var serializable in serializables)
            {
                var data = serializable.Value.Data ?? new InspectorPropertyData
                {
                    ComponentPropertyName = serializable.Key.Substring(serializable.Key.IndexOf("__") + 2),
                    PropertyValue = new PropertyValue
                    {
                        ResourceGuid = null,
                        Value = _propertyWrappers[serializable.Value.PropertyInfo].Getter(serializable.Value.ComponentReference.Component)
                    }
                };

                var value = data.PropertyValue;

                InspectorPropertyData item = InspectorPropertiesData.FirstOrDefault(x => x.ComponentPropertyName == serializable.Key);
                if (item == null)
                {
                    item = new InspectorPropertyData
                    {
                        ComponentPropertyName = serializable.Key,
                        PropertyValue = value
                    };
                    InspectorPropertiesData.Add(item);
                }
                else
                {
                    item.PropertyValue = value;
                }
            }

            foreach (var serializableProperty in SerializableProperties)
            {
                if (serializableProperty.Value?.Data == null)
                {
                    continue;
                }

                if (InspectorPropertiesData.FirstOrDefault(x => x.ComponentPropertyName == serializableProperty.Value.Name) != null)
                {
                    continue;
                }

                InspectorPropertiesData.Add(new InspectorPropertyData
                {
                    ComponentPropertyName = serializableProperty.Value.Name,
                    PropertyValue = serializableProperty.Value.Data.PropertyValue,
                });
            }

            foreach (InspectorPropertyData inspectorPropertyData in InspectorPropertiesData)
            {
                if (inspectorPropertyData.PropertyValue.Value is JObject)
                {
                    continue;
                }

                if (!(inspectorPropertyData.PropertyValue.Value is ValueType
                      || inspectorPropertyData.PropertyValue.Value is string
                      || inspectorPropertyData.PropertyValue?.Value != null && inspectorPropertyData.PropertyValue.Value.GetType().ImplementsInterface<IEnumerable>()))
                {
                    inspectorPropertyData.PropertyValue.Value = null;
                }
            }

            var tempInspectorPropertiesData = new List<InspectorPropertyData>();
            
            foreach (var inspectorPropertyData in InspectorPropertiesData)
            {
                var newData = new InspectorPropertyData
                {
                    ComponentPropertyName = inspectorPropertyData.ComponentPropertyName,
                    PropertyValue = new PropertyValue
                    {
                        ResourceGuid = inspectorPropertyData.PropertyValue.ResourceGuid,
                        ResourceGuids = inspectorPropertyData.PropertyValue.ResourceGuids != null ? new List<string>(inspectorPropertyData.PropertyValue.ResourceGuids) : null,
                        Value = inspectorPropertyData.PropertyValue.Value.Copy()
                    }
                };
                tempInspectorPropertiesData.Add(newData);
            }

            return tempInspectorPropertiesData;
        }

        private void AddUsingResource(string resourceGuid) => AddUsingResource(GameStateData.GetResource(resourceGuid));

        private void AddUsingResource(ResourceObject resourceObject)
        {
            var guid = resourceObject?.Data?.Guid;
            if(string.IsNullOrEmpty(guid))
                return;

            UsingResources.TryAdd(guid, resourceObject);
        }

        private void RemoveUsingResource(ResourceObject resourceObject) => RemoveUsingResource(resourceObject.Data.Guid);

        private void RemoveUsingResource(string resourceGuid)
        {
            if (UsingResources.ContainsKey(resourceGuid))
                UsingResources.Remove(resourceGuid);
        }

        private void RemoveUsingResourceByPropertyName(string componentPropertyName)
        {
            InspectorPropertyData propertyDataToRemove =
                InspectorPropertiesData.FirstOrDefault(propertyData => propertyData.ComponentPropertyName == componentPropertyName);

            if (string.IsNullOrEmpty(propertyDataToRemove?.PropertyValue.ResourceGuid))
            {
                return;
            }

            if (UsingResources.ContainsKey(propertyDataToRemove.PropertyValue.ResourceGuid))
            {
                RemoveUsingResource(propertyDataToRemove.PropertyValue.ResourceGuid);
            }

            InspectorPropertiesData.Remove(propertyDataToRemove);

            if (_propertyDefaults.ContainsKey(componentPropertyName))
            {
                SetPropertyValue(componentPropertyName, _propertyDefaults[componentPropertyName]);
            }
        }

        private void ClearNotUsingResources()
        {
            if (UsingResources == null)
            {
                return;
            }

            var resourcesToRemove = UsingResources.Values
                .Select(x => x?.Data.Guid)
                .Where(resourceGuid => !string.IsNullOrEmpty(resourceGuid) && !IsResourceInUse(resourceGuid))
                .ToList();

            foreach (string resource in resourcesToRemove)
            {
                RemoveUsingResource(resource);
            }
        }

        private bool IsResourceInUse(string resourceGuid)
        {
            return InspectorPropertiesData.Any(x =>
                x.PropertyValue.ResourceGuid == resourceGuid
                || x.PropertyValue.ResourceGuids != null && x.PropertyValue.ResourceGuids.Contains(resourceGuid));
        }

        private IEnumerator SetPropertiesWithDelay()
        {
            if (InspectorPropertiesData == null)
            {
                yield break;
            }

            foreach (InspectorPropertyData propertyData in InspectorPropertiesData)
            {
                if (propertyData.PropertyValue.ResourceGuids != null)
                {
                    SetPropertyResourcesValue(propertyData.ComponentPropertyName, propertyData.PropertyValue.ResourceGuids);
                    continue;
                }

                if (propertyData.PropertyValue.ResourceGuid != null)
                {
                    SetPropertyResourceValue(propertyData.ComponentPropertyName, propertyData.PropertyValue.ResourceGuid);
                    continue;
                }

                if (propertyData.PropertyValue.Value != null)
                {
                    SetPropertyValue(propertyData.ComponentPropertyName, propertyData.PropertyValue.Value);
                }
            }
        }

        private void SetValueToPropertyWrapper(ComponentReference componentReference, PropertyInfo propertyInfo, object value)
        {
            if (componentReference == null || !componentReference.Component || propertyInfo == null)
            {
                return;
            }

            if (!_propertyDefaults.ContainsKey($"{componentReference.Name}__{propertyInfo.Name}"))
            {
                object currentValue = _propertyWrappers[propertyInfo].Getter(componentReference.Component);
                _propertyDefaults.Add($"{componentReference.Name}__{propertyInfo.Name}", currentValue);
            }

            try
            {
                if (propertyInfo.PropertyType == typeof(GameObject))
                {
                    if (value is GameObject prefab && prefab)
                    {
                        var go = Object.Instantiate(prefab);
                        go.SetActive(true);
                        _propertyWrappers[propertyInfo].Setter(componentReference.Component, go);
                    }
                    else
                    {
                        _propertyWrappers[propertyInfo].Setter(componentReference.Component, null);
                    }
                }
                else if (propertyInfo.PropertyType == typeof(Texture) || propertyInfo.PropertyType == typeof(TextAsset))
                {
                    _propertyWrappers[propertyInfo].Setter(componentReference.Component, value);
                }
                else if (propertyInfo.PropertyType == typeof(Sprite))
                {
                    if (value is Texture2D texture && texture)
                    {
                        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), 0.5f * Vector2.one, 100f, 0u, SpriteMeshType.FullRect, Vector4.zero, false);
                        _propertyWrappers[propertyInfo].Setter(componentReference.Component, sprite);
                    }
                    else if (value is Sprite sprite && sprite)
                    {
                        _propertyWrappers[propertyInfo].Setter(componentReference.Component, sprite);
                    }
                    else
                    {
                        _propertyWrappers[propertyInfo].Setter(componentReference.Component, null);
                    }
                }
                else if (propertyInfo.PropertyType.IsSubclassOf(typeof(ResourceOnDemand)))
                {
                    object oldValue = _propertyWrappers[propertyInfo].Getter(componentReference.Component);

                    ResourceDto resourceDto = GetComponentPropertyResource(componentReference, propertyInfo);

                    if (oldValue != null && oldValue is ResourceOnDemand resource)
                    {
                        resource.DTO = resourceDto;
                    }
                    else if (resourceDto != null)
                    {
                        _propertyWrappers[propertyInfo].Setter(componentReference.Component, Activator.CreateInstance(propertyInfo.PropertyType, resourceDto));
                    }
                }
                else if (propertyInfo.PropertyType == typeof(AudioClip))
                {
                    if (value is AudioClip audioClip && audioClip)
                    {
                        _propertyWrappers[propertyInfo].Setter(componentReference.Component, audioClip);
                    }
                    else
                    {
                        _propertyWrappers[propertyInfo].Setter(componentReference.Component, null);
                    }
                }
                else if (propertyInfo.PropertyType == typeof(VarwinVideoClip))
                {
                    if (value is string videoClipUrl)
                    {
                        _propertyWrappers[propertyInfo].Setter(componentReference.Component, new VarwinVideoClip(videoClipUrl));
                    }
                    else
                    {
                        _propertyWrappers[propertyInfo].Setter(componentReference.Component, null);
                    }
                }
                else if (propertyInfo.PropertyType != typeof(string) && propertyInfo.PropertyType.ImplementsInterface<IEnumerable>())
                {
                    bool isArray = propertyInfo.PropertyType.IsArray;

                    if (isArray)
                    {
                        SetArrayPropertyValue(componentReference, propertyInfo, value);
                    }
                    else
                    {
                        SetListPropertyValue(componentReference, propertyInfo, value);
                    }
                }
                else
                {
                    Converter.CastValue(propertyInfo, value, componentReference.Component);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"{componentReference.Component.name}: Can not set value {value} to property {propertyInfo.Name}\n{e}");
            }
        }

        private void SetArrayPropertyValue(ComponentReference componentReference, PropertyInfo propertyInfo, object value)
        {
            Type containerItemType = propertyInfo.PropertyType.GetElementType();
            object property = _propertyWrappers[propertyInfo].Getter(componentReference.Component);

            Array CreateNewArrayProperty(int size)
            {
                var newArray = Array.CreateInstance(containerItemType, size);

                return newArray;
            }

            var propertyArray = (Array) property;

            switch (value)
            {
                case CreateArrayMember createArrayMember:
                {
                    var oldArray = (Array) property;
                    int currentArraySize = oldArray?.Length ?? 0;
                    Array newArray = CreateNewArrayProperty(currentArraySize + 1);

                    for (var i = 0; i < newArray.Length; i++)
                    {
                        if (createArrayMember.Index == i)
                        {
                            continue;
                        }

                        newArray.SetValue(oldArray.GetValue(i), i);
                    }

                    newArray.SetValue(createArrayMember.NewMember, createArrayMember.Index);
                    _propertyWrappers[propertyInfo].Setter(componentReference.Component, newArray);

                    break;
                }
                case RemoveArrayMember removeArrayMember:
                {
                    var oldArray = (Array) property;
                    int currentArraySize = oldArray?.Length ?? 0;
                    Array newArray = CreateNewArrayProperty(currentArraySize - removeArrayMember.Indices.Count);

                    for (var i = 0; i < newArray.Length; i++)
                    {
                        if (removeArrayMember.Indices.Contains(i))
                        {
                            continue;
                        }

                        newArray.SetValue(oldArray.GetValue(i), i);
                    }

                    _propertyWrappers[propertyInfo].Setter(componentReference.Component, newArray);

                    break;
                }
                case ChangeArrayMember changeArrayMember:
                {
                    var array = (Array) property;
                    
                    if (!Converter.CastValue(changeArrayMember.ValueType, changeArrayMember.Value, out var result))
                    {
                        throw new ArgumentException();
                    }

                    var isValueDifferent = !array.GetValue(changeArrayMember.Index).Equals(result);
                    array.SetValue(result, changeArrayMember.Index);

                    if (isValueDifferent)
                    {
                        _propertyWrappers[propertyInfo].Setter(componentReference.Component, array);
                    }

                    break;
                }
                default:
                {
                    Converter.CastValue(propertyInfo, value, componentReference.Component);
                    break;
                }
            }
        }

        private void SetListPropertyValue(ComponentReference componentReference, PropertyInfo propertyInfo, object value)
        {
            var genericTypes = propertyInfo.PropertyType.GetGenericArguments();
            var containerItemType = genericTypes.Single();
            var property = _propertyWrappers[propertyInfo].Getter(componentReference.Component);

            IList CreateNewListProperty()
            {
                var listType = typeof(List<>);
                var constructedListType = listType.MakeGenericType(containerItemType);

                return (IList) Activator.CreateInstance(constructedListType);
            }

            property ??= CreateNewListProperty();

            var propertyList = (IList) property;
            switch (value)
            {
                case CreateArrayMember createArrayMember:
                {
                    propertyList.Insert(createArrayMember.Index, createArrayMember.NewMember);

                    _propertyWrappers[propertyInfo].Setter(componentReference.Component, property);

                    break;
                }
                case RemoveArrayMember removeArrayMember:
                {
                    var indices = removeArrayMember.Indices.OrderBy(x => x).ToArray();
                    for (var indexToRemove = indices.Length - 1; indexToRemove >= 0; indexToRemove--)
                    {
                        propertyList.RemoveAt(indices[indexToRemove]);
                    }

                    _propertyWrappers[propertyInfo].Setter(componentReference.Component, property);
                    break;
                }
                case ChangeArrayMember changeArrayMember:
                {
                    if (!Converter.CastValue(changeArrayMember.ValueType, changeArrayMember.Value, out var result))
                    {
                        throw new ArgumentException();
                    }

                    var isValueDifferent = !propertyList[changeArrayMember.Index].Equals(result);

                    propertyList[changeArrayMember.Index] = result;

                    if (isValueDifferent || _propertyWrappers[propertyInfo] == null)
                    {
                        _propertyWrappers[propertyInfo].Setter(componentReference.Component, property);
                    }

                    break;
                }
                default:
                {
                    Converter.CastValue(propertyInfo, value, componentReference.Component);
                    break;
                }
            }
        }

        private List<T> GetResourceOnDemandContainer<T>(ComponentReference componentReference, PropertyInfo propertyInfo)
        {
            var onDemands = new List<T>();
            GetComponentPropertyResources(componentReference, propertyInfo, out List<ResourceDto> resources);
            foreach (var resourceDto in resources)
            {
                T resourceOnDemand = default;
                if (resourceDto != null)
                {
                    resourceOnDemand = (T) Activator.CreateInstance(typeof(T), resourceDto);
                }

                onDemands.Add(resourceOnDemand);
            }

            return onDemands;
        }

        private InspectorProperty GetSerializedInspectorProperty(string componentPropertyName)
        {
            return SerializableProperties.TryGetValue(componentPropertyName, out var property) ? property : null;
        }

        private ResourceDto GetComponentPropertyResource(ComponentReference componentReference, PropertyInfo propertyInfo)
        {
            PropertyValue propertyValue = SerializableProperties[$"{componentReference.Name}__{propertyInfo.Name}"].Data.PropertyValue;
            if (propertyValue == null)
            {
                return null;
            }

            string resourceGuid = propertyValue.ResourceGuid;
            return string.IsNullOrEmpty(resourceGuid) ? null : UsingResources[resourceGuid].Data;
        }

        private void GetComponentPropertyResources(ComponentReference componentReference, PropertyInfo propertyInfo, out List<ResourceDto> result)
        {
            result = new List<ResourceDto>();
            PropertyValue propertyValue = SerializableProperties[$"{componentReference.Name}__{propertyInfo.Name}"].Data.PropertyValue;
            if (propertyValue == null)
            {
                return;
            }

            foreach (string resourceGuid in propertyValue.ResourceGuids)
            {
                if (!string.IsNullOrEmpty(resourceGuid) && UsingResources.ContainsKey(resourceGuid))
                {
                    result.Add(UsingResources[resourceGuid].Data);
                }
                else
                {
                    result.Add(null);
                }
            }
        }

        private bool IsValidForSerializationValue(object value)
        {
            if (value == null)
            {
                return true;
            }
            
            return !(value is IList && value.GetType().IsGenericType && value.GetType().GetGenericArguments()[0].BaseType == typeof(Object));
        }
        
        public void RefreshFromComponents(bool force = false)
        {
            var varwinInspectors = _varwinObjectDescriptor.Components.Where(x => VarwinInspectorHelper.IsVarwinInspector(x.Component));

            foreach (ComponentReference varwinInspector in varwinInspectors)
            {
                var members = VarwinInspectorHelper.GetVarwinInspectorMembers(varwinInspector.Component);
                var properties = members.OfType<PropertyInfo>();
                var methods = members.OfType<MethodInfo>();

                foreach (PropertyInfo property in properties)
                {
                    try
                    {
                        string componentPropertyName = $"{varwinInspector.Name}__{property.Name}";
                        
                        var varwinSerializableAttribute = property.FastGetCustomAttribute<VarwinSerializableAttribute>(true);
                        var varwinInspectorAttribute = property.FastGetCustomAttribute<VarwinInspectorAttribute>(true);

                        if (varwinSerializableAttribute == null)
                        {
                            continue;
                        }

                        _propertyWrappers[property] = ReflectionUtils.BuildPropertyWrapper(property);

                        InspectorPropertyData data = null;

                        if (!force)
                        {
                            if (InspectorPropertiesData is {Count: > 0})
                            {
                                data = InspectorPropertiesData.FirstOrDefault(x => x.ComponentPropertyName == componentPropertyName);
                            }
                        }

                        if (data == null)
                        {
                            object varwinInspectorPropertyValue = _propertyWrappers[property].Getter(varwinInspector.Component);

                            if (varwinInspectorPropertyValue != null && varwinInspectorPropertyValue is not Object && IsValidForSerializationValue(varwinInspectorPropertyValue))
                            {
                                data = new InspectorPropertyData
                                {
                                    ComponentPropertyName = property.Name,
                                    PropertyValue = new PropertyValue
                                    {
                                        Value = varwinInspectorPropertyValue 
                                    }
                                };
                            }
                            else if (varwinInspectorPropertyValue == null && property.PropertyType == typeof(string))
                            {
                                data = new InspectorPropertyData
                                {
                                    ComponentPropertyName = property.Name,
                                    PropertyValue = new PropertyValue
                                    {
                                        Value = ""
                                    }
                                };
                            }
                            else 
                            {
                                data = new InspectorPropertyData
                                {
                                    ComponentPropertyName = property.Name,
                                    PropertyValue = new PropertyValue
                                    {
                                        Value = null
                                    }
                                };
                            }
                        }

                        var inspectorProperty = new InspectorProperty
                        {
                            ComponentReference = varwinInspector,
                            PropertyInfo = property,
                            LocalizedName = varwinInspectorAttribute?.LocalizedNames,
                            Data = data
                        };

                        SerializableProperties[componentPropertyName] = inspectorProperty;
                        if (varwinInspectorAttribute != null)
                        {
                            InspectorProperties[componentPropertyName] = inspectorProperty;
                            InspectorComponentsTypes.Add(inspectorProperty.ComponentReference.Type);
                            var currentPropertyData = InspectorPropertiesData.Find(x => x.ComponentPropertyName == componentPropertyName);

                            if (currentPropertyData != null)
                            {
                                var resourceGuids = currentPropertyData.PropertyValue?.ResourceGuids;
                                var resourceGuid = currentPropertyData.PropertyValue?.ResourceGuid;
                                currentPropertyData.PropertyValue = data.PropertyValue;
                                currentPropertyData.PropertyValue.ResourceGuid = resourceGuid;
                                currentPropertyData.PropertyValue.ResourceGuids = resourceGuids;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Exception was raised in property {property.Name} of class {varwinInspector.Component}: {e}");
                    }
                }

                foreach (MethodInfo method in methods)
                {
                    var varwinInspectorAttribute = method.FastGetCustomAttribute<VarwinInspectorAttribute>(true);
                    var inspectorMethod = new InspectorMethod
                    {
                        MethodInfo = method,
                        ComponentReference = varwinInspector,
                        LocalizedName = varwinInspectorAttribute?.LocalizedNames
                    };

                    if (varwinInspectorAttribute != null)
                    {
                        var methodName = $"{varwinInspector.Name}__{method.Name}";
                        if (!InspectorMethods.ContainsKey(methodName))
                        {
                            InspectorMethods.Add(methodName, inspectorMethod);
                        }
                        
                        InspectorComponentsTypes.Add(inspectorMethod.ComponentReference.Type);
                    }
                }
            }
        }

        public static implicit operator bool(InspectorController inspectorController) => inspectorController != null;

        public override string ToString()
        {
            return _objectController ? $"InspectorController ({_varwinObjectDescriptor.Name}" : base.ToString();
        }
    }
}