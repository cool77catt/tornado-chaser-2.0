using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TornadoChaser
{
    // Follows behind the car, looks down the road, and widens the field of view
    // as speed increases. Switches smoothly between a close view and a high, wide
    // view; the player's choice is remembered between runs.
    [RequireComponent(typeof(Camera))]
    public class ChaseCamera : MonoBehaviour
    {
        [Serializable]
        struct View
        {
            public Vector3 offset;
            public float lookAhead;
            public float lookHeight;
        }

        const string WideViewKey = "ChaseCamera.WideView";

        [SerializeField] CarController target;
        // Kept low and tilted slightly up so the tornado and the storm clouds above it
        // fill the top of the portrait screen.
        [SerializeField] View closeView = new() { offset = new Vector3(0f, 3.5f, -10f), lookAhead = 25f, lookHeight = 6f };
        // High and far back, looking down the road, so the whole tornado and more of the road ahead are visible.
        [SerializeField] View wideView = new() { offset = new Vector3(0f, 9f, -24f), lookAhead = 30f, lookHeight = 3f };
        [SerializeField, Tooltip("Seconds to blend between views.")]
        float switchDuration = 0.5f;
        [SerializeField, Tooltip("How tightly the camera tracks the car's side-to-side movement. Higher is tighter.")]
        float lateralFollow = 5f;
        [SerializeField] float minSpeedFov = 60f;
        [SerializeField] float maxSpeedFov = 72f;

        Camera cam;
        bool wide;
        float blend;  // 0 = close view, 1 = wide view

        public void ToggleView()
        {
            wide = !wide;
            PlayerPrefs.SetInt(WideViewKey, wide ? 1 : 0);
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
            wide = PlayerPrefs.GetInt(WideViewKey, 0) == 1;
            blend = wide ? 1f : 0f;
        }

        void Start() => transform.position = target.transform.position + Offset();

        void Update()
        {
            // Keyboard shortcut for testing in the Editor; players use the on-screen button.
            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) ToggleView();
        }

        void LateUpdate()
        {
            // Unscaled, so switching views still works on the frozen game-over screen.
            blend = Mathf.MoveTowards(blend, wide ? 1f : 0f, Time.unscaledDeltaTime / switchDuration);
            float eased = Mathf.SmoothStep(0f, 1f, blend);
            Vector3 offset = Offset();
            float lookAhead = Mathf.Lerp(closeView.lookAhead, wideView.lookAhead, eased);
            float lookHeight = Mathf.Lerp(closeView.lookHeight, wideView.lookHeight, eased);

            Vector3 car = target.transform.position;
            float x = Mathf.Lerp(transform.position.x, car.x + offset.x, 1f - Mathf.Exp(-lateralFollow * Time.deltaTime));
            transform.position = new Vector3(x, car.y + offset.y, car.z + offset.z);
            transform.LookAt(new Vector3(x, car.y + lookHeight, car.z + lookAhead));

            cam.fieldOfView = Mathf.Lerp(minSpeedFov, maxSpeedFov, target.NormalizedSpeed);
        }

        Vector3 Offset() => Vector3.Lerp(closeView.offset, wideView.offset, Mathf.SmoothStep(0f, 1f, blend));
    }
}
