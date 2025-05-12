using System;
using UnityEngine;

namespace Varwin
{
    public class SceneChangedHandler : MonoBehaviour
    {
        private void Awake()
        {
            ProjectData.CurrentSceneObjectsChanged += OnCurrentSceneChanged;
            ProjectData.CurrentSceneTemplateChanged += OnCurrentSceneChanged;
        }

        private void OnCurrentSceneChanged()
        {
            if (ProjectData.IsDesktopEditor)
            {
                return;
            }

            LoaderAdapter.LoadProject(ProjectData.ProjectId, ProjectData.SceneId, ProjectData.ProjectConfigurationId, true);
        }

        private void OnDestroy()
        {
            ProjectData.CurrentSceneObjectsChanged -= OnCurrentSceneChanged;
            ProjectData.CurrentSceneTemplateChanged -= OnCurrentSceneChanged;
        }
    }
}