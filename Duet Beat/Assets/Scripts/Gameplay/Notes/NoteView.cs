using UnityEngine;
using UnityEngine.UI;

namespace Gameplay
{
    /// <summary>
    /// Visual + state holder for a single falling note. Pooled and reused by <see cref="GameplayScreenController"/>.
    /// Normal/Big notes are square and center-pivoted; Hold notes are a tall bar, bottom-pivoted so its
    /// bottom edge (the "head") tracks <see cref="HitTime"/> exactly like a normal note while the bar's
    /// height — sized to its fall speed — represents the remaining hold <see cref="Duration"/> above it.
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class NoteView : MonoBehaviour
    {
        public RectTransform Rect { get; private set; }
        public Image Image { get; private set; }

        public int Lane;
        public float HitTime;
        public float Duration;
        public NoteType Type;
        public bool Hit;
        public bool Holding;

        private void Awake()
        {
            Rect = GetComponent<RectTransform>();
            Image = GetComponent<Image>();
        }

        public void ResetState(int lane, NoteData data, Color color, Vector2 size, Vector2 pivot, Sprite sprite = null)
        {
            Lane = lane;
            HitTime = data.time;
            Duration = data.duration;
            Type = data.type;
            Hit = false;
            Holding = false;
            Image.color = color;
            Image.sprite = sprite; // null => Image renders as a plain filled rectangle.
            Rect.pivot = pivot;
            Rect.sizeDelta = size;
            gameObject.SetActive(true);
        }
    }
}
