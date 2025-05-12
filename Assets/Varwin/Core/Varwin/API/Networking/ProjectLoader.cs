using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using Varwin.Data;
using Varwin.Data.AssetBundle;
using Varwin.Data.ServerData;

namespace Varwin.WWW
{
    public static class ProjectLoader
    {
        public static IEnumerator LoadProject(ProjectStructure projectStructure, Action<string, float> onProcessing = null, Action onFinished = null)
        {
            var projectPath = FileSystemUtils.GetFilesPath(ProjectData.IsMobileClient, "Projects") + $"/{projectStructure.Guid}/";
            if (!Directory.Exists(projectPath))
            {
                Directory.CreateDirectory(projectPath);
            }

            SerializeProjectInfo(projectStructure, projectPath);

            foreach (var sceneTemplate in projectStructure.SceneTemplates)
            {
                yield return DownloadSceneTemplate(sceneTemplate, projectPath, onProcessing);
            }

            foreach (var scene in projectStructure.Scenes)
            {
                yield return DownloadLogic(scene, projectPath, onProcessing);
            }

            foreach (var resource in projectStructure.Resources)
            {
                yield return DownloadResource(resource, projectPath, onProcessing);
            }

            foreach (var prefabObject in projectStructure.Objects)
            {
                yield return DownloadObject(prefabObject, projectPath, onProcessing);
            }

            onFinished?.Invoke();
        }

        private static IEnumerator DownloadResource(ResourceDto resource, string projectPath, Action<string, float> onProcessing)
        {
            yield return DownloadFile(resource.Path, projectPath, process => onProcessing?.Invoke(resource.GetLocalizedName(), process));

            if (resource.IsPicture())
            {
                yield return DownloadFile($"{resource.Path}.ktx", projectPath, process => onProcessing?.Invoke($"{resource.GetLocalizedName()} ktx", process));
            }
            else if (resource.IsModel())
            {
                yield return DownloadFile($"{resource.Assets}/content.zip", projectPath, process => onProcessing?.Invoke(resource.GetLocalizedName(), process));
                yield return DownloadFile($"{resource.Assets}/install.json", projectPath, process => onProcessing?.Invoke(resource.GetLocalizedName(), process));
            }
        }
        
        private static void SerializeProjectInfo(ProjectStructure projectStructure, string projectPath)
        {
            var serializedObject = JsonConvert.SerializeObject(projectStructure);
            File.WriteAllText(Path.Combine(projectPath, "index.json"), serializedObject);
        }

        private static IEnumerator DownloadSceneTemplate(SceneTemplatePrefab sceneTemplate, string projectPath, Action<string, float> onProcessing = null)
        {
            float stepLength = 1f / 6f;
            var process = 0f;
            var name = sceneTemplate.Name.GetCurrentLocale();
            yield return DownloadFile(sceneTemplate.BundleResource, projectPath, newProcess => SendProcess(name, newProcess, process, stepLength, onProcessing));
            process += stepLength;
            yield return DownloadFile(sceneTemplate.AndroidBundleResource, projectPath, newProcess => SendProcess(name, newProcess, process, stepLength, onProcessing));
            process += stepLength;
            yield return DownloadFile(sceneTemplate.ManifestResource, projectPath, newProcess => SendProcess(name, newProcess, process, stepLength, onProcessing));
            process += stepLength;
            yield return DownloadFile(sceneTemplate.AndroidManifestResource, projectPath, newProcess => SendProcess(name, newProcess, process, stepLength, onProcessing));
            process += stepLength;
            yield return DownloadFile(sceneTemplate.ConfigResource, projectPath, newProcess => SendProcess(name, newProcess, process, stepLength, onProcessing));
            process += stepLength;
            yield return DownloadSceneDlls(sceneTemplate, projectPath, newProcess => SendProcess(name, newProcess, process, stepLength, onProcessing));
        }

        private static void SendProcess(string name, float currentProcess, float process, float divider, Action<string, float> onProcessing = null)
        {
            process += currentProcess * divider;
            onProcessing?.Invoke(name, process);
        }

        private static IEnumerator DownloadLogic(Varwin.Data.ServerData.Scene scene, string projectPath, Action<string, float> onProcessing = null)
        {
            yield return DownloadFile(scene.LogicResource, projectPath, (process) => onProcessing?.Invoke(scene.Name, process));
        }

        private static IEnumerator DownloadObject(PrefabObject prefabObject, string projectPath, Action<string, float> onProcessing)
        {
            float stepLength = 1f / 7f;
            float process = 0f;
            string name = prefabObject.Name.GetCurrentLocale();
            yield return DownloadFile(prefabObject.ConfigResource, projectPath, newProcess => SendProcess(name, newProcess, process, stepLength, onProcessing));
            process += stepLength;
            yield return DownloadFile(prefabObject.AndroidBundleManifest, projectPath, newProcess => SendProcess(name, newProcess, process, stepLength, onProcessing));
            process += stepLength;
            yield return DownloadFile(prefabObject.AndroidBundleResource, projectPath, newProcess => SendProcess(name, newProcess, process, stepLength, onProcessing));
            process += stepLength;
            yield return DownloadFile(prefabObject.BundleResource, projectPath, newProcess => SendProcess(name, newProcess, process, stepLength, onProcessing));
            process += stepLength;
            yield return DownloadFile(prefabObject.BundleManifest, projectPath, newProcess => SendProcess(name, newProcess, process, stepLength, onProcessing));
            process += stepLength;
            yield return DownloadAssetBundleParts(prefabObject, projectPath, newProcess => SendProcess(name, newProcess, process, stepLength, onProcessing));
            process += stepLength;
            yield return DownloadObjectDlls(prefabObject, projectPath, newProcess => SendProcess(name, newProcess, process, stepLength, onProcessing));
        }

        private static IEnumerator DownloadAssetBundleParts(PrefabObject prefabObject, string projectPath, Action<float> onProcessing = null)
        {
            var partsList = FindAssetBundleParts(prefabObject, projectPath);
            foreach (var partPath in partsList)
            {
                yield return DownloadFile(partPath, projectPath, onProcessing);
                yield return DownloadFile($"{partPath}.manifest", projectPath);
            }
        }

        private static List<string> FindAssetBundleParts(PrefabObject prefabObject, string projectPath)
        {
            var partsList = new List<string>();

            if (!File.Exists(projectPath + prefabObject.ConfigResource))
            {
                return partsList;
            }

            var config = File.ReadAllText(projectPath + prefabObject.ConfigResource);
            var deserializedConfig = JsonConvert.DeserializeObject<AssetInfo>(config);

            if (deserializedConfig == null || deserializedConfig.AssetBundleParts == null || deserializedConfig.AssetBundleParts.Count == 0)
            {
                return partsList;
            }

            foreach (var assetBundlePart in deserializedConfig.AssetBundleParts)
            {
                partsList.Add(Path.Combine(prefabObject.Assets, assetBundlePart));

                if (ProjectData.IsMobileClient)
                {
                    partsList.Add(Path.Combine(prefabObject.Assets, $"android_{assetBundlePart}"));
                }
            }
            
            return partsList;
        }

        private static IEnumerator DownloadSceneDlls(SceneTemplatePrefab sceneTemplatePrefab, string projectPath, Action<float> onProcessing)
        {
            var path = projectPath + sceneTemplatePrefab.ConfigResource;
            var sceneInfo = JsonConvert.DeserializeObject<SceneData>(File.ReadAllText(path));
            if (sceneInfo != null)
            {
                var count = sceneInfo.DllNames.Count;
                for (var index = 0; index < count; index++)
                {
                    var assembly = sceneInfo.DllNames[index];
                    var currentProcess = (float) index / count;
                    yield return DownloadFile(Path.Combine(sceneTemplatePrefab.Assets, (string) assembly), projectPath, process => onProcessing?.Invoke(process / count + currentProcess));
                }
            }
        }

        private static IEnumerator DownloadObjectDlls(PrefabObject prefabObject, string projectPath, Action<float> onProcessing)
        {
            var path = projectPath + prefabObject.ConfigResource;
            var assetInfo = JsonConvert.DeserializeObject<AssetInfo>(File.ReadAllText(path));
            if (assetInfo != null)
            {
                var count = assetInfo.Assembly.Count;
                for (var index = 0; index < count; index++)
                {
                    var assembly = assetInfo.Assembly[index];
                    var currentProcess = (float) index / count;
                    yield return DownloadFile(Path.Combine(prefabObject.Assets, assembly), projectPath, process => onProcessing?.Invoke(process / count + currentProcess));
                }
            }
        }

        private static IEnumerator DownloadFile(string path, string projectPath, Action<float> onProcessing = null)
        {
            using var request = new UnityWebRequest(Settings.Instance.ApiHost + path);
            var finalPath = projectPath + path;
            AppendLocalPathIfNeeded(finalPath);
            var handler = new DownloadHandlerFile(finalPath) {removeFileOnAbort = true};
            request.downloadHandler = handler;
            request.SendWebRequest();
            while (!request.isDone)
            {
                onProcessing?.Invoke(request.downloadProgress);
                yield return null;
            }

            if (request.HasError())
            {
                Debug.LogWarning($"[Project downloader] Download error {finalPath}");
                onProcessing?.Invoke(1f);
                yield break;
            }

            Debug.Log($"[Project downloader] Loaded {finalPath}");
        }

        private static void AppendLocalPathIfNeeded(string path)
        {
            var destinationPath = path.Split('/');
            var sourcePath = destinationPath[0];
            for (var index = 1; index < destinationPath.Length - 1; index++)
            {
                if (string.IsNullOrEmpty(destinationPath[index]))
                {
                    continue;
                }

                var folder = destinationPath[index];
                sourcePath = $"{sourcePath}/{folder}";
                if (!Directory.Exists(sourcePath))
                {
                    Directory.CreateDirectory(sourcePath);
                }
            }
        }
    }
}