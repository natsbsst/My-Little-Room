using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class ApartmentTimeOfDay : MonoBehaviour
{
    public enum Mode { Day, Night }

    Renderer[] skyBackdrops;
    Material daySky;
    Material[] nightSkies;
    GameObject[] illuminatedCityWindows;
    Light[] nightLights;
    Light sun;
    Camera previewCamera;
    Button dayButton;
    Button nightButton;

    public Mode CurrentMode { get; private set; }

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
        if (!scene.isLoaded)
            return;

        foreach (var root in scene.GetRootGameObjects())
        {
            if (!root.name.StartsWith("MyLittleRoom_Apartment", StringComparison.Ordinal) ||
                root.GetComponent<ApartmentTimeOfDay>())
                continue;

            var settings = Resources.Load<ApartmentTimeOfDaySettings>("ApartmentTimeOfDay");
            if (!settings || !settings.DaySky)
            {
                Debug.LogError("My Little Room: day sky material is missing.");
                return;
            }

            var controller = root.AddComponent<ApartmentTimeOfDay>();
            if (!controller.Configure(settings.DaySky))
                Destroy(controller);
            return;
        }
    }

    bool Configure(Material daylight)
    {
        var objects = GetComponentsInChildren<Transform>(true);
        skyBackdrops = objects.Where(t => t.name == "NIGHT_Sky_Backdrop" || t.name == "Living_NIGHT_Sky")
            .Select(t => t.GetComponent<Renderer>()).Where(r => r).ToArray();
        if (skyBackdrops.Length != 2)
        {
            Debug.LogError("My Little Room: the apartment is missing its sky backdrops.", this);
            return false;
        }

        daySky = daylight;
        nightSkies = skyBackdrops.Select(r => r.sharedMaterial).ToArray();
        illuminatedCityWindows = objects.Where(t =>
                t.name.StartsWith("NIGHT_Lit_NYC_Window_", StringComparison.Ordinal) ||
                t.name.StartsWith("Living_NIGHT_NYC_Window_", StringComparison.Ordinal))
            .Select(t => t.gameObject).ToArray();
        nightLights = objects.Where(t => t.name == "NIGHT_Bedside" ||
                t.name == "NIGHT_Desk" || t.name == "NIGHT_FairyGlow")
            .Select(t => t.GetComponent<Light>()).Where(l => l).ToArray();
        sun = gameObject.scene.GetRootGameObjects()
            .Where(root => root.name == "Directional Light")
            .Select(root => root.GetComponent<Light>())
            .FirstOrDefault(light => light && light.type == LightType.Directional);
        return true;
    }

    void Start()
    {
        if (skyBackdrops == null || skyBackdrops.Length != 2)
            return;
        FrameDefaultCamera();
        CreateControls();
        SetNight();
    }

    public void SetDay() => SetMode(Mode.Day);
    public void SetNight() => SetMode(Mode.Night);

    void SetMode(Mode mode)
    {
        CurrentMode = mode;
        bool day = mode == Mode.Day;

        for (int i = 0; i < skyBackdrops.Length; i++)
            skyBackdrops[i].sharedMaterial = day ? daySky : nightSkies[i];
        foreach (var window in illuminatedCityWindows)
            window.SetActive(!day);
        foreach (var light in nightLights)
            light.gameObject.SetActive(!day);

        if (sun)
            sun.enabled = day;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = day ? new Color(.55f, .60f, .69f) : new Color(.18f, .20f, .29f);
        RenderSettings.reflectionIntensity = day ? 1f : .25f;

        if (previewCamera)
            previewCamera.backgroundColor = day ? new Color(.42f, .70f, .95f) : new Color(.025f, .04f, .10f);

        if (dayButton) dayButton.interactable = !day;
        if (nightButton) nightButton.interactable = day;
    }

    void FrameDefaultCamera()
    {
        var camera = Camera.main;
        if (!camera || camera.gameObject.scene != gameObject.scene ||
            Vector3.Distance(camera.transform.position, new Vector3(0f, 1f, -10f)) > .01f ||
            Quaternion.Angle(camera.transform.rotation, Quaternion.identity) > .1f)
            return;

        var eye = transform.TransformPoint(new Vector3(-17f, 14f, -19f));
        var center = transform.TransformPoint(new Vector3(-6.7f, 1.15f, -7.7f));
        camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(center - eye));
        camera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera = camera;
    }

    void CreateControls()
    {
        if (!EventSystem.current)
            new GameObject("Time of Day Event System", typeof(EventSystem), typeof(InputSystemUIInputModule))
                .transform.SetParent(transform, false);

        var controls = new GameObject("Time of Day Controls", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        controls.transform.SetParent(transform, false);
        var canvas = controls.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = controls.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;

        dayButton = AddButton(controls.transform, "День", 16f);
        nightButton = AddButton(controls.transform, "Ночь", 112f);
        dayButton.onClick.AddListener(SetDay);
        nightButton.onClick.AddListener(SetNight);
    }

    static Button AddButton(Transform parent, string label, float left)
    {
        var control = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        control.transform.SetParent(parent, false);
        var rect = control.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -16f);
        rect.sizeDelta = new Vector2(88f, 38f);
        var image = control.GetComponent<Image>();
        image.color = new Color(.12f, .16f, .23f);
        var button = control.GetComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(.80f, .89f, 1f);
        colors.disabledColor = new Color(.49f, .67f, .94f);
        button.colors = colors;

        var caption = new GameObject("Label", typeof(RectTransform), typeof(Text));
        caption.transform.SetParent(control.transform, false);
        var text = caption.GetComponent<Text>();
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 19;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = label;
        return button;
    }
}
