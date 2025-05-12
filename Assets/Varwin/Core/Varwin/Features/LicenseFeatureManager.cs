using System.Collections.Generic;
using System.Linq;

namespace Varwin.Data
{
    /// <summary>
    /// Менеджер фич для лицензий.
    /// </summary>
    public static class LicenseFeatureManager
    {
        /// <summary>
        /// Список фич, доступных для лицензий.
        /// </summary>
        private static readonly Dictionary<Edition, Feature[]> _licenseFeatures = new()
        {
            {Edition.None, new[] {Feature.VR, Feature.MobileVR, Feature.NettleDesk, Feature.AR}},
            {Edition.Starter, new[] {Feature.VR, Feature.MobileVR, Feature.NettleDesk, Feature.AR}},
            {Edition.Full, new[] {Feature.VR, Feature.MobileVR, Feature.NettleDesk, Feature.AR}},
            {Edition.Professional, new[] {Feature.VR, Feature.MobileVR, Feature.AR}},
            {Edition.Business, new[] {Feature.VR, Feature.MobileVR, Feature.AR}},
            {Edition.Education, new[] {Feature.VR, Feature.MobileVR, Feature.AR}},
            {Edition.EducationKorea, new[] {Feature.VR, Feature.MobileVR, Feature.AR}},
            {Edition.Robotics, new[] {Feature.VR, Feature.MobileVR, Feature.AR}},
            {Edition.Server, new[] {Feature.VR, Feature.MobileVR, Feature.AR}},
            {Edition.NettleDesk, new[] {Feature.NettleDesk, Feature.AR}}
        };

        /// <summary>
        /// Активировать фичи для лицензии.
        /// </summary>
        /// <param name="edition">Лицензия.</param>
        public static void ActivateLicenseFeatures(Edition edition)
        {
            ResetLicenseFeatures();
            
            if (!_licenseFeatures.ContainsKey(edition))
            {
                return;
            }
            
            foreach (var feature in _licenseFeatures[edition])
            {
#if UNITY_STANDALONE_LINUX
                if (feature == Feature.NettleDesk)
                {
                    continue;
                }
#endif
                FeatureManager.SetFeatureActive(feature, true);
            }
        }

        /// <summary>
        /// Сброс фич для всех лицензий.
        /// </summary>
        private static void ResetLicenseFeatures()
        {
            foreach (var (edition, licenseFeatures) in _licenseFeatures)
            {
                foreach (var feature in licenseFeatures)
                {
                    FeatureManager.SetFeatureActive(feature, false);
                }
            }
        }

        /// <summary>
        /// Доступна ли эта фича в лицензии.
        /// </summary>
        /// <param name="edition">Лицензия.</param>
        /// <param name="feature">Фича.</param>
        /// <returns>Истина, если доступна.</returns>
        public static bool FeatureIsAvailable(Edition edition, Feature feature)
        {
            return _licenseFeatures[edition].Contains(feature);
        }
    }
}