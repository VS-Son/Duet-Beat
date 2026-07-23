using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DuetBeat.SongSelect
{
    public class SongListItemView : MonoBehaviour
    {
        [Header("Row")]
        [SerializeField] private Button rowButton;
        [SerializeField] private Image rowBackground;
        [SerializeField] private Outline rowSelectionOutline;

        [Header("Cover")]
        [SerializeField] private Image coverImage;
        [SerializeField] private TMP_Text genreLabelText;

        [Header("Info")]
        [SerializeField] private TMP_Text songNameText;
        [SerializeField] private TMP_Text artistNameText;
        [SerializeField] private TMP_Text starsText;
        [SerializeField] private GameObject difficultyTagRoot;
        [SerializeField] private TMP_Text difficultyTagText;
        [SerializeField] private Image difficultyTagBackground;

        [Header("Play Button")]
        [SerializeField] private Button playButton;
        [SerializeField] private Image playButtonImage;
        [SerializeField] private TMP_Text playButtonText;

        private static readonly Color32 FreePlayColor = new Color32(0x4C, 0xD9, 0x38, 0xFF);
        private static readonly Color32 AdsPlayColor = new Color32(0xFF, 0xA6, 0x2B, 0xFF);
        private static readonly Color32 CurrencyPlayColor = new Color32(0x8E, 0x7B, 0xE0, 0xFF);
        private static readonly Color32 UnselectedBg = new Color32(0xEA, 0xE0, 0xF8, 0xFF);
        private static readonly Color32 SelectedBg = new Color32(0x8B, 0x5C, 0xF6, 0xFF);
        private static readonly Color32 HardTagColor = new Color32(0x6A, 0x1B, 0x9A, 0xFF);
        private static readonly Color32 ExtremeTagColor = new Color32(0xC6, 0x28, 0x28, 0xFF);

        public SongEntry Data { get; private set; }

        private Action<SongListItemView> _onSelected;
        private Action<SongListItemView> _onPlay;

        public void Bind(SongEntry entry, Action<SongListItemView> onSelected, Action<SongListItemView> onPlay)
        {
            Data = entry;
            _onSelected = onSelected;
            _onPlay = onPlay;

            songNameText.text = entry.name;
            artistNameText.text = entry.artistName;
            genreLabelText.text = string.IsNullOrEmpty(entry.genre) ? entry.category : entry.genre;

            int starCount = Mathf.Clamp(entry.difficulty + 1, 1, 3);
            starsText.text = new string('*', starCount) + new string('-', 3 - starCount);

            bool showTag = entry.difficulty >= 2;
            difficultyTagRoot.SetActive(showTag);
            if (showTag)
            {
                bool extreme = entry.difficulty >= 3;
                difficultyTagText.text = extreme ? "Extreme" : "Hard";
                difficultyTagBackground.color = extreme ? ExtremeTagColor : HardTagColor;
            }

            switch (entry.unlockType)
            {
                case "Ads":
                    playButtonImage.color = AdsPlayColor;
                    playButtonText.text = "PLAY";
                    break;
                case "Currency":
                    playButtonImage.color = CurrencyPlayColor;
                    playButtonText.text = entry.price > 0 ? $"{entry.price}" : "PLAY";
                    break;
                default:
                    playButtonImage.color = FreePlayColor;
                    playButtonText.text = "PLAY";
                    break;
            }

            SetSelected(false);

            rowButton.onClick.RemoveAllListeners();
            rowButton.onClick.AddListener(() => _onSelected?.Invoke(this));

            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(() =>
            {
                _onSelected?.Invoke(this);
                _onPlay?.Invoke(this);
            });
        }

        public void SetSelected(bool selected)
        {
            rowBackground.color = selected ? SelectedBg : UnselectedBg;
            if (rowSelectionOutline != null)
                rowSelectionOutline.enabled = selected;

            if (songNameText != null)
                songNameText.color = selected ? Color.white : new Color32(0x5A, 0x3E, 0x8C, 0xFF);
        }
    }
}
