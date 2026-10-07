using UnityEngine;

namespace TornadoChaser
{
    // Flashes lightning at random intervals: briefly brightens the sun, ambient
    // light, fog, and the storm shaders, and draws a jagged bolt near the tornado.
    public class LightningController : MonoBehaviour
    {
        static readonly int FlashId = Shader.PropertyToID("_LightningFlash");

        [SerializeField] Light sun;
        [SerializeField] Transform tornado;
        [SerializeField] LineRenderer bolt;
        [SerializeField, Tooltip("Seconds between flashes.")]
        Vector2 interval = new(3f, 9f);
        [SerializeField] float sunBoost = 1.5f;
        [SerializeField] Color fogBoost = new(0.18f, 0.19f, 0.22f);
        [SerializeField, Tooltip("Where bolts strike across the screen: 0 is the center and 1 is the screen edge.")]
        Vector2 boltScreenOffset = new(0.25f, 0.8f);
        [SerializeField, Range(0f, 1f), Tooltip("Chance that a flash shows a visible bolt.")]
        float boltChance = 0.6f;
        [SerializeField, Tooltip("Height the bolt drops from. Keep it near the storm cloud deck's base.")]
        float boltHeight = 85f;

        const float FlashDuration = 0.5f;

        float baseSunIntensity;
        Color baseAmbient;
        Color baseFog;
        float nextFlash;
        float flashTime = -1f;
        bool hasBolt;

        void Start()
        {
            baseSunIntensity = sun.intensity;
            baseAmbient = RenderSettings.ambientLight;
            baseFog = RenderSettings.fogColor;
            bolt.enabled = false;
            nextFlash = Time.time + 2f;
        }

        void OnDisable() => Shader.SetGlobalFloat(FlashId, 0f);

        void Update()
        {
            if (flashTime < 0f && Time.time >= nextFlash) StartFlash();
            if (flashTime < 0f) return;

            flashTime += Time.deltaTime;
            float flash = FlashCurve(flashTime);
            Apply(flash);
            // The bolt shows through both strikes, flickering off in the gap between them.
            bolt.enabled = hasBolt && flash > 0.5f;

            if (flashTime >= FlashDuration)
            {
                Apply(0f);
                bolt.enabled = false;
                flashTime = -1f;
                nextFlash = Time.time + Random.Range(interval.x, interval.y);
            }
        }

        void StartFlash()
        {
            flashTime = 0f;
            hasBolt = Random.value < boltChance;
            if (hasBolt) PlaceBolt();
        }

        void Apply(float flash)
        {
            sun.intensity = baseSunIntensity * (1f + sunBoost * flash);
            RenderSettings.ambientLight = baseAmbient * (1f + 0.6f * flash);
            RenderSettings.fogColor = baseFog + fogBoost * flash;
            Shader.SetGlobalFloat(FlashId, flash);
        }

        // A bright strike, a dim gap, a second strike, then a fade.
        static float FlashCurve(float t) =>
            t < 0.06f ? 1f
            : t < 0.12f ? 0.25f
            : t < 0.2f ? 0.8f
            : Mathf.Max(0f, 0.8f * (1f - (t - 0.2f) / (FlashDuration - 0.2f)));

        void PlaceBolt()
        {
            // Strike beside or in front of the funnel so it doesn't hide the bolt, and
            // inside the camera's view, which is narrow on a portrait screen.
            var view = Camera.main;
            float groundZ = tornado.position.z + Random.Range(-25f, 5f);
            float distance = groundZ - view.transform.position.z;
            float halfWidth = distance * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad) * view.aspect;
            float side = halfWidth * Random.Range(boltScreenOffset.x, boltScreenOffset.y) * (Random.value < 0.5f ? -1f : 1f);
            var ground = new Vector3(view.transform.position.x + side, 0f, groundZ);

            // A random zigzag, straightened so the bottom lands exactly on the strike point.
            int count = bolt.positionCount;
            var drift = new float[count];
            for (int i = 1; i < count; i++) drift[i] = drift[i - 1] + Random.Range(-6f, 6f);
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                float x = drift[i] - drift[count - 1] * t;
                bolt.SetPosition(i, ground + new Vector3(x, boltHeight * (1f - t), Random.Range(-3f, 3f)));
            }
        }
    }
}
