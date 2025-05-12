using System;
using UnityEngine;

public class GCManager : MonoBehaviour
{
    private const float DebounceTime = 15f;
    
    private static GCManager Instance { get; set; }
    private static bool _instanceIsInitialized;
    private static bool _destroyingInProgress;

    private float _collectTimeout;
    private bool _collectRequired;

    private void Update()
    {
        _collectTimeout += Time.deltaTime;
        if (_collectRequired && _collectTimeout > DebounceTime)
        {
            _collectRequired = false;
            _collectTimeout = 0f;
            GC.Collect();
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        _destroyingInProgress = true;
    }

    private void CollectInstance()
    {
        _collectRequired = true;
    }

    public static void Collect()
    {
#if VARWIN_SDK
        GC.Collect();
        return;
#endif

        if (_destroyingInProgress)
        {
            GC.Collect();
            return;
        }

        if (!Instance && !_instanceIsInitialized)
        {
            Instance = new GameObject("GCManager").AddComponent<GCManager>();
            DontDestroyOnLoad(Instance.gameObject);
            _instanceIsInitialized = true;
        }

        if (Instance)
        {
            Instance.CollectInstance();
        }
        else
        {
            GC.Collect();
        }
    }
}
