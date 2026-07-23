using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DuetBeat.SongSelect
{
    public static class SongDatabaseLoader
    {
        private const string ResourcePath = "SongData/SongData_Config_BR";

        private static SongDatabaseRoot _cachedRoot;

        public static SongDatabaseRoot LoadRoot()
        {
            if (_cachedRoot != null)
                return _cachedRoot;

            TextAsset json = Resources.Load<TextAsset>(ResourcePath);
            if (json == null)
            {
                Debug.LogError($"SongDatabaseLoader: could not find TextAsset at Resources/{ResourcePath}");
                return null;
            }

            _cachedRoot = JsonUtility.FromJson<SongDatabaseRoot>(json.text);
            return _cachedRoot;
        }

        public static List<SongEntry> GetSongsByCategory(string category)
        {
            SongDatabaseRoot root = LoadRoot();
            if (root?.softLaunchSongs == null)
                return new List<SongEntry>();

            return root.softLaunchSongs
                .Where(s => !s.disable && s.category == category)
                .OrderBy(s => s.priority)
                .ThenBy(s => s.index)
                .ToList();
        }

        public static List<SongEntry> GetPopularSongs()
        {
            return GetSongsByCategory("Popular");
        }
    }
}
