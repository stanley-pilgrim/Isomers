using System;
using UnityEngine;
namespace Varwin
{
    public class VrFadeController : MonoBehaviour
    {
        [SerializeField] private Renderer _renderer;

        private Material _rendererMaterial;
        
        private void Reset()
        {
            _renderer = GetComponent<Renderer>();
        }

        private void Start()
        {
            _rendererMaterial = _renderer.material;
        }

        private void LateUpdate()
        {
            if (!UIFadeInOutController.Instance)
            {
                return;
            }

            _rendererMaterial.color = new(0, 0, 0, UIFadeInOutController.Instance.Alpha);
            _renderer.enabled = UIFadeInOutController.Instance.Alpha > Mathf.Epsilon;
        }
    }
}
