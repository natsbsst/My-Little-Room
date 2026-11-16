using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class DayNightCycle : MonoBehaviour
{
    public const float CycleSeconds = 180f;

    [SerializeField, Tooltip("Stop the clock and preview the selected hour.")]
    bool manualTime;
    [SerializeField, Range(0f, 24f)]
    float previewHour = 12f;

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    readonly List<(Light light, float intensity)> roomLights = new();
    readonly List<(Renderer renderer, int slot, Color emission, MaterialPropertyBlock block)> lampGlow = new();
    readonly Dictionary<Material, Material> windowMaterials = new();
    readonly List<Renderer> cityWindows = new();

    Renderer[] skies;
    Material sky;
    Light sun;
    float sunIntensity;
    float sunAzimuth;
    Camera previewCamera;
    Gradient skyColors;
    Gradient ambientColors;
    Gradient sunlightColors;
    const double SecondsPerHour = CycleSeconds / 24.0;
    double elapsedSeconds = 5 * SecondsPerHour;
    float hour = 5f;

    public float CurrentHour => hour;
    // Session counters for future database records; menus and pause do not advance them.
    [SerializeField] double sessionPlaySeconds;
    public double SessionPlaySeconds => sessionPlaySeconds;
    public double TotalGameHours => 5.0 + sessionPlaySeconds / SecondsPerHour;
    public int CurrentDay => (int)Math.Floor(TotalGameHours / 24.0) + 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            OnSceneLoaded(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        EnsureInScene(scene);
    }

    public static void EnsureInScene(Scene scene)
    {
        if (!scene.isLoaded)
            return;

        var apartment = scene.GetRootGameObjects().FirstOrDefault(root =>
            root.name.StartsWith("MyLittleRoom_Apartment", StringComparison.Ordinal));
        if (!apartment || apartment.GetComponent<DayNightCycle>())
            return;

        var settings = Resources.Load<ApartmentTimeOfDaySettings>("ApartmentTimeOfDay");
        if (!settings || !settings.DaySky)
        {
            Debug.LogError("My Little Room: the day sky material is missing.");
            return;
        }

        var cycle = apartment.AddComponent<DayNightCycle>();
        if (!cycle.Configure(settings.DaySky))
            Destroy(cycle);
    }

    bool Configure(Material daySky)
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        skies = renderers.Where(r => r.name == "NIGHT_Sky_Backdrop" || r.name == "Living_NIGHT_Sky").ToArray();
        if (skies.Length != 2 || !skies[0].sharedMaterial)
        {
            Debug.LogError("My Little Room: the apartment is missing its sky backdrops.", this);
            return false;
        }

        Color night = skies[0].sharedMaterial.GetColor(BaseColor);
        Color day = daySky.GetColor(BaseColor);
        sky = new Material(daySky) { name = "Apartment sky (runtime)" };
        sky.EnableKeyword("_EMISSION");
        sky.SetFloat("_ReceiveShadows", 0f);
        sky.EnableKeyword("_RECEIVE_SHADOWS_OFF");
        foreach (var backdrop in skies)
        {
            backdrop.sharedMaterial = sky;
            backdrop.shadowCastingMode = ShadowCastingMode.Off;
            backdrop.receiveShadows = false;
        }

        skyColors = Palette(night, new Color(.90f, .55f, .57f),
            day, new Color(1f, .47f, .30f));
        ambientColors = Palette(new Color(.18f, .20f, .29f), new Color(.60f, .42f, .51f),
            new Color(.65f, .70f, .80f), new Color(.51f, .32f, .37f));
        sunlightColors = Palette(new Color(.42f, .52f, .85f), new Color(1f, .66f, .55f),
            new Color(1f, .96f, .89f), new Color(1f, .51f, .28f));

        foreach (var light in GetComponentsInChildren<Light>(true))
        {
            if (InBathroom(light.transform))
            {
                light.enabled = true;
                if (light.intensity <= 0f)
                    light.intensity = 1f;
            }
            else if (light.name != "Fridge_Interior_Light" || light.enabled)
            {
                roomLights.Add((light, light.intensity > 0f ? light.intensity : 1f));
            }
        }

        foreach (var renderer in renderers)
        {
            if (renderer.name.StartsWith("NIGHT_Lit_NYC_Window_", StringComparison.Ordinal) ||
                renderer.name.StartsWith("Living_NIGHT_NYC_Window_", StringComparison.Ordinal))
                PrepareCityWindow(renderer);
            else if (!InBathroom(renderer.transform))
                PrepareLampGlow(renderer);
        }

        sun = gameObject.scene.GetRootGameObjects().Where(root => root.name == "Directional Light")
            .Select(root => root.GetComponent<Light>())
            .FirstOrDefault(light => light && light.type == LightType.Directional);
        if (sun)
        {
            sunIntensity = Mathf.Max(sun.intensity, 1f);
            sunAzimuth = sun.transform.eulerAngles.y;
        }
        return true;
    }

    void Start()
    {
        if (!sky)
            return;
        FrameDefaultCamera();
        ApplyHour();
    }

    void Update()
    {
        if (sky)
            Advance(Time.deltaTime);
    }

    void Advance(double seconds)
    {
        if (!manualTime && seconds > 0) sessionPlaySeconds += seconds;
        elapsedSeconds = manualTime ? Mathf.Repeat(previewHour, 24f) * SecondsPerHour
            : (elapsedSeconds + seconds) % CycleSeconds;
        hour = (float)(elapsedSeconds / SecondsPerHour);
        ApplyHour();
    }

    public void SetTimeOfDay(float time)
    {
        previewHour = Mathf.Repeat(time, 24f);
        manualTime = true;
        if (sky)
            Advance(0f);
    }

    [ContextMenu("Resume automatic cycle")]
    public void ResumeAutomaticCycle() => manualTime = false;

    void ApplyHour()
    {
        float sunlight = Fade(5f, 8f, hour) * (1f - Fade(17f, 21f, hour));
        float artificial = hour < 12f ? 1f - Fade(5f, 8f, hour) : Fade(17f, 20f, hour);
        float cityGlow = hour < 12f ? 1f - Fade(5f, 7.5f, hour) : Fade(18.5f, 21f, hour);

        float fraction = hour / 24f;
        Color color = skyColors.Evaluate(fraction);
        sky.SetColor(BaseColor, color);
        sky.SetColor(EmissionColor, color * Mathf.Lerp(.35f, .70f, sunlight));
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColors.Evaluate(fraction);
        RenderSettings.reflectionIntensity = Mathf.Lerp(.25f, 1f, sunlight);
        if (previewCamera)
            previewCamera.backgroundColor = color;

        if (sun)
        {
            sun.color = sunlightColors.Evaluate(fraction);
            sun.intensity = sunIntensity * sunlight;
            sun.enabled = sunlight > 0f;
            float altitude = Mathf.Sin(Mathf.InverseLerp(5f, 21f, hour) * Mathf.PI);
            sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(12f, 60f, altitude), sunAzimuth, 0f);
        }

        foreach (var (light, intensity) in roomLights)
        {
            if (!light)
                continue;
            light.intensity = intensity * artificial;
            light.enabled = artificial > 0f;
        }

        foreach (var (renderer, slot, emission, block) in lampGlow)
        {
            if (!renderer)
                continue;
            block.SetColor(EmissionColor, emission * artificial);
            renderer.SetPropertyBlock(block, slot);
        }

        foreach (var material in windowMaterials.Values)
        {
            Color tint = material.GetColor(BaseColor);
            tint.a = cityGlow;
            material.SetColor(BaseColor, tint);
            material.SetColor(EmissionColor, new Color(.73f, .43f, .13f) * cityGlow);
        }
        foreach (var window in cityWindows)
            if (window && window.enabled != (cityGlow > 0f))
                window.enabled = cityGlow > 0f;
    }

    void PrepareLampGlow(Renderer renderer)
    {
        var materials = renderer.sharedMaterials;
        for (int slot = 0; slot < materials.Length; slot++)
        {
            var material = materials[slot];
            if (!material || !material.HasProperty(EmissionColor) ||
                (material.name != "URP_Bedroom_Warm_Emissive" &&
                 material.name != "URP_KL_Warm_Lamp" && material.name != "URP_KL_Fire_Glow"))
                continue;

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, slot);
            if (block.isEmpty)
                renderer.GetPropertyBlock(block);
            lampGlow.Add((renderer, slot, material.GetColor(EmissionColor), block));
        }
    }

    void PrepareCityWindow(Renderer renderer)
    {
        var source = renderer.sharedMaterial;
        if (!source || !source.HasProperty(BaseColor))
            return;

        if (!windowMaterials.TryGetValue(source, out var overlay))
        {
            overlay = new Material(source) { name = source.name + " (runtime window fade)" };
            overlay.SetFloat("_Surface", 1f);
            overlay.SetFloat("_Blend", 0f);
            overlay.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            overlay.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            overlay.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            overlay.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            overlay.SetFloat("_ZWrite", 0f);
            overlay.SetOverrideTag("RenderType", "Transparent");
            overlay.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            overlay.EnableKeyword("_EMISSION");
            overlay.renderQueue = (int)RenderQueue.Transparent;
            overlay.SetShaderPassEnabled("ShadowCaster", false);
            windowMaterials.Add(source, overlay);
        }

        renderer.sharedMaterial = overlay;
        cityWindows.Add(renderer);
    }

    static bool InBathroom(Transform current)
    {
        for (; current; current = current.parent)
            if (current.name.StartsWith("Bathroom_", StringComparison.Ordinal))
                return true;
        return false;
    }

    static float Fade(float start, float end, float time)
    {
        float t = Mathf.InverseLerp(start, end, time);
        return t * t * (3f - 2f * t);
    }

    static Gradient Palette(Color night, Color dawn, Color day, Color evening)
    {
        var gradient = new Gradient { mode = GradientMode.Blend };
        gradient.SetKeys(new[]
        {
            new GradientColorKey(night, 0f),
            new GradientColorKey(night, 5f / 24f),
            new GradientColorKey(dawn, 6.5f / 24f),
            new GradientColorKey(day, 8f / 24f),
            new GradientColorKey(day, 17f / 24f),
            new GradientColorKey(evening, 18.8f / 24f),
            new GradientColorKey(night, 21f / 24f),
            new GradientColorKey(night, 1f)
        }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }

    void FrameDefaultCamera()
    {
        var camera = Camera.main;
        if (!camera || camera.gameObject.scene != gameObject.scene)
            return;
        if (camera.TryGetComponent<ThirdPersonMannequinCamera>(out _))
        {
            previewCamera = camera;
            return;
        }
        if (Vector3.Distance(camera.transform.position, new Vector3(0f, 1f, -10f)) > .01f ||
            Quaternion.Angle(camera.transform.rotation, Quaternion.identity) > .1f)
            return;

        var eye = transform.TransformPoint(new Vector3(-17f, 14f, -19f));
        var center = transform.TransformPoint(new Vector3(-6.7f, 1.15f, -7.7f));
        camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(center - eye));
        camera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera = camera;
    }

    void OnDestroy()
    {
        if (sky) Destroy(sky);
        foreach (var material in windowMaterials.Values)
            if (material) Destroy(material);
    }
}
