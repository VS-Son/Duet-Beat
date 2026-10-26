using UnityEngine;
using UnityEngine.UI;

namespace Gameplay
{
    /// <summary>
    /// A single fragment of a shattered note, spawned in a burst when a note is hit. Pooled and driven
    /// entirely by <see cref="GameplayScreenController"/>'s update loop, mirroring <see cref="NoteView"/>'s
    /// pattern (dumb state holder, no self-ticking).
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class ShatterPiece : MonoBehaviour
    {
        public RectTransform Rect { get; private set; }
        public Image Image { get; private set; }

        public Vector2 Velocity;
        public float AngularVelocity;
        public float Life;
        public float MaxLife;

        private void Awake()
        {
            Rect = GetComponent<RectTransform>();
            Image = GetComponent<Image>();
        }

        public void Spawn(Vector2 position, Vector2 velocity, float angularVelocity, float rotationDegrees,
            Vector2 size, Color color, float life)
        {
            Rect.anchoredPosition = position;
            Rect.sizeDelta = size;
            Rect.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            Velocity = velocity;
            AngularVelocity = angularVelocity;
            Life = life;
            MaxLife = life;
            Image.color = color;
            gameObject.SetActive(true);
        }
    }
}
