namespace Varwin.Multiplayer.Types
{
    /// <summary>
    /// Данные для скачивания проекта из RMS.
    /// </summary>
    public class RmsConnectionData
    {
        public string RefreshToken;
        public string AccessToken;
        public string ApiUrl;
        public int ProjectId;
        public int SceneId;

        public bool Initialized;

        public RmsConnectionData(string refreshToken, string accessToken, string apiUrl, int projectId, int sceneId)
        {
            RefreshToken = refreshToken;
            AccessToken = accessToken;
            ApiUrl = apiUrl;
            ProjectId = projectId;
            SceneId = sceneId;
            Initialized = true;
        }
    }
}