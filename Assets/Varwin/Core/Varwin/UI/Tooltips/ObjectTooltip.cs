using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Varwin.PlatformAdapter;

namespace Varwin.UI
{
    public class ObjectTooltip : MonoBehaviour
    {       
        public enum ObjectTooltipSize
        {
            Small,
            Large
        } 
        public enum ObjectTooltipDirection
        {
            Up,
            Right,
            Down,
            Left
        }
        
        private GameObject _tooltippedObject;
        private string _tooltipText;

        private LineRenderer _line;
        private Text _text;
        private Transform _canvasTransform;

        private int resizeTextMinSizeDefault;
        private int resizeTextMaxSizeDefault;
       
        private float _offsetLength;

        private MeshRenderer[] _meshRenderers;

        private bool _initialized;
      
        [SerializeField]
        private float _maxTextHeight = 125.0f;

        [HideInInspector]
        public ObjectTooltipSize TooltipSize = ObjectTooltipSize.Large;

        [HideInInspector]
        public ObjectTooltipDirection TooltipDirection = ObjectTooltipDirection.Up;
        
        public GameObject TooltippedObject => _tooltippedObject;

        private void Start()
        {
            _text = GetComponentInChildren<Text>();
            resizeTextMinSizeDefault = _text.resizeTextMinSize;
            resizeTextMaxSizeDefault = _text.resizeTextMaxSize;
            
            _canvasTransform = GetComponentInChildren<Canvas>().transform;

            _line = GetComponentInChildren<LineRenderer>();
            _line.material.color = Color.black;
            _line.enabled = false;
        }

        public void SetTooltip(GameObject o, string text, ObjectTooltipSize tooltipSize, float offsetLength)
        {
            SetTooltip(o, text, tooltipSize, offsetLength, ObjectTooltipDirection.Up);
        }

        public void SetTooltip(GameObject o, string text, ObjectTooltipSize tooltipSize, float offsetLength, ObjectTooltipDirection tooltipDirection)
        {
            _tooltippedObject = o;
            _tooltipText = text;
            TooltipSize = tooltipSize;
            TooltipDirection = tooltipDirection;
            _offsetLength = offsetLength;

            UpdateObjectElements();

            _initialized = _tooltippedObject && text.Trim().Length != 0;
        }

        private void LateUpdate()
        {
            UpdateTooltip();
        }

        private void UpdateTooltip()
        {
            _line.enabled = _initialized;
            _canvasTransform.gameObject.SetActive(_initialized);

            if (!_initialized)
            {
                return;
            }
            
            if (!_tooltippedObject)
            {
                Destroy(gameObject);
                return;
            }
            
            GetObjectLineCenterAndPivot(out Vector3 center, out Vector3 pivot);

            Vector3 offset = Vector3.zero;
            float innerOffset = 0;
            var canvasRectTransform = (RectTransform) _canvasTransform;
            switch (TooltipDirection)
            {
                case ObjectTooltipDirection.Up:
                    offset = Vector3.up;
                    innerOffset = 0f;
                    break;
                case ObjectTooltipDirection.Right:
                    offset = Vector3.left;
                    innerOffset = 0.5f * canvasRectTransform.sizeDelta.x * canvasRectTransform.localScale.x;
                    break;
                case ObjectTooltipDirection.Down:
                    offset = Vector3.down;
                    var panelRectTransform = (RectTransform) _canvasTransform.GetChild(0);
                    if (panelRectTransform)
                    {
                        innerOffset = panelRectTransform.sizeDelta.y * canvasRectTransform.localScale.y;
                    }
                    break;
                case ObjectTooltipDirection.Left:
                    offset = Vector3.right;
                    innerOffset = 0.5f * canvasRectTransform.sizeDelta.x * canvasRectTransform.localScale.x;
                    break;
            }
            
            _canvasTransform.localPosition = (_offsetLength + innerOffset) * offset;
            
            _text.text = _tooltipText;
            _text.verticalOverflow = VerticalWrapMode.Overflow;

            float textHeight = _text.preferredHeight;
            
            if (textHeight > _maxTextHeight)
            {
                textHeight = _maxTextHeight;

                _text.resizeTextMinSize = resizeTextMinSizeDefault;
                _text.resizeTextMaxSize = resizeTextMaxSizeDefault;
                _text.resizeTextForBestFit = true;
            }
            else
            {
                _text.resizeTextForBestFit = false;
            }

            RectTransform textRectTransform = _text.rectTransform;
            
            textRectTransform.sizeDelta = new Vector2(textRectTransform.sizeDelta.x, textHeight);

            _text.verticalOverflow = Math.Abs(textHeight - _maxTextHeight) < Mathf.Epsilon ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;

            transform.position = pivot;

            if(InputAdapter.Instance.PlayerController.Nodes.Head != null)
            {
                transform.LookAt(InputAdapter.Instance.PlayerController.Nodes.Head.Transform.position);
            }
            
            _line.SetPosition(0, _canvasTransform.position);
            _line.SetPosition(1, center);
        }

        private void GetObjectLineCenterAndPivot(out Vector3 center, out Vector3 pivot)
        {
            pivot = Vector3.zero;

            var bounds = new Bounds();
            var renderersCount = 0;
            
            if (_meshRenderers.Length == 0)
            {
                pivot = _tooltippedObject.transform.position;
                center = pivot;

                return;
            }

            foreach (MeshRenderer meshRenderer in _meshRenderers)
            {
                if (!meshRenderer)
                {
                    continue;
                }
                
                bounds.Encapsulate(meshRenderer.bounds.max);
                pivot += meshRenderer.bounds.center;

                renderersCount++;
            }

            pivot /= renderersCount;
            center = pivot;

            switch (TooltipDirection)
            {
                case ObjectTooltipDirection.Up:
                    pivot.y = bounds.max.y;
                    break;
                case ObjectTooltipDirection.Right:
                    pivot.x = bounds.max.x;
                    break;
                case ObjectTooltipDirection.Down:
                    pivot.y = bounds.min.y;
                    break;
                case ObjectTooltipDirection.Left:
                    pivot.x = bounds.min.x;
                    break;
            }

            if (renderersCount != _meshRenderers.Length)
            {
                UpdateObjectElements();
            }
        }

        public void UpdateObjectElements()
        {
            if (!_tooltippedObject)
            {
                return;
            }
            
            _meshRenderers = _tooltippedObject.GetComponentsInChildren<MeshRenderer>();
        }
    }
}