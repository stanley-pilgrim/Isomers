using System;
using UnityEngine;
using Varwin.Data.ServerData;

namespace Varwin
{
    public static class Scene
    {
        public static event Action<string> LoadingStarted;

        public static void Load(string sid)
        {
            Data.ServerData.Scene projectScene = ProjectData.ProjectStructure.GetProjectScene(sid);

            if (projectScene == null)
            {
                Debug.LogError("Project scene not found!");
                return;
            }

            LoadingStarted?.Invoke(sid);
            LoaderAdapter.LoadProject(ProjectData.ProjectId, projectScene.Id, ProjectData.ProjectConfigurationId, true);
            ProjectData.SceneLoaded += RespawnPlayer;

            void RespawnPlayer()
            {
                ProjectData.SceneLoaded -= RespawnPlayer;
                PlayerManager.Respawn();
            }
        }
    }

    public static class Configuration
    {
        public static event Action<string> LoadingStarted;

        public static void Load(string sid)
        {
            ProjectConfiguration projectConfiguration = ProjectData.ProjectStructure.GetConfiguration(sid);

            if (projectConfiguration == null)
            {
                Debug.LogError("Project configuration not found!");
                return;
            }

            LoadingStarted?.Invoke(sid);
            LoaderAdapter.LoadProjectConfiguration(projectConfiguration.Id);
        }
    }
}