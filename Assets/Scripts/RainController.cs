using UnityEngine;

namespace TornadoChaser
{
    // Rain falls in the camera's local space, so it always surrounds the view.
    // Matching the rain's backward speed to the car makes it streak toward the
    // camera as the car speeds up.
    [RequireComponent(typeof(ParticleSystem))]
    public class RainController : MonoBehaviour
    {
        [SerializeField] CarController car;
        [SerializeField] float fallSpeed = 22f;
        [SerializeField] float wind = -3f;

        ParticleSystem rain;

        void Awake() => rain = GetComponent<ParticleSystem>();

        void Update()
        {
            var velocity = rain.velocityOverLifetime;
            velocity.x = wind;
            velocity.y = -fallSpeed;
            velocity.z = -car.Speed;
        }
    }
}
