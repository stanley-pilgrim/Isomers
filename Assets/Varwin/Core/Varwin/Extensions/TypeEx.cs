using System;
using System.Linq;

namespace Varwin
{
    public static class TypeEx
    {
        public static bool ImplementInterface(this Type type, Type interfaceType)
        {
            for (var type1 = type; type1 != (Type) null; type1 = type1.BaseType)
            {
                if (type1.GetInterfaces().Any(type2 => type2 == interfaceType || type2.ImplementInterface(interfaceType)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}