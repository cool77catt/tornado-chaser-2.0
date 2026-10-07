using UnityEngine;

namespace TornadoChaser
{
    // Follows behind the car, looks down the road, and widens the field of view
    // as speed increases.
    [RequireComponent(typeof(Camera))]
    public class ChaseCamera : MonoBehaviour
    {
        [SerializeField] CarController target;
        [SerializeField] Vector3 offset = new(0f, 4.5f, -8f);
        [SerializeField] float lookAhead = 14f;
        [SerializeField] float lookHeight = 1f;
        [SerializeField, Tooltip("How tightly the camera tracks the car's side-to-side movement. Higher is tighter.")]
        float lateralFollow = 5f;
        [SerializeField] float minSpeedFov = 60f;
        [SerializeField] float maxSpeedFov = 72f;

        Camera cam;

        void Awake() => cam = GetComponent<Camera>();

        void Start() => transform.position = target.transform.position + offset;

        void LateUpdate()
        {
            Vector3 car = target.transform.position;
            float x = Mathf.Lerp(transform.position.x, car.x + offset.x, 1f - Mathf.Exp(-lateralFollow * Time.deltaTime));
            transform.position = new Vector3(x, car.y + offset.y, car.z + offset.z);
            transform.LookAt(new Vector3(x, car.y + lookHeight, car.z + lookAhead));

            cam.fieldOfView = Mathf.Lerp(minSpeedFov, maxSpeedFov, target.NormalizedSpeed);
        }
    }
}
