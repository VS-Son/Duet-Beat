using System;
using System.Collections;
using System.Collections.Generic;
using Gameplay;
using SongSelect;
using UnityEngine;
using UnityEngine.Networking;

namespace DuetBeat.SongSelect
{
    public class SongListController : MonoBehaviour
    {
        [SerializeField] private Transform contentParent;
        [SerializeField] private SongListItemView itemPrefab;
        [SerializeField] private string category = "Popular";
        [SerializeField] private AudioSource previewAudioSource;

        private readonly List<SongListItemView> _spawnedItems = new List<SongListItemView>();
        private SongListItemView _selectedItem;
        private Coroutine _previewCoroutine;

        private void Start()
        {
            Populate();
        }

        public void Populate()
        {
            Clear();

            List<SongEntry> songs = SongDatabaseLoader.GetSongsByCategory(category);
            foreach (SongEntry entry in songs)
            {
                SongListItemView item = Instantiate(itemPrefab, contentParent);
                item.gameObject.SetActive(true);
                item.Bind(entry, OnItemSelected, OnItemPlay);
                _spawnedItems.Add(item);
            }

            if (_spawnedItems.Count > 0)
                OnItemSelected(_spawnedItems[0]);
        }

        private void Clear()
        {
            StopPreview();

            foreach (SongListItemView item in _spawnedItems)
            {
                if (item != null)
                    Destroy(item.gameObject);
            }
            _spawnedItems.Clear();
            _selectedItem = null;
        }

        private void OnItemSelected(SongListItemView item)
        {
            PlayPreview(item.Data);

            if (_selectedItem == item)
                return;

            if (_selectedItem != null)
                _selectedItem.SetSelected(false);

            _selectedItem = item;
            _selectedItem.SetSelected(true);
        }

        private void OnItemPlay(SongListItemView item)
        {
            GameplayScreenController.Instance.StartSong(item.Data, transform.root.gameObject);
        }

        private void PlayPreview(SongEntry entry)
        {
            StopPreview();

            if (entry == null || string.IsNullOrEmpty(entry.filePreview))
                return;

            if (!entry.filePreview.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[SongSelect] Preview for '{entry.name}' is a local file ('{entry.filePreview}') not bundled in this test build — skipping playback.");
                return;
            }

            _previewCoroutine = StartCoroutine(LoadAndPlayPreview(entry.filePreview));
        }

        private void StopPreview()
        {
            if (_previewCoroutine != null)
            {
                StopCoroutine(_previewCoroutine);
                _previewCoroutine = null;
            }

            if (previewAudioSource != null)
            {
                previewAudioSource.Stop();
                previewAudioSource.clip = null;
            }
        }

        private IEnumerator LoadAndPlayPreview(string url)
        {
            AudioType audioType = url.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ? AudioType.MPEG : AudioType.UNKNOWN;

            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[SongSelect] Failed to load preview audio '{url}': {request.error}");
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                previewAudioSource.clip = clip;
                previewAudioSource.Play();
            }
        }
    }
}
