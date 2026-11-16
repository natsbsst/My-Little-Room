using UnityEngine;

/// <summary>Автоматическая дверь; не зависит от способа управления персонажем.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class DoorController : MonoBehaviour
{
    [Min(0.01f)] public float openDistance = 1.5f;
    [Min(0.02f)] public float closeDistance = 2f;
    [Range(-179f, 179f)] public float openAngle = 90f;
    [Min(0.01f)] public float animationDuration = 0.8f;
    public Transform doorHinge;
    public Transform targetCharacter;
    [Tooltip("Неподвижный центр проёма. Локальная X направлена вдоль его ширины.")]
    public Transform doorwayCenter;
    [Min(0f)] public float doorwayHalfWidth = 0.52f;
    [Tooltip("Только коллайдеры движущегося полотна, не стен и коробки.")]
    public Collider[] doorColliders = new Collider[0];

    Quaternion closedRotation;
    Vector3 axisInParent;
    Vector3 fallbackCenter;
    float progress;
    bool wantsOpen;
    bool initialized;
    CharacterController characterBody;

    public float OpenFraction => progress;
    public bool WantsOpen => wantsOpen;

    void Awake() => Initialize();

    void Initialize()
    {
        if (initialized) return;
        if (!doorHinge) doorHinge = transform;
        if (!targetCharacter)
        {
            Debug.LogError("DoorController: назначьте существующего персонажа.", this);
            enabled = false;
            return;
        }
        // У импортированных петель локальная Y может быть горизонтальной.
        closedRotation = doorHinge.localRotation;
        axisInParent = doorHinge.parent
            ? doorHinge.parent.InverseTransformDirection(Vector3.up).normalized : Vector3.up;
        fallbackCenter = doorwayCenter ? doorwayCenter.position : doorHinge.position;
        characterBody = targetCharacter.GetComponent<CharacterController>();
        initialized = true;
    }

    // Расстояние в плоскости пола до отрезка проёма, а не до движущегося полотна.
    public float DistanceToDoorway(Vector3 position)
    {
        Vector3 center = doorwayCenter ? doorwayCenter.position : fallbackCenter;
        Vector3 width = doorwayCenter
            ? Vector3.ProjectOnPlane(doorwayCenter.right, Vector3.up).normalized : Vector3.zero;
        Vector3 offset = Vector3.ProjectOnPlane(position - center, Vector3.up);
        Vector3 closest = width * Mathf.Clamp(Vector3.Dot(offset, width),
            -doorwayHalfWidth, doorwayHalfWidth);
        return (offset - closest).magnitude;
    }

    void Update() => Step(Time.deltaTime);

    void Step(float deltaTime)
    {
        if (!initialized || !targetCharacter || !doorHinge) return;
        float distance = DistanceToDoorway(targetCharacter.position);
        if (distance <= openDistance) wantsOpen = true;
        else if (distance > Mathf.Max(closeDistance, openDistance + 0.01f)) wantsOpen = false;
        // Между порогами сохраняем состояние: возле двери нет дрожания.
        float previousProgress = progress;
        Quaternion previousRotation = doorHinge.localRotation;
        progress = Mathf.MoveTowards(progress, wantsOpen ? 1f : 0f,
            Mathf.Max(0f, deltaTime) / Mathf.Max(0.01f, animationDuration));
        float eased = progress * progress * (3f - 2f * progress);
        Quaternion opened = Quaternion.AngleAxis(openAngle, axisInParent) * closedRotation;
        doorHinge.localRotation = Quaternion.Slerp(closedRotation, opened, eased);
        // Полотно остаётся физическим при любом угле. Не вращаем его внутрь персонажа.
        if (characterBody && characterBody.enabled)
            foreach (var collider in doorColliders)
                if (collider && collider.enabled && Physics.ComputePenetration(
                    collider, collider.transform.position, collider.transform.rotation,
                    characterBody, characterBody.transform.position, characterBody.transform.rotation,
                    out _, out float depth) && depth > 0.005f)
                {
                    progress = previousProgress;
                    doorHinge.localRotation = previousRotation;
                    if (!wantsOpen) wantsOpen = true;
                    break;
                }
    }

    void OnValidate()
    {
        openDistance = Mathf.Max(0.01f, openDistance);
        closeDistance = Mathf.Max(openDistance + 0.01f, closeDistance);
        animationDuration = Mathf.Max(0.01f, animationDuration);
        doorwayHalfWidth = Mathf.Max(0f, doorwayHalfWidth);
    }
}
