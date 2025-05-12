using System;
using System.Collections.Generic;
using Varwin.Core.Behaviours;
using Varwin.Core.Behaviours.ConstructorLib;

namespace Varwin.Multiplayer.NetworkBehaviours.v2
{
    public static class NetworkBehaviourHelper
    {
        /// <summary>
        /// Соответствие между поведениями и сетевыми поведениями 
        /// </summary>
        private static Dictionary<Type, Type> _correspondingTypes = new()
        {
            {typeof(VisualizationBehaviour), typeof(NetworkVisualizationBehaviour)},
            {typeof(LightBehaviour), typeof(NetworkLightBehaviour)}
        };

        public static bool TryGetCorrespondingBehaviourType(VarwinBehaviour behaviour, out Type type)
        {
            return _correspondingTypes.TryGetValue(behaviour.GetType(), out type);
        }
    }
}
