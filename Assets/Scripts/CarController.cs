using UnityEngine;

namespace TornadoChaser
{
    // Drives the car forward along +Z on its own. The player adjusts speed and
    // steers within the road's width.
    [RequireComponent(typeof(CarInput))]
    public class CarController : MonoBehaviour
    {
        [Header("Speed (m/s)")]
        [SerializeField] float minSpeed = 8f;
        [SerializeField] float maxSpeed = 40f;
        [SerializeField] float startSpeed = 18f;
        [SerializeField] float acceleration = 10f;
        [SerializeField] float braking = 20f;
        [SerializeField, Tooltip("Speed lost per second while the player isn't touching the screen.")]
        float coastDeceleration = 3f;

        [Header("Steering")]
        [SerializeField] float maxLateralSpeed = 12f;
        [SerializeField, Tooltip("How quickly steering reaches its target. Higher is snappier.")]
        float lateralResponse = 8f;
        [SerializeField, Tooltip("How far the car can move from the road's center.")]
        float maxLateralOffset = 4f;
        [SerializeField] float maxVisualYaw = 20f;

        CarInput input;
        float lateralVelocity;

        public float Speed { get; private set; }
        public float NormalizedSpeed => Mathf.InverseLerp(minSpeed, maxSpeed, Speed);

        void Awake()
        {
            input = GetComponent<CarInput>();
            Speed = startSpeed;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            float throttle = input.Throttle;
            if (throttle > 0f) Speed += acceleration * throttle * dt;
            else if (throttle < 0f) Speed += braking * throttle * dt;
            else if (!input.IsActive) Speed -= coastDeceleration * dt;
            Speed = Mathf.Clamp(Speed, minSpeed, maxSpeed);

            float targetLateral = input.Steer * maxLateralSpeed;
            lateralVelocity = Mathf.Lerp(lateralVelocity, targetLateral, 1f - Mathf.Exp(-lateralResponse * dt));

            Vector3 position = transform.position;
            position.z += Speed * dt;
            position.x += lateralVelocity * dt;
            if (Mathf.Abs(position.x) >= maxLateralOffset)
            {
                position.x = Mathf.Sign(position.x) * maxLateralOffset;
                lateralVelocity = 0f;
            }
            transform.position = position;

            float yaw = Mathf.Atan2(lateralVelocity, Speed) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, Mathf.Clamp(yaw, -maxVisualYaw, maxVisualYaw), 0f);
        }
    }
}
