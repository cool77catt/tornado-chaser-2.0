using UnityEngine;

namespace TornadoChaser
{
    // Vertical HUD meter for the chase. The top is the tornado and the bottom is
    // losing it. The fill is a glassy, shaded tube whose colors blend between the
    // chase zones from ChaseTracker, and a glowing line marks the car's gap. When
    // the car gets too close, the whole meter pulses red, faster the closer it gets.
    // When it falls too far behind, it pulses blue-white, faster as the countdown runs out.
    public class ChaseMeter : MonoBehaviour
    {
        [SerializeField] ChaseTracker tracker;
        [SerializeField, Tooltip("Gap in meters shown at the bottom of the meter.")]
        float meterRange = 120f;

        [Header("Parts")]
        [SerializeField] RectTransform pulseRoot;
        [SerializeField] UnityEngine.UI.RawImage fill;
        [SerializeField] UnityEngine.UI.Image warningGlow;
        [SerializeField] RectTransform marker;
        [SerializeField] UnityEngine.UI.Image markerGlow;

        [Header("Warning pulse")]
        [SerializeField, Tooltip("Pulses per second when a warning starts, and just before the run ends.")]
        Vector2 pulseRate = new(1.5f, 5f);
        [SerializeField] float pulseScale = 0.06f;

        static readonly Color SuckedUpColor = new(0.6f, 0.04f, 0.06f);
        static readonly Color DangerColor = new(0.95f, 0.2f, 0.14f);
        static readonly Color CloseColor = new(1f, 0.66f, 0.12f);
        static readonly Color SafeColor = new(0.25f, 0.8f, 0.4f);
        static readonly Color LostColor = new(0.4f, 0.42f, 0.48f);
        // Cold and pale, so falling behind reads differently from the red of getting too close.
        static readonly Color LosingGlowColor = new(0.6f, 0.8f, 1f);

        Texture2D fillTexture;
        float pulsePhase;
        float glowAlpha;
        Color glowColor = DangerColor;

        void Start() => fill.texture = fillTexture = BuildFillTexture();

        void OnDestroy() => Destroy(fillTexture);

        void LateUpdate()
        {
            float gap = tracker.Gap;
            float position = 1f - Mathf.Clamp01(gap / meterRange);
            marker.anchorMin = new Vector2(marker.anchorMin.x, position);
            marker.anchorMax = new Vector2(marker.anchorMax.x, position);
            Color zoneColor = ColorAt(gap);
            markerGlow.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.85f);

            // Urgency runs from 0 when a warning starts to 1 just before the run ends.
            bool warning = true;
            float urgency;
            switch (tracker.Zone)
            {
                case ChaseZone.Danger or ChaseZone.SuckedUp:
                    urgency = Mathf.InverseLerp(tracker.DangerGap, tracker.SuckedUpGap, gap);
                    glowColor = DangerColor;
                    break;
                case ChaseZone.Lost:
                    urgency = 1f - Mathf.Clamp01(tracker.LostTimeRemaining / tracker.LostGracePeriod);
                    glowColor = LosingGlowColor;
                    break;
                default:
                    warning = false;
                    urgency = 0f;
                    break;
            }

            // Unscaled time keeps the pulse going on the frozen game-over screen.
            float wave = 0f;
            if (warning)
            {
                pulsePhase += Time.unscaledDeltaTime * Mathf.Lerp(pulseRate.x, pulseRate.y, urgency) * Mathf.PI * 2f;
                wave = 0.5f + 0.5f * Mathf.Sin(pulsePhase);
            }
            else
            {
                pulsePhase = 0f;
            }

            // The glow keeps its last color while fading out after a warning ends.
            float targetGlow = warning ? Mathf.Lerp(0.3f, 1f, wave) : 0f;
            glowAlpha = Mathf.MoveTowards(glowAlpha, targetGlow, Time.unscaledDeltaTime * 6f);
            warningGlow.color = new Color(glowColor.r, glowColor.g, glowColor.b, glowAlpha);

            float scale = 1f + pulseScale * wave * Mathf.Lerp(0.5f, 1f, urgency);
            pulseRoot.localScale = new Vector3(scale, scale, 1f);
        }

        // Blends between colors placed at the middle of each zone.
        Color ColorAt(float gap)
        {
            float suckedUp = tracker.SuckedUpGap, danger = tracker.DangerGap, close = tracker.CloseGap, safe = tracker.SafeGap;
            var stops = new[]
            {
                (suckedUp * 0.5f, SuckedUpColor),
                ((suckedUp + danger) * 0.5f, DangerColor),
                ((danger + close) * 0.5f, CloseColor),
                ((close + safe) * 0.5f, SafeColor),
                ((safe + meterRange) * 0.5f, LostColor),
            };
            if (gap <= stops[0].Item1) return stops[0].Item2;
            for (int i = 1; i < stops.Length; i++)
            {
                if (gap <= stops[i].Item1)
                    return Color.Lerp(stops[i - 1].Item2, stops[i].Item2, Mathf.InverseLerp(stops[i - 1].Item1, stops[i].Item1, gap));
            }
            return stops[^1].Item2;
        }

        // A capsule-shaped, tube-shaded gradient sized to the fill's aspect ratio, with
        // thin dark lines at each zone boundary.
        Texture2D BuildFillTexture()
        {
            Rect rect = fill.rectTransform.rect;
            const int width = 32;
            int height = Mathf.Max(width, Mathf.RoundToInt(width * rect.height / rect.width));
            float radius = width / 2f;
            float[] boundaries = { tracker.SuckedUpGap, tracker.DangerGap, tracker.CloseGap, tracker.SafeGap };

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float py = y + 0.5f;
                float gap = (1f - py / height) * meterRange;
                Color baseColor = ColorAt(gap);

                float boundaryLine = 0f;
                foreach (float boundary in boundaries)
                {
                    float boundaryY = (1f - boundary / meterRange) * height;
                    boundaryLine = Mathf.Max(boundaryLine, 1f - Mathf.Clamp01(Mathf.Abs(py - boundaryY) - 0.5f));
                }

                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    float u = px / width;
                    // Rounded across the width like a tube, with a glassy highlight down one side.
                    float tube = 0.6f + 0.4f * Mathf.Sin(u * Mathf.PI);
                    float highlight = 0.4f * Mathf.Exp(-Mathf.Pow((u - 0.3f) / 0.09f, 2f));
                    Color color = baseColor * tube + new Color(highlight, highlight, highlight);
                    color = Color.Lerp(color, color * 0.35f, boundaryLine);

                    float centerY = Mathf.Clamp(py, radius, height - radius);
                    float distance = Vector2.Distance(new Vector2(px, py), new Vector2(radius, centerY));
                    color.a = Mathf.Clamp01(radius - distance + 0.5f);
                    pixels[y * width + x] = color;
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "ChaseMeterFill",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
