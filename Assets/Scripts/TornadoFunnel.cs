using UnityEngine;

namespace TornadoChaser
{
    // Generates a funnel mesh that is narrow at the ground and flares out at the
    // cloud base. The middle of the funnel sways while both ends stay put.
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TornadoFunnel : MonoBehaviour
    {
        [SerializeField] float height = 60f;
        [SerializeField] float baseRadius = 5f;
        [SerializeField] float topRadius = 110f;
        [SerializeField, Tooltip("1 is a straight cone. Higher keeps a narrow column for longer, then bells out sharply into the clouds.")]
        float flare = 3f;
        [SerializeField] float swayAmount = 8f;
        [SerializeField] float swaySpeed = 0.5f;
        [SerializeField] int rings = 48;
        [SerializeField] int segments = 64;

        Mesh mesh;
        Vector3[] vertices;
        Vector3[] normals;

        public float Height => height;

        void OnEnable() => Build();

        void OnDisable()
        {
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }

        void Update()
        {
            if (Application.isPlaying) UpdateShape(Time.time);
        }

        void Build()
        {
            int columns = segments + 1;
            int count = (rings + 1) * columns;
            vertices = new Vector3[count];
            normals = new Vector3[count];
            var uvs = new Vector2[count];
            var triangles = new int[rings * segments * 6];

            for (int r = 0; r <= rings; r++)
            for (int s = 0; s <= segments; s++)
                uvs[r * columns + s] = new Vector2((float)s / segments, (float)r / rings);

            int k = 0;
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < segments; s++)
            {
                int i = r * columns + s;
                triangles[k++] = i;
                triangles[k++] = i + columns;
                triangles[k++] = i + 1;
                triangles[k++] = i + 1;
                triangles[k++] = i + columns;
                triangles[k++] = i + columns + 1;
            }

            mesh = new Mesh { name = "TornadoFunnel", hideFlags = HideFlags.DontSave };
            UpdateShape(0f);
            mesh.uv = uvs;
            mesh.triangles = triangles;
            float width = 2f * (topRadius + swayAmount);
            mesh.bounds = new Bounds(new Vector3(0f, height / 2f, 0f), new Vector3(width, height, width));
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        void UpdateShape(float time)
        {
            int columns = segments + 1;
            for (int r = 0; r <= rings; r++)
            {
                float t = (float)r / rings;
                float radius = Radius(t);
                // Tilt normals down to match the funnel's outward slope.
                float slope = (Radius(Mathf.Min(1f, t + 0.01f)) - Radius(Mathf.Max(0f, t - 0.01f))) / (0.02f * height);
                Vector3 center = Center(t, time);

                for (int s = 0; s <= segments; s++)
                {
                    float angle = (float)s / segments * Mathf.PI * 2f;
                    var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    vertices[r * columns + s] = center + direction * radius;
                    normals[r * columns + s] = new Vector3(direction.x, -slope, direction.z).normalized;
                }
            }
            mesh.vertices = vertices;
            mesh.normals = normals;
        }

        // Funnel radius at normalized height t (0 = ground, 1 = cloud base).
        public float Radius(float t) => Mathf.Lerp(baseRadius, topRadius, Mathf.Pow(t, flare));

        // Center of the funnel at normalized height t, in local space.
        public Vector3 Center(float t, float time)
        {
            float bend = Mathf.Sin(t * Mathf.PI) * swayAmount;
            return new Vector3(
                bend * Mathf.Sin(time * swaySpeed + t * 3f),
                t * height,
                bend * Mathf.Cos(time * swaySpeed * 0.7f + t * 2f));
        }
    }
}
