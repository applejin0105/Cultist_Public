using UnityEngine;
using UnityEngine.InputSystem;

namespace Core.Extensions
{
    public class CustomCursor : MonoBehaviour
    {
        private static CustomCursor _instance;

        public Texture2D defaultCursor;
        public Texture2D clickedCursor;
        public Vector2 hotSpot = Vector2.zero;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            Cursor.SetCursor(defaultCursor, hotSpot, CursorMode.Auto);
        }

        private void Update()
        {
            if (Mouse.current == null) return;

            if (Mouse.current.leftButton.wasPressedThisFrame) Cursor.SetCursor(clickedCursor, hotSpot, CursorMode.Auto);

            if (Mouse.current.leftButton.wasReleasedThisFrame)
                Cursor.SetCursor(defaultCursor, hotSpot, CursorMode.Auto);
        }
    }
}