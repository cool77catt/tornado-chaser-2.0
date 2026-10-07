using UnityEngine;
using UnityEngine.InputSystem;

namespace TornadoChaser
{
    // One-thumb floating stick: wherever the thumb lands becomes the center.
    // Drag left/right to steer, up to accelerate, down to brake.
    // Keyboard (WASD / arrows) is supported for testing in the Editor.
    public class CarInput : MonoBehaviour
    {
        [SerializeField, Tooltip("Drag distance for full input, as a fraction of screen width.")]
        float dragRange = 0.15f;
        [SerializeField, Range(0f, 0.5f)] float deadZone = 0.1f;

        public float Steer { get; private set; }     // -1 (left) .. 1 (right)
        public float Throttle { get; private set; }  // -1 (brake) .. 1 (gas)
        public bool IsActive { get; private set; }   // true while the player is giving any input

        Vector2 dragOrigin;
        bool dragging;

        void Update()
        {
            float steer = 0f, throttle = 0f;
            bool active = false;

            var pointer = Pointer.current;
            if (pointer != null && pointer.press.isPressed)
            {
                Vector2 position = pointer.position.ReadValue();
                if (!dragging)
                {
                    dragOrigin = position;
                    dragging = true;
                }
                Vector2 delta = (position - dragOrigin) / (Screen.width * dragRange);
                steer = ApplyDeadZone(delta.x);
                throttle = ApplyDeadZone(delta.y);
                active = true;
            }
            else
            {
                dragging = false;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float keySteer = Axis(keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed,
                                      keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed);
                float keyThrottle = Axis(keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed,
                                         keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed);
                if (keySteer != 0f || keyThrottle != 0f)
                {
                    steer = Mathf.Clamp(steer + keySteer, -1f, 1f);
                    throttle = Mathf.Clamp(throttle + keyThrottle, -1f, 1f);
                    active = true;
                }
            }

            Steer = steer;
            Throttle = throttle;
            IsActive = active;
        }

        float ApplyDeadZone(float value)
        {
            float magnitude = Mathf.Abs(value);
            if (magnitude < deadZone) return 0f;
            return Mathf.Sign(value) * Mathf.Clamp01((magnitude - deadZone) / (1f - deadZone));
        }

        static float Axis(bool negative, bool positive) => (positive ? 1f : 0f) - (negative ? 1f : 0f);
    }
}
