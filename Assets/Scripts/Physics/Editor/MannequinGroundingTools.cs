using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

public static class MannequinGroundingTools
{
    [MenuItem("My Little Room/Physics/Align feet with floor %&h")]
    public static void Align()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Остановите Play Mode.");
        var actor = UnityEngine.Object.FindFirstObjectByType<MannequinWalk>();
        var capsule = actor.GetComponent<CharacterController>();
        var agent = actor.GetComponent<UnityEngine.AI.NavMeshAgent>();
        Undo.RecordObjects(new Object[] {actor.transform, capsule, agent}, "Убрать зазор стоп над полом");
        capsule.center = new Vector3(capsule.center.x, capsule.height * 0.5f + capsule.skinWidth, capsule.center.z);
        agent.baseOffset = 0f;
        actor.transform.position = new Vector3(actor.transform.position.x, 0f, actor.transform.position.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(actor.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(capsule);
        PrefabUtility.RecordPrefabInstancePropertyModifications(agent);
        EditorSceneManager.MarkSceneDirty(actor.gameObject.scene);
        EditorSceneManager.SaveScene(actor.gameObject.scene);
        Measure();
    }
    [MenuItem("My Little Room/Physics/Measure foot contact %&m")]
    public static void Measure()
    {
        var actor = UnityEngine.Object.FindFirstObjectByType<MannequinWalk>();
        var capsule = actor.GetComponent<CharacterController>();
        float sole = SoleHeight(actor.transform);
        var origin = actor.transform.position + Vector3.up * 0.3f;
        var hits = Physics.RaycastAll(origin, Vector3.down, 2f).Where(h=>!h.collider.transform.IsChildOf(actor.transform)).OrderBy(h=>h.distance).ToArray();
        float floor = hits.Length > 0 ? hits[0].point.y : float.NaN;
        Debug.Log($"GROUND_CONTACT play={Application.isPlaying} rootY={actor.transform.position.y:F6} soleY={sole:F6} floorY={floor:F6} gap={sole-floor:F6} capsuleCenter={capsule.center.y:F6} height={capsule.height:F6} grounded={capsule.isGrounded}");
    }

    // Измеряем реальные вершины деформированной модели, а не запас в renderer.bounds.
    public static float SoleHeight(Transform actor)
    {
        float minimum = float.PositiveInfinity;
        var baked = new Mesh();
        foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            renderer.BakeMesh(baked);
            foreach (Vector3 vertex in baked.vertices)
                minimum = Mathf.Min(minimum, renderer.transform.TransformPoint(vertex).y);
        }
        UnityEngine.Object.DestroyImmediate(baked);
        return minimum;
    }
}
