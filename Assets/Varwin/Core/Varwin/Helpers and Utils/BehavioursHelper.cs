using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Varwin
{
    public static class BehavioursHelper
    {
        public static List<int> GetServerBehavioursDiff(List<GraphQL.Types.SceneBehavioursQueryResponse.ObjectBehaviour> serverBehavioursData)
        {
            if (serverBehavioursData == null)
            {
                Debug.LogError($"Can't find client and server behaviour difference. Server behaviours list is null");
                return null;
            }

            var objects = GameStateData.GetObjectsInScene();
            var ids = new List<int>();

            var objectsWithBehavioursDiff = new List<int>();

            //cases:
            // 1. Client add new behaviour
            // 2. Client remove behaviour
            // 3. No differences

            foreach (var objectController in objects)
            {
                if (ids.Contains(objectController.IdObject))
                {
                    continue;
                }
                
                ids.Add(objectController.IdObject);
                
                var serverBehavioursList = serverBehavioursData.FirstOrDefault(x => x.objectId == objectController.IdObject)?.behaviours;

                serverBehavioursList ??= new List<string>();

                var clientBehavioursList = objectController.GetObjectBehaviours();

                if (clientBehavioursList == null)
                {
                    continue;
                }

                var hasDifferentBehaviour = clientBehavioursList.Count != serverBehavioursList.Count || clientBehavioursList
                    .Any(behaviour => !serverBehavioursList.Contains(behaviour));

                if (hasDifferentBehaviour)
                {
                    objectsWithBehavioursDiff.Add(objectController.IdObject);                    
                }
            }

            return objectsWithBehavioursDiff;
        }
    }
}