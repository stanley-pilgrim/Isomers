using System.Reflection;
using Varwin.Core;

namespace Varwin
{
    public static class CloneUtils
    {
        public static void CloneEvents(object source, object clone)
        {
            var fields = source.GetType().GetRuntimeFields();

            foreach (var field in fields)
            {
                var childEvent = source.GetType().GetEvent(field.Name); 
                if (childEvent == null || childEvent.FastGetCustomAttribute<LogicEventAttribute>(true) == null)
                {
                    continue;
                }
                        
                field.SetValue(clone, field.GetValue(source));
            }
        }
        
        public static void CloneProperties(object source, object clone)
        {
            var properties = source.GetType().GetRuntimeProperties();

            foreach (var propertyInfo in properties)
            {
                if (!propertyInfo.CanWrite || !propertyInfo.CanRead)
                {
                    continue;
                }
                
                if (propertyInfo.FastGetCustomAttribute<VariableAttribute>(true) == null)
                {
                    continue;
                }
                
                propertyInfo.SetValue(clone, propertyInfo.GetValue(source));
            }
        }
    }
}