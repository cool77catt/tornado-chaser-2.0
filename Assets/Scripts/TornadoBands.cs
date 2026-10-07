using UnityEngine;

namespace TornadoChaser
{
    // Stacked, puffy rings wrapped around the funnel core. Each ring is tilted,
    // bulges outward, and spins at its own speed, which breaks the funnel into
    // churning layers with a ragged silhouette. All rings share one mesh, so
    // they cost a single draw call.
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TornadoBands : MonoBehaviour
    {
        const int Rows = 5;

        [SerializeField] TornadoFunnel funnel;
        [SerializeField] int bandCount = 36;
        [SerializeField, Tooltip("Ring height as a multiple of the spacing between rings. Above 1, rings overlap.")]
        float overlap = 2f;
        [SerializeField, Tooltip("Ring radius relative to the funnel core.")]
        float radiusScale = 1.08f;
        [SerializeField, Tooltip("How far each ring bulges out at its middle, as a fraction of its radius.")]
        Vector2 bulgeRange = new(0.08f, 0.25f);
        [SerializeField, Tooltip("Degrees each ring is tipped away from horizontal.")]
        Vector2 tiltRange = new(2f, 9f);
        [SerializeField, Tooltip("Radians per second at the ground. Wider rings spin slower.")]
        float spinSpeed = 1.2f;
        [SerializeField] int segments = 64;
        [SerializeField] int seed = 7;

        struct Band
        {
            public float Bottom, Top;  // normalized funnel heights
            public float Bulge;
            public float TiltX, TiltZ;  // vertical rise per meter along x and z
            public float Phase, SpinScale;
        }

        Band[] bands;
        Vector2[] ringDirections;
        Mesh mesh;
        Vector3[] vertices;
        Vector3[] normals;

        void OnEnable() => Build();

        void OnDisable()
        {
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }

        void Update()
        {
            if (Application.isPlaying && mesh != null) UpdateShape(Time.time);
        }

        void Build()
        {
            if (funnel == null) return;

            var random = new System.Random(seed);
            float Range(Vector2 range) => Mathf.Lerp(range.x, range.y, (float)random.NextDouble());

            float spacing = 1f / bandCount;
            float halfHeight = spacing * overlap / 2f;
            bands = new Band[bandCount];
            var uOffsets = new float[bandCount];
            for (int b = 0; b < bandCount; b++)
            {
                float middle = (b + 0.5f) * spacing;
                float tilt = Mathf.Tan(Range(tiltRange) * Mathf.Deg2Rad);
                float tiltDirection = Range(new Vector2(0f, Mathf.PI * 2f));
                bands[b] = new Band
                {
                    Bottom = Mathf.Max(0f, middle - halfHeight),
                    Top = Mathf.Min(1f, middle + halfHeight),
                    Bulge = Range(bulgeRange),
                    TiltX = tilt * Mathf.Cos(tiltDirection),
                    TiltZ = tilt * Mathf.Sin(tiltDirection),
                    Phase = Range(new Vector2(0f, Mathf.PI * 2f)),
                    SpinScale = Range(new Vector2(0.7f, 1.3f)),
                };
                uOffsets[b] = (float)random.NextDouble();
            }

            ringDirections = new Vector2[segments + 1];
            for (int s = 0; s <= segments; s++)
            {
                float angle = (float)s / segments * Mathf.PI * 2f;
                ringDirections[s] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }

            int columns = segments + 1;
            int perBand = Rows * columns;
            vertices = new Vector3[bandCount * perBand];
            normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[bandCount * (Rows - 1) * segments * 6];

            int k = 0;
            for (int b = 0; b < bandCount; b++)
            {
                int start = b * perBand;
                for (int r = 0; r < Rows; r++)
                for (int s = 0; s <= segments; s++)
                    uvs[start + r * columns + s] = new Vector2((float)s / segments + uOffsets[b], (float)r / (Rows - 1));

                // Bands are listed bottom to top, so higher rings draw over lower ones.
                for (int r = 0; r < Rows - 1; r++)
                for (int s = 0; s < segments; s++)
                {
                    int i = start + r * columns + s;
                    triangles[k++] = i;
                    triangles[k++] = i + columns;
                    triangles[k++] = i + 1;
                    triangles[k++] = i + 1;
                    triangles[k++] = i + columns;
                    triangles[k++] = i + columns + 1;
                }
            }

            mesh = new Mesh { name = "TornadoBands", hideFlags = HideFlags.DontSave };
            UpdateShape(0f);
            mesh.uv = uvs;
            mesh.triangles = triangles;
            float width = 2f * (funnel.Radius(1f) * radiusScale * (1f + bulgeRange.y) + 20f);
            mesh.bounds = new Bounds(new Vector3(0f, funnel.Height / 2f, 0f), new Vector3(width, funnel.Height, width));
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        void UpdateShape(float time)
        {
            float groundRadius = funnel.Radius(0f);
            int i = 0;
            foreach (var band in bands)
            {
                float middleRadius = funnel.Radius((band.Bottom + band.Top) / 2f);
                // Like a real vortex, the narrow base spins faster than the wide top.
                float angle = band.Phase + time * spinSpeed * band.SpinScale * Mathf.Sqrt(groundRadius / middleRadius);
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                float bandMeters = (band.Top - band.Bottom) * funnel.Height;

                for (int r = 0; r < Rows; r++)
                {
                    float across = (float)r / (Rows - 1);
                    float t = Mathf.Lerp(band.Bottom, band.Top, across);
                    float coreRadius = funnel.Radius(t) * radiusScale;
                    float radius = coreRadius * (1f + band.Bulge * Mathf.Sin(across * Mathf.PI));
                    // The lower half of each puff faces down and the upper half faces up.
                    float slope = coreRadius * band.Bulge * Mathf.Cos(across * Mathf.PI) * Mathf.PI / bandMeters;
                    Vector3 center = funnel.Center(t, time);

                    foreach (var direction in ringDirections)
                    {
                        float x = direction.x * cos - direction.y * sin;
                        float z = direction.x * sin + direction.y * cos;
                        float offsetX = x * radius, offsetZ = z * radius;
                        vertices[i] = center + new Vector3(offsetX, band.TiltX * offsetX + band.TiltZ * offsetZ, offsetZ);
                        normals[i] = new Vector3(x, -slope, z).normalized;
                        i++;
                    }
                }
            }
            mesh.vertices = vertices;
            mesh.normals = normals;
        }
    }
}
