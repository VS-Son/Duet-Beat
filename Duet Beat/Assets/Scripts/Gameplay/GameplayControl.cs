using System;
using Unity.VisualScripting;
using UnityEngine;

namespace Gameplay
{
    public class GameplayControl : MonoBehaviour
    {
        public static GameplayControl Instance;
        [SerializeField] private GameObject characterLeft;
        [SerializeField] private GameObject characterRight;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
            
        }

        private void Start()
        {
            SetPosCharacter();
            
        }

        private void SetPosCharacter()
        {
            characterLeft.transform.localPosition = new Vector3(-1.25f, characterLeft.transform.localPosition.y, 0f);
            characterRight.transform.localPosition = new Vector3(1.25f, characterRight.transform.localPosition.y, 0f);        }

        private void OnEnable()
        {
            HoldAndDrag.OnMovementHandler += OnMovementHandler;
        }

        private void OnDisable()
        {
            HoldAndDrag.OnMovementHandler -= OnMovementHandler;
        }
        private void OnMovementHandler(float delta, TouchType touchType)
        {
            switch (touchType)
            {
                case TouchType.Left:
                    float charLeft = characterLeft.transform.localPosition.x + delta / 2 * Time.fixedDeltaTime;
                    charLeft =  Mathf.Clamp(charLeft, -2f, -0.5f);
                    characterLeft.transform.localPosition = new Vector3(charLeft, characterLeft.transform.localPosition.y, characterLeft.transform.localPosition.z);
                    break;
                case TouchType.Right:
                    float charRight = characterRight.transform.localPosition.x + delta / 2 * Time.fixedDeltaTime;
                     charRight =  Mathf.Clamp(charRight, 0.5f, 2f);
                    characterRight.transform.localPosition = new Vector3(charRight, characterRight.transform.localPosition.y, characterRight.transform.localPosition.z);

                    break;
            }
        }
        
        public void OnEndDragHandler()
        {
            HoldAndDrag.OnMovementHandler -= OnMovementHandler;
        }
    }
}
