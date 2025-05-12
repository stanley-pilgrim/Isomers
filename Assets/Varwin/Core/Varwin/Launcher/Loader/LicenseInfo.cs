using Varwin.Data;

namespace Varwin
{
    public static class LicenseInfo
    {
        private static License _value;
        private static bool _licenseWasRead;
        
        public static License Value {
            get => _value;
            set
            {
                if (_licenseWasRead)
                {
                    return;
                }

                _value = value;
                _licenseWasRead = true;
            }
        }

        public static bool IsDemo => Value?.Code == Edition.Starter;
    }
}
