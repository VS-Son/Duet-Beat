using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gameplay
{
    public enum TouchType
    {
        Left,
        Right
    }

    public class HoldAndDrag : MonoBehaviour,IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public static event Action<float, TouchType> OnMovementHandler;
        public TouchType type;
        private static bool _isLeft;
        private static bool _isRight;
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (type == TouchType.Left && type != TouchType.Right)
            {
                _isLeft = true;
                Debug.Log("On Begin Drag Left");
            }
            if (type == TouchType.Right && type != TouchType.Left)
            {
                _isRight = true;
                Debug.Log("On Begin Drag Right");
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_isLeft && _isRight)
            {
                switch (type)
                {
                    case TouchType.Left when type != TouchType.Right:
                        OnMovementHandler?.Invoke(eventData.delta.x,TouchType.Left);
                        Debug.Log("On Begin Drag Left");
                        break;
                    case TouchType.Right when type != TouchType.Left:
                        OnMovementHandler?.Invoke(eventData.delta.x,TouchType.Right);
                        Debug.Log("On Begin Drag Right");
                        break;
                }
            }
            
        }
        public void OnEndDrag(PointerEventData eventData)
        {
            if (type == TouchType.Left && type != TouchType.Right)
            {
                Debug.Log("On Begin Drag Left");
            }
            if (type == TouchType.Right && type != TouchType.Left)
            {
                Debug.Log("On Begin Drag Right");
            }
        
        }
    }
}