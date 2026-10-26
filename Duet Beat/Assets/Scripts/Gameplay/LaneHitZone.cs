using UnityEngine;
using UnityEngine.EventSystems;

namespace Gameplay
{
    /// <summary>
    /// Invisible full-height tap target for one falling-note lane. Forwards press/release to the
    /// controller so it can register hits and track hold notes, independent of the existing
    /// drag-based <see cref="HoldAndDrag"/> input.
    /// </summary>
    public class LaneHitZone : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public int lane;
        public GameplayScreenController controller;

        public void OnPointerDown(PointerEventData eventData)
        {
            controller?.TryHit(lane);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            controller?.OnLaneReleased(lane);
        }
    }
}
