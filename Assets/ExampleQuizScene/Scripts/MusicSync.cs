using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Utility
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class MusicSync : UdonSharpBehaviour
    {
        [SerializeField] private AudioSource audioSource;

        private void OnEnable()
        {
            //Debug.Log("Super VR Ball - The server time is " + Networking.GetServerTimeInSeconds());
            var newTime = ((float) Networking.GetServerTimeInSeconds()) % audioSource.clip.length;
            audioSource.time = newTime + (newTime < 0 ? audioSource.clip.length : 0);
            audioSource.Play();
            
            //Debug.Log("Super VR Ball - Synced music playback to " + audioSource.time + ". Intended: " +newTime);
        }
    }
}
