using UnityEngine;
using Varwin.Data;

namespace Varwin
{
    public class Spawner : MonoBehaviour
    {
        public static Spawner Instance;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void SpawnAsset(SpawnInitParams param)
        {
            CreateSpawnEntity(param);
        }

        public void CreateSpawnEntity(SpawnInitParams paramObject)
        {
            if (GameStateData.GetWrapperCollection().Exist(paramObject.IdInstance))
            {
                Debug.Log($"Object with id {paramObject.IdInstance} already exist!");
                return;
            }

            Contexts contexts = Contexts.sharedInstance;
            var entity = contexts.game.CreateEntity();
            entity.AddSpawnAsset(paramObject);
        }
    }
}