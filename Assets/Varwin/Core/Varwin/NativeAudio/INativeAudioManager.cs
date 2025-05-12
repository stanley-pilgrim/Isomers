using UnityEngine;

namespace Varwin
{
    public interface INativeAudioManager
    {
        bool Initialized { get; }
        
        byte TrackCount { get; }

        bool IsLoaded(int index);
        void Load(int index, AudioClip audioClip);
        void Unload(int index);
        
        void Play(int index);
        void Resume(int index);
        void Pause(int index);
        void Stop(int index);
        
        void SetPan(int index, float pan);
        void SetVolume(int index, float volume);
        
        float GetPlaybackTime(int index);
        void SetPlaybackTime(int index, float offsetSeconds);
    }
}