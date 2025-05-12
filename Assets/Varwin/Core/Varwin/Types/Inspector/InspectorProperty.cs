using System.Collections;
using System.Linq;
using System.Reflection;
using DesperateDevs.Utils;
using UnityEngine;
using Varwin.Public;

namespace Varwin
{
    /// <summary>
    /// Класс, описывающий поле в инспекторе.
    /// </summary>
    public class InspectorProperty
    {
        public string Name => $"{ComponentReference.Name}__{PropertyInfo.Name}";

        public ComponentReference ComponentReference;
        public PropertyInfo PropertyInfo;
        public I18n LocalizedName;
        public InspectorPropertyData Data;

        public bool IsResource => PropertyInfo.PropertyType == typeof(Sprite)
                                  || PropertyInfo.PropertyType == typeof(Texture)
                                  || PropertyInfo.PropertyType == typeof(Texture2D)
                                  || PropertyInfo.PropertyType.IsSubclassOf(typeof(ResourceOnDemand))
                                  || PropertyInfo.PropertyType == typeof(TextAsset)
                                  || PropertyInfo.PropertyType == typeof(GameObject)
                                  || PropertyInfo.PropertyType == typeof(AudioClip)
                                  || PropertyInfo.PropertyType == typeof(VarwinVideoClip);

        public bool OnDemand => PropertyInfo.PropertyType.IsSubclassOf(typeof(ResourceOnDemand))
                                || PropertyInfo.PropertyType.ImplementsInterface<IEnumerable>()
                                && PropertyInfo.PropertyType != typeof(string)
                                && (PropertyInfo.PropertyType.IsGenericType && PropertyInfo.PropertyType.GetGenericArguments().First().IsSubclassOf(typeof(ResourceOnDemand))
                                    || PropertyInfo.PropertyType.IsArray && PropertyInfo.PropertyType.GetElementType().IsSubclassOf(typeof(ResourceOnDemand)));
    }
}