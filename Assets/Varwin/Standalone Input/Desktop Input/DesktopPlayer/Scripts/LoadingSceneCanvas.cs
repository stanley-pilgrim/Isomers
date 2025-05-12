using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Varwin.Data;
using Varwin.Data.ServerData;

namespace Varwin.UI
{
    public class LoadingSceneCanvas : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Slider _progressSlider;
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private List<Image> _varwinLogos;
        [SerializeField] private List<Image> _customLogos;
        [SerializeField] private List<GameObject> _progressStatusLabels;

        private const float AverageDurationPerObject = 0.5f;
        private float _minDuration = 15f;
        private float _progress;
        private ResourceDto _logoResourceDto;

        private void OnEnable()
        {
            _canvas.enabled = ProjectData.PlatformMode == PlatformMode.Desktop;
            _progress = 0f;

            ProjectData.ProjectStructureChanged += OnProjectStructureChanged;
        }

        private void OnProjectStructureChanged()
        {
            PrepareStatusText();
            PrepareCustomLogo();
            TryHideStatusText();
            UpdateMinDuration();
        }
        
        private void TryHideStatusText()
        {
            var configuration = ProjectData.ProjectStructure.ProjectConfigurations.GetProjectConfigurationById(ProjectData.ProjectConfigurationId);
            if (configuration == null)
            {
                ResetStatusText();
                return;
            }

            _progressStatusLabels.ForEach(a => a.gameObject.SetActive(!configuration.HideExtendedLoadingStatus));
        }

        private void PrepareCustomLogo()
        {
            var configuration = ProjectData.ProjectStructure.ProjectConfigurations.GetProjectConfigurationById(ProjectData.ProjectConfigurationId);
            if (configuration?.LoadingLogoResourceId == null)
            {
                ResetToDefaultLogo();
                return;
            }

            _logoResourceDto = ProjectData.ProjectStructure.Resources.Find(a => a.Id == configuration.LoadingLogoResourceId);

            if (_logoResourceDto == null)
            {
                ResetToDefaultLogo();
                return;
            }

            LoaderAdapter.Loader.LoadResource(_logoResourceDto);
            LoaderAdapter.Loader.ResourceLoaded += OnResourceLoaded;
        }

        private void OnResourceLoaded(ResourceDto resource, object value)
        {
            if (_logoResourceDto != resource)
            {
                return;
            }

            var customLogo = _customLogos.FirstOrDefault();
            if (customLogo && customLogo.sprite)
            {
                Destroy(customLogo.sprite);
            }

            if (value is Texture2D texture2D)
            {
                var sprite = Sprite.Create(texture2D, new Rect(Vector2.zero, new Vector2(texture2D.width, texture2D.height)), Vector2.one / 2f);
                PrepareLogo();

                _customLogos.ForEach(a =>
                {
                    a.preserveAspect = true;
                    a.sprite = sprite;
                });
            }
        }

        private void PrepareStatusText()
        {
            _progressStatusLabels.ForEach(a => a.gameObject.SetActive(false));
        }

        private void ResetStatusText()
        {
            _progressStatusLabels.ForEach(a => a.gameObject.SetActive(true));
        }

        private void PrepareLogo()
        {
            _varwinLogos.ForEach(a => a.gameObject.SetActive(false));
            _customLogos.ForEach(a => a.gameObject.SetActive(true));
        }

        private void ResetToDefaultLogo()
        {
            _varwinLogos.ForEach(a => a.gameObject.SetActive(true));
            _customLogos.ForEach(a => a.gameObject.SetActive(false));
        }

        private void OnDisable()
        {
            ProjectData.ProjectStructureChanged -= OnProjectStructureChanged;
        }

        private void UpdateMinDuration()
        {
            _minDuration = AverageDurationPerObject * ProjectData.ProjectStructure.Objects.Count;
        }

        private void Update()
        {
            if (!_canvas.enabled)
            {
                return;
            }

            UpdateProgress();
            RedrawSlider();
        }

        private void UpdateProgress()
        {
            float duration = AverageDurationPerObject * _minDuration;

            var paused = false;

            if (_progress <= 0.25f)
            {
                duration = Random.Range(2f * _minDuration, 4f * _minDuration);
            }
            else if (_progress <= 0.5f)
            {
                duration = Random.Range(_minDuration, 2f * _minDuration);
            }
            else if (_progress <= 0.75f)
            {
                duration = Random.Range(2f * _minDuration, 4f * _minDuration);
            }
            else if (_progress <= 0.875f)
            {
                duration = Random.Range(4f * _minDuration, 8f * _minDuration);
            }
            else if (_progress <= 0.95f)
            {
                duration = 8f * _minDuration;
            }
            else
            {
                paused = true;
            }

            if (paused)
            {
                return;
            }

            _progress = duration > 0 ? Mathf.Clamp01(_progress + Time.unscaledDeltaTime / duration) : 1f;
        }

        private void RedrawSlider()
        {
            _progressSlider.normalizedValue = _progress;
            _progressText.text = $"{Mathf.RoundToInt(_progress * 100)} %";
        }
    }
}