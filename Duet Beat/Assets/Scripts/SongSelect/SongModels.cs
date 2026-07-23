using System;
using System.Collections.Generic;

namespace DuetBeat.SongSelect
{
    [Serializable]
    public class SongCategoryConfig
    {
        public string category;
        public int order;
        public bool enable;
        public string imagePreview;
    }

    [Serializable]
    public class SongEntry
    {
        public int index;
        public string id;
        public string name;
        public string artistName;
        public string fileAudio;
        public string filePreview;
        public string fileData;
        public string imagePreview;
        public string unlockType;
        public int price;
        public bool offlineAudio;
        public bool offlinePreviewAudio;
        public bool offlineData;
        public string category;
        public string loadType;
        public int difficulty;
        public bool disable;
        public int priority;
        public string genre;
        public List<string> categories;
        public string theme;
        public string tile;
        public string label;
        public int revision;
        public bool tutorial;
        public bool hasShortVersion;
        public bool shortModeOnly;
        public bool isFullTutorialSong;
    }

    [Serializable]
    public class SongDatabaseRoot
    {
        public List<SongCategoryConfig> categories;
        public List<SongEntry> tutorialSongs;
        public List<SongEntry> softLaunchSongs;
        public List<SongEntry> testSongs;
        public List<SongEntry> listSongs;
        public List<SongEntry> onlineSongs;
        public List<SongEntry> eventSongs;
    }
}
