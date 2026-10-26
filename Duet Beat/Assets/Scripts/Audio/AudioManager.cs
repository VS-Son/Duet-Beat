using System;
using System.Collections.Generic;
using UnityEngine;

namespace Audio
{
    public class AudioManager : MonoBehaviour
    {
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource songPreview;
        [SerializeField] private AudioSource sfxSource;

        [Range(0,1f)][SerializeField] private float bgmVolume;
        [Range(0, 1f)] [SerializeField] private float songPreviewVolume;
        [Range(0, 1f)] [SerializeField] private float sfxVolume;
        

        private Dictionary<string, AudioClip> _audioClips = new();

        private void Awake()
        {
            LoadAllSfx();
        }

        private void Start()
        {
            
        }

        private void LoadAllSfx()
        {
            AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio");
            foreach (var clip in clips)
            {
                var nameClip = clip.name.ToLower();
                _audioClips[nameClip] = clip;
            }
        }

        public void PlayPreveiw(string filePreview)
        {
            if (string.IsNullOrEmpty(filePreview)) return;
            if (!filePreview.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return;
            
        }
    }
}
