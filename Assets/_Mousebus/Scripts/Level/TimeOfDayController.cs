using UnityEngine;
using UnityEngine.Rendering;

// Attach to: any empty GameObject in the level scene (e.g. "TimeOfDay").
// Drag the scene's Directional Light into the Sun field.
// Use Mousebus → Add Time Of Day  to stamp this into the scene automatically.
// Use Mousebus → Setup Sky and DoF to create the skybox material and depth-of-field volume.
//
// Set realSecondsPerGameHour low for fast testing (e.g. 12 = full day in ~2 min).
// Match LevelManager.realSecondsPerGameHour (90) for the final game feel.
// While in the Editor, drag the Preview slider to scrub through the day without playing.
[ExecuteAlways]
public class TimeOfDayController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Light    sun;
    [Tooltip("The Skybox/Procedural material assigned to this scene. " +
             "Created automatically by Mousebus → Setup Sky and DoF.")]
    [SerializeField] private Material skyboxMaterial;

    [Header("Clock")]
    [Tooltip("Hour the level starts (24 h)")]
    [SerializeField] private float startHour = 8f;
    [Tooltip("Hour the level ends (24 h)")]
    [SerializeField] private float endHour = 18f;
    [Tooltip("Real seconds per in-game hour. Lower = faster day. Match LevelManager for production.")]
    [SerializeField] private float realSecondsPerGameHour = 12f;

    [Header("Sun Arc")]
    [Tooltip("Sun elevation at start hour (degrees above horizon)")]
    [SerializeField] private float sunriseElevation = 10f;
    [Tooltip("Sun elevation at solar noon (degrees above horizon)")]
    [SerializeField] private float noonElevation = 55f;
    [Tooltip("Compass direction the sun rises from (90 = due east)")]
    [SerializeField] private float startAzimuth = 80f;

    [Header("Sun Light")]
    [SerializeField] private Gradient       sunColor;
    [SerializeField] private AnimationCurve sunIntensity;

    [Header("Skybox (Procedural)")]
    [Tooltip("Sky tint color across the day — fed into the skybox _SkyTint property.")]
    [SerializeField] private Gradient       skyTint;
    [Tooltip("Ground/horizon color across the day — fed into _GroundColor.")]
    [SerializeField] private Gradient       skyGroundColor;
    [Tooltip("Skybox exposure across the day — dims at dawn and dusk.")]
    [SerializeField] private AnimationCurve skyExposure;

    [Header("Ambient")]
    [SerializeField] private Gradient ambientSkyColor;
    [SerializeField] private Gradient ambientEquatorColor;
    [SerializeField] private Gradient ambientGroundColor;

    [Header("Fog")]
    [SerializeField] private bool          controlFog = true;
    [SerializeField] private Gradient      fogColor;
    [Tooltip("Distance at which fog begins — closer in the morning, farther at noon.")]
    [SerializeField] private AnimationCurve fogStart;
    [Tooltip("Distance at which fog is fully opaque.")]
    [SerializeField] private AnimationCurve fogEnd;

    [Header("Editor Preview")]
    [Tooltip("Drag to scrub through the day in the Editor without entering Play mode")]
    [Range(0f, 1f)]
    [SerializeField] private float previewT = 0f;

    private float _elapsed;

    private void OnEnable() => _elapsed = 0f;

    // Reset() fires when this component is first added in the Editor —
    // seeds all gradients with Vancouver sky defaults so the system looks good immediately.
    private void Reset()
    {
        sunColor = MakeGradient(
            (0.00f, new Color(1.00f, 0.50f, 0.20f)),   // sunrise — burnt orange
            (0.20f, new Color(1.00f, 0.88f, 0.65f)),   // morning — warm gold
            (0.50f, new Color(1.00f, 0.97f, 0.92f)),   // noon    — cool white
            (0.80f, new Color(1.00f, 0.82f, 0.55f)),   // late afternoon — amber
            (1.00f, new Color(1.00f, 0.42f, 0.12f))    // sunset  — deep orange
        );

        sunIntensity = new AnimationCurve(
            new Keyframe(0.00f, 0.5f),
            new Keyframe(0.50f, 1.4f),
            new Keyframe(1.00f, 0.4f)
        );
        SmoothenCurve(sunIntensity);

        skyTint = MakeGradient(
            (0.00f, new Color(0.55f, 0.38f, 0.48f)),   // dawn — muted purple
            (0.15f, new Color(0.80f, 0.60f, 0.50f)),   // sunrise pink
            (0.40f, new Color(0.52f, 0.72f, 1.00f)),   // morning blue
            (0.50f, new Color(0.46f, 0.70f, 1.00f)),   // noon blue
            (0.85f, new Color(0.85f, 0.55f, 0.30f)),   // golden hour
            (1.00f, new Color(0.45f, 0.28f, 0.35f))    // dusk
        );

        skyGroundColor = MakeGradient(
            (0.00f, new Color(0.20f, 0.16f, 0.18f)),
            (0.30f, new Color(0.38f, 0.36f, 0.32f)),
            (0.50f, new Color(0.45f, 0.44f, 0.40f)),
            (0.80f, new Color(0.42f, 0.35f, 0.28f)),
            (1.00f, new Color(0.22f, 0.16f, 0.16f))
        );

        skyExposure = new AnimationCurve(
            new Keyframe(0.00f, 0.6f),
            new Keyframe(0.15f, 0.9f),
            new Keyframe(0.50f, 1.3f),
            new Keyframe(0.85f, 0.9f),
            new Keyframe(1.00f, 0.5f)
        );
        SmoothenCurve(skyExposure);

        ambientSkyColor = MakeGradient(
            (0.00f, new Color(0.40f, 0.28f, 0.38f)),
            (0.15f, new Color(0.70f, 0.48f, 0.38f)),
            (0.35f, new Color(0.46f, 0.60f, 0.88f)),
            (0.50f, new Color(0.52f, 0.68f, 0.98f)),
            (0.85f, new Color(0.70f, 0.45f, 0.28f)),
            (1.00f, new Color(0.35f, 0.22f, 0.30f))
        );

        ambientEquatorColor = MakeGradient(
            (0.00f, new Color(0.35f, 0.28f, 0.30f)),
            (0.50f, new Color(0.65f, 0.70f, 0.78f)),
            (1.00f, new Color(0.55f, 0.35f, 0.25f))
        );

        ambientGroundColor = MakeGradient(
            (0.00f, new Color(0.10f, 0.10f, 0.12f)),
            (0.50f, new Color(0.22f, 0.22f, 0.18f)),
            (1.00f, new Color(0.18f, 0.12f, 0.10f))
        );

        fogColor = MakeGradient(
            (0.00f, new Color(0.38f, 0.30f, 0.35f)),   // dawn haze
            (0.20f, new Color(0.72f, 0.60f, 0.50f)),   // morning mist
            (0.50f, new Color(0.68f, 0.74f, 0.86f)),   // midday
            (0.80f, new Color(0.75f, 0.58f, 0.42f)),   // golden hour haze
            (1.00f, new Color(0.45f, 0.28f, 0.30f))    // dusk
        );

        // Fog tighter in the morning (atmospheric haze), opens up by noon
        fogStart = new AnimationCurve(
            new Keyframe(0.00f, 20f),
            new Keyframe(0.50f, 45f),
            new Keyframe(1.00f, 18f)
        );
        SmoothenCurve(fogStart);

        fogEnd = new AnimationCurve(
            new Keyframe(0.00f,  60f),
            new Keyframe(0.50f, 110f),
            new Keyframe(1.00f,  55f)
        );
        SmoothenCurve(fogEnd);
    }

    private void Update()
    {
        float t;
        if (!Application.isPlaying)
            t = previewT;
        else
        {
            _elapsed += Time.deltaTime;
            t = Mathf.Clamp01(_elapsed / ((endHour - startHour) * realSecondsPerGameHour));
        }
        Apply(t);
    }

    private void Apply(float t)
    {
        // ── Sun ────────────────────────────────────────────────────────────
        float elevation = Mathf.Lerp(sunriseElevation, noonElevation, Mathf.Sin(t * Mathf.PI));
        float azimuth   = startAzimuth + t * 180f;

        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(elevation, azimuth, 0f);
            if (sunColor     != null) sun.color     = sunColor.Evaluate(t);
            if (sunIntensity != null) sun.intensity = sunIntensity.Evaluate(t);
        }

        // ── Skybox ─────────────────────────────────────────────────────────
        if (skyboxMaterial != null)
        {
            if (skyTint        != null) skyboxMaterial.SetColor("_SkyTint",    skyTint.Evaluate(t));
            if (skyGroundColor != null) skyboxMaterial.SetColor("_GroundColor", skyGroundColor.Evaluate(t));
            if (skyExposure    != null) skyboxMaterial.SetFloat("_Exposure",    skyExposure.Evaluate(t));
        }

        // ── Ambient ────────────────────────────────────────────────────────
        RenderSettings.ambientMode = AmbientMode.Trilight;
        if (ambientSkyColor     != null) RenderSettings.ambientSkyColor     = ambientSkyColor.Evaluate(t);
        if (ambientEquatorColor != null) RenderSettings.ambientEquatorColor = ambientEquatorColor.Evaluate(t);
        if (ambientGroundColor  != null) RenderSettings.ambientGroundColor  = ambientGroundColor.Evaluate(t);

        // ── Fog ────────────────────────────────────────────────────────────
        if (controlFog)
        {
            if (fogColor != null) RenderSettings.fogColor         = fogColor.Evaluate(t);
            if (fogStart != null) RenderSettings.fogStartDistance = fogStart.Evaluate(t);
            if (fogEnd   != null) RenderSettings.fogEndDistance   = fogEnd.Evaluate(t);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static Gradient MakeGradient(params (float t, Color c)[] keys)
    {
        var g         = new Gradient();
        var colorKeys = new GradientColorKey[keys.Length];
        for (int i = 0; i < keys.Length; i++)
            colorKeys[i] = new GradientColorKey(keys[i].c, keys[i].t);
        g.SetKeys(colorKeys, new[]
        {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(1f, 1f)
        });
        return g;
    }

    private static void SmoothenCurve(AnimationCurve curve)
    {
        for (int i = 0; i < curve.length; i++)
            curve.SmoothTangents(i, 0f);
    }
}
