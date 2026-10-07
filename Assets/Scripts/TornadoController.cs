using UnityEngine;

namespace TornadoChaser
{
    // Moves the tornado down the road at a speed that changes unpredictably,
    // while it wanders from side to side.
    public class TornadoController : MonoBehaviour
    {
        [Header("Forward speed (m/s)")]
        [SerializeField] float minSpeed = 14f;
        [SerializeField] float maxSpeed = 30f;
        [SerializeField, Tooltip("How quickly the speed changes. Higher is more erratic.")]
        float speedVariation = 0.15f;

        [Header("Wander")]
        [SerializeField, Tooltip("Farthest the tornado strays from the road's center.")]
        float wanderWidth = 10f;
        [SerializeField] float wanderRate = 0.05f;

        float seed;

        public float Speed { get; private set; }

        void Awake() => seed = Random.value * 1000f;

        void Update()
        {
            float time = Time.time;
            Speed = Mathf.Lerp(minSpeed, maxSpeed, Noise01(seed, time * speedVariation));

            Vector3 position = transform.position;
            position.z += Speed * Time.deltaTime;
            position.x = (Noise01(seed + 50f, time * wanderRate) * 2f - 1f) * wanderWidth;
            transform.position = position;
        }

        // Perlin noise mostly stays within 0.2-0.8, so stretch it to fill 0-1.
        static float Noise01(float x, float y) => Mathf.InverseLerp(0.2f, 0.8f, Mathf.PerlinNoise(x, y));
    }
}
