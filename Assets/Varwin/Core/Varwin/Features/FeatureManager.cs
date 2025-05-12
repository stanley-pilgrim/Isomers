using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Varwin.Data
{
    /// <summary>
    /// Менеджер фич для работы продукта.
    /// </summary>
    public static class FeatureManager
    {
        /// <summary>
        /// Ключ для чтения/записи в хранилище.
        /// </summary>
        private const string PlayerPrefsKey = "AVAILABLE_FEATURES";

        /// <summary>
        /// 
        /// </summary>
        public static event Action<Feature, bool> FeatureActiveChanged;
        
        /// <summary>
        /// Список доступных фич.
        /// </summary>
        private static HashSet<Feature> _availableFeatures;
        
        /// <summary>
        /// Инициализация фич.
        /// </summary>
        static FeatureManager()
        {
            if (!PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                _availableFeatures = new HashSet<Feature>();
                return;
            }

            try
            {
                _availableFeatures = JsonConvert.DeserializeObject<HashSet<Feature>>(PlayerPrefs.GetString(PlayerPrefsKey));
            }
            catch (Exception e)
            {
                _availableFeatures = new HashSet<Feature>();
                Debug.LogWarning("Features not loaded! Use default");
            }
        }
        
        /// <summary>
        /// Активировать фичу из списка.
        /// </summary>
        /// <param name="feature">Фича.</param>
        /// <param name="active">Активировать ли.</param>
        public static void SetFeatureActive(Feature feature, bool active)
        {
            if (_availableFeatures.Contains(feature) && active)
            {
                return;
            }
            
            if (!_availableFeatures.Contains(feature) && !active)
            {
                return;
            }

            if (active)
            {
                _availableFeatures.Add(feature);
            }
            else
            {
                _availableFeatures.Remove(feature);
            }
            
            FeatureActiveChanged?.Invoke(feature, active);
            AutoSaveFeaturesState();
        }

        /// <summary>
        /// Сохранить текущее состояние фич.
        /// </summary>
        private static void AutoSaveFeaturesState()
        {
            PlayerPrefs.SetString(PlayerPrefsKey, JsonConvert.SerializeObject(_availableFeatures));
        }
        
        /// <summary>
        /// Активна ли фича.
        /// </summary>
        /// <param name="feature">Фича.</param>
        /// <returns>Истина, если фича доступна.</returns>
        public static bool IsAvailable(Feature feature)
        {
#if UNITY_STANDALONE_LINUX
            if (feature == Feature.AR)
            {
                return false;
            }
#endif

            return _availableFeatures.Contains(feature);
        }
    }
}