using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Varwin
{
    public static class ArgumentStorage
    {
        private static readonly Dictionary<string, string> Args = new Dictionary<string, string>();

        public static bool Contains(string key)
        {
            return Args.ContainsKey(key);
        }

        public static string GetValue(string key)
        {
            if (!Args.ContainsKey(key))
            {
                Debug.LogWarning($"Storage does not contain argument: {key}");
                return null;
            }

            return Args[key];
        }

        public static void SetValue(string key, string value)
        {
            if (!Args.ContainsKey(key))
            {
                Args.Add(key, value);
                Debug.Log($"Storage argument added: {key}={value}");
                return;
            }

            Debug.Log($"Storage argument changed: {key}={value}");
            Args[key] = value;
        }

        public static void ClearStorage()
        {
            Debug.Log("Argument storage is cleared");
            Args.Clear();
        }

        public static void AddJsonArgsArray(string args)
        {
            var jObj = JObject.Parse(args);

            foreach (JProperty property in jObj.Properties())
            {
                SetValue(property.Name, property.Value.ToString());
            }
        }
    }
}