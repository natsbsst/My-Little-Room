using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CharacterController))]
public sealed class MannequinWalk : MonoBehaviour
{
    const float WalkSpeed = 2.5f;
    const float TurnSpeed = 140f;
    [SerializeField, Min(0.1f)] float jumpHeight = 0.8f;
    static readonly int Speed = Animator.StringToHash("Speed");
    static readonly int Idle = Animator.StringToHash("Idle");
    static readonly int Walk = Animator.StringToHash("Walk");

    Animator animator;
    CharacterController body;
    NavMeshAgent agent;
    DoorController[] doors;
    float verticalSpeed;
    public bool TryJump()
    {
        var mode = GetComponent<ControlModeManager>();
        var interaction = GetComponent<ManualInteraction>();
        if (!enabled || !body || !body.enabled || !body.isGrounded || verticalSpeed > 0f ||
            Time.timeScale <= 0f || Physics.gravity.y >= 0f ||
            (mode && mode.IsAutoMode) || (interaction && interaction.IsBusy)) return false;
        var state = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        if (state != Idle && state != Walk) return false;
        StopNavigation();
        verticalSpeed = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
        return true;
    }
    public bool IsNavigating => agent && agent.enabled && agent.isOnNavMesh &&
        (agent.pathPending || (agent.hasPath && agent.remainingDistance > agent.stoppingDistance));

    // Единственная точка запуска автономного пути. Решение, куда идти, остаётся за ИИ.
    public bool SetDestination(Vector3 destination)
    {
        if (!agent) return false;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas };
        if (!NavMesh.SamplePosition(destination, out var goal, 0.6f, filter) ||
            !NavMesh.SamplePosition(transform.position, out var start, 0.5f, filter)) return false;
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(start.position, goal.position, filter, path) ||
            path.status != NavMeshPathStatus.PathComplete) return false;
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.enabled = true;
        if (!agent.isOnNavMesh) { agent.enabled = false; return false; }
        agent.nextPosition = transform.position;
        return agent.SetPath(path);
    }

    public void StopNavigation()
    {
        if (!agent || !agent.enabled) return;
        if (agent.isOnNavMesh) agent.ResetPath();
        agent.enabled = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Connect()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            OnSceneLoaded(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        if (!scene.isLoaded || scene.path != "Assets/Scenes/SampleScene.unity")
            return;
        var mannequin = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "MyLittleRoom_Mannequin");
        if (mannequin && !mannequin.GetComponent<MannequinWalk>())
            mannequin.AddComponent<MannequinWalk>();
        if (mannequin && !mannequin.GetComponent<LifeBootstrap>())
            mannequin.AddComponent<LifeBootstrap>();
        if (mannequin && Camera.main && Camera.main.gameObject.scene == scene)
        {
            var camera = Camera.main;
            var follow = camera.GetComponent<ThirdPersonMannequinCamera>();
            if (!follow)
                follow = camera.gameObject.AddComponent<ThirdPersonMannequinCamera>();
            follow.Follow(mannequin.transform);
        }
    }

    void Start()
    {
        animator = GetComponent<Animator>();
        body = GetComponent<CharacterController>();
        agent = GetComponent<NavMeshAgent>();
        doors = FindObjectsByType<DoorController>();
        if (agent) { agent.updatePosition = false; agent.updateRotation = false; }
        if (!animator.parameters.Any(parameter => parameter.nameHash == Speed))
        {
            Debug.LogError("Mannequin Animator is missing its Speed parameter.", this);
            enabled = false;
            return;
        }
    }

    void OnAnimatorMove()
    {
        if (!animator)
            return;
        int state = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        if (state != Idle && state != Walk)
        {
            // Даже перемещение из анимации проходит через тот же физический контроллер.
            Physics.SyncTransforms();
            body.Move(animator.deltaPosition);
            transform.rotation *= animator.deltaRotation;
        }
    }

    void Update()
    {
        if (!animator || !body || !body.enabled)
            return;
        float dt = Time.deltaTime;
        Vector3 velocity = Vector3.zero;
        var state = animator.GetCurrentAnimatorStateInfo(0);
        bool canWalk = state.shortNameHash == Idle || state.shortNameHash == Walk;
        var keyboard = Keyboard.current;
        float forward = 0f, strafe = 0f;
        var mode = GetComponent<ControlModeManager>();
        bool manual = !mode || !mode.IsAutoMode;
        if (canWalk && manual && Time.timeScale > 0 && Application.isFocused && keyboard != null)
        {
            forward = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            strafe = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            if (keyboard.spaceKey.wasPressedThisFrame) TryJump();
        }
        if (forward != 0f || strafe != 0f)
        {
            StopNavigation(); // Ручной ввод получает приоритет без второго двигателя.
            var view = Camera.main ? Camera.main.transform : transform;
            var facing = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, facing);
            velocity = Vector3.ClampMagnitude(facing * forward + right * strafe, 1f) * WalkSpeed;
            if (velocity.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(velocity), 540f * dt);
        }
        else if (canWalk && IsNavigating)
        {
            velocity = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(agent.desiredVelocity, Vector3.up), WalkSpeed);
            if (velocity.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(velocity), TurnSpeed * dt);
        }
        // Ждём открытия перед зоной взмаха полотна. Отход от двери всегда разрешён.
        foreach (var door in doors)
        {
            if (!door || door.OpenFraction >= 0.98f) continue;
            float distance = door.DistanceToDoorway(transform.position);
            float next = door.DistanceToDoorway(transform.position + velocity * dt);
            if (next < 1.4f && next < distance - 0.0001f) { velocity = Vector3.zero; break; }
        }
        if (body.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
        else verticalSpeed = Mathf.Max(verticalSpeed + Physics.gravity.y * dt, -30f);
        // Синхронизируем поворот дверных Collider до CharacterController.Move.
        Physics.SyncTransforms();
        Vector3 before = transform.position;
        var collisions = body.Move((velocity + Vector3.up * verticalSpeed) * dt);
        if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
        if (agent && agent.enabled && agent.isOnNavMesh)
        {
            agent.nextPosition = transform.position;
            // Небольшой допуск учитывает skinWidth капсулы и исключает вечное
            // ожидание последних миллиметров пути возле точки назначения.
            if (!agent.pathPending && agent.hasPath && agent.remainingDistance <= agent.stoppingDistance + 0.03f)
                StopNavigation();
        }
        float actualSpeed = Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude / Mathf.Max(dt, 0.0001f);
        animator.SetFloat(Speed, canWalk ? Mathf.Clamp01(actualSpeed / WalkSpeed) : 0f);
    }
}
