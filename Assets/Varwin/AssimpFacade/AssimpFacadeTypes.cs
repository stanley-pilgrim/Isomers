using UnityEngine;

namespace Assimp
{
    public class AssimpImporter
    {
        public static AssimpModelContainer Load(string modelPath)
        {
            throw new System.NotImplementedException();
        }
    }

    public class AssimpModelContainer
    {
        public GameObject gameObject;

        public static void UnloadAll() { }
    }
}