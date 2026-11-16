using System.Linq;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class LifeBootstrap : MonoBehaviour
{
    public static readonly string[] Furniture = { "Fridge_Body", "Bathroom_Shower_Tray", "Bed_Mattress", "Desk_Top", "Living_Sofa_Base" };
    public static readonly string[] Actions = { "Eat", "Wash", "Sleep", "Work", "Relax" };
    public static readonly NeedType[] Needs = { NeedType.Satiety, NeedType.Hygiene, NeedType.Sleep, NeedType.Work, NeedType.Leisure };
    T Ensure<T>() where T : Component => GetComponent<T>() ?? gameObject.AddComponent<T>();
    void Start()
    {
        // Initialize each loaded scene directly; editor recompilation can clear static
        // sceneLoaded subscriptions without rerunning RuntimeInitializeOnLoadMethod.
        DayNightCycle.EnsureInScene(gameObject.scene);
        var camera = gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault(candidate => candidate.CompareTag("MainCamera"));
        if (camera)
        {
            var follow = camera.GetComponent<ThirdPersonMannequinCamera>();
            if (!follow) follow = camera.gameObject.AddComponent<ThirdPersonMannequinCamera>();
            follow.Follow(transform);
        }
        Ensure<NPCNeeds>();
        var agent = Ensure<NavMeshAgent>();
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas };
        var transforms = gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var points = new GameObject("Life Interaction Points");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(points, gameObject.scene);
        for (int i = 0; i < Furniture.Length; i++)
        {
            var furniture = transforms.FirstOrDefault(t => t.name == Furniture[i]);
            if (!furniture || !TryFindPoint(furniture, filter, out var position))
            { Debug.LogWarning("Life: нет достижимого подхода к " + Furniture[i]); continue; }
            var point = new GameObject(Actions[i] + " Interaction").transform;
            point.SetParent(points.transform);
            point.position = position;
            var action = gameObject.AddComponent<NeedAction>();
            action.Configure(Actions[i], Needs[i], point);
        }
        Ensure<NPCMovement>();
        Ensure<UtilityAI>();
        Ensure<ControlModeManager>();
        Ensure<ManualInteraction>();
        Ensure<LifeAudio>();
        Ensure<LifeHUD>();
    }
    bool TryFindPoint(Transform furniture, NavMeshQueryFilter filter, out Vector3 position)
    {
        position = default;
        var renderer = furniture.GetComponent<Renderer>();
        if (!renderer || !NavMesh.SamplePosition(transform.position, out var start, .6f, filter)) return false;
        var bounds = renderer.bounds;
        float best = float.PositiveInfinity;
        for (int ring = 0; ring < 3; ring++)
        for (int i = 0; i < 32; i++)
        {
            float a = i * Mathf.PI * 2 / 32;
            var p = bounds.center + new Vector3(Mathf.Cos(a) * (bounds.extents.x + .45f + ring * .25f), 0,
                Mathf.Sin(a) * (bounds.extents.z + .45f + ring * .25f));
            p.y = start.position.y;
            if (!NavMesh.SamplePosition(p, out var hit, .28f, filter) || Mathf.Abs(hit.position.y-start.position.y) > .4f) continue;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, hit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            float distance = Vector3.Distance(hit.position, bounds.ClosestPoint(hit.position));
            if (distance > 1f) continue;
            float score = distance + Vector3.Distance(start.position, hit.position) * .02f;
            if (score < best) { best = score; position = hit.position; }
        }
        return !float.IsPositiveInfinity(best);
    }
}
