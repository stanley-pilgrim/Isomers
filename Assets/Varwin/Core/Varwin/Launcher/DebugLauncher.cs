using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Varwin.Log;
using Varwin;
using Varwin.Data;
using UnityEngine;
using Varwin.Public;
using Varwin.PlatformAdapter;

public class DebugLauncher : MonoBehaviour
{
    public Transform PlayerSpawnPoint;
    public GameMode GameMode;

    private void Update()
    {
        if (ProjectData.GameMode != GameMode)
        {
            ProjectData.GameMode = GameMode;
        }
    }

    private void Awake()
    {
        if (!PlayerSpawnPoint)
        {
            var worldDescriptor = FindObjectOfType<WorldDescriptor>();
            if (worldDescriptor && worldDescriptor.PlayerSpawnPoint)
            {
                PlayerSpawnPoint = worldDescriptor.PlayerSpawnPoint;
            }

            if (!PlayerSpawnPoint)
            {
                PlayerSpawnPoint = transform;
            }
        }
        
        Application.logMessageReceived += ErrorHelper.ErrorHandler;
        Settings.CreateDebugSettings("");
    }

    private IEnumerator Start()
    {
        while (InputAdapter.Instance == null)
        {
            yield return null;
        }
        
        GameObject playerRig = Instantiate(InputAdapter.Instance.PlayerController.RigInitializer.InitializeRig());
        
        InputAdapter.Instance.PlayerController.Init(playerRig);
       
        playerRig.transform.position = PlayerSpawnPoint ? PlayerSpawnPoint.position : Vector3.zero;
        
        ProjectData.GameMode = GameMode;
       
        InitObjectsOnScene();

        // TODO: Когда всплывет баг с переключением режимов в SDK и CameraManager.CurrentCamera — переделать
        var currentCamera = InputAdapter.Instance.PlayerController.Nodes.Rig.Transform.GetComponentInChildren<Camera>();
        if (ProjectData.PlatformMode == PlatformMode.Desktop)
        {
            CameraManager.DesktopPlayerCamera = currentCamera;
        }
        else
        {
            CameraManager.VrCamera = currentCamera;
        }
    }

    private void InitObjectsOnScene()
    {
        var sceneObjects = GetSceneObjects();

        foreach (var sceneObject in sceneObjects)
        {
            var spawn = new SpawnInitParams
            {
                Name = sceneObject.Value,
                IdScene = 1,
                IdInstance = 0,
                IdObject = 0,
                IdServer = 0
            };
            
            Helper.InitObject(0, spawn, sceneObject.Key, null);
        }
    }

    private Dictionary<GameObject, string> GetSceneObjects()
    {
        var sceneObjects = new Dictionary<GameObject, string>();
        
        var descriptors = FindObjectsOfType<VarwinObjectDescriptor>();
        foreach (VarwinObjectDescriptor descriptor in descriptors)
        {
            if (!sceneObjects.ContainsKey(descriptor.gameObject))
            {
                sceneObjects.Add(descriptor.gameObject, descriptor.Name);
            }
        }
        
        var monoBehaviours = FindObjectsOfType<MonoBehaviour>().Where(x => x is IVarwinInputAware);
        foreach (MonoBehaviour monoBehaviour in monoBehaviours)
        {
            if (monoBehaviour.GetComponentInParent<VarwinObjectDescriptor>())
            {
                continue;
            }
            
            if (!sceneObjects.ContainsKey(monoBehaviour.gameObject))
            {
                sceneObjects.Add(monoBehaviour.gameObject, monoBehaviour.name);
            }
        }

        return sceneObjects;
    }
}
