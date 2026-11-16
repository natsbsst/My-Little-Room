using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ApartmentPhysicsAudit
{
    [Serializable] public class Item { public string name; public Vector3 position, size, min, max, localSize; public string parent; }
    [Serializable] public class Report { public Item[] items; public string[] components; }
    [MenuItem("My Little Room/Physics/Audit scene")]
    public static void Audit()
    {
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        var all = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var report = new Report {
            items = all.Where(t => t.GetComponent<Renderer>()).Select(t => {
                var r = t.GetComponent<Renderer>();
                var mesh = t.GetComponent<MeshFilter>();
                return new Item { name=t.name, position=t.position, size=r.bounds.size,
                    min=r.bounds.min, max=r.bounds.max, parent=t.parent ? t.parent.name : "",
                    localSize=mesh && mesh.sharedMesh ? mesh.sharedMesh.bounds.size : r.localBounds.size };
            }).OrderBy(i=>i.name).ToArray(),
            components = all.SelectMany(t=>t.GetComponents<Component>()).Where(c=>c &&
                (c is Collider || c is Rigidbody || c is UnityEngine.AI.NavMeshAgent || c is MonoBehaviour))
                .Select(c=>c.name+": "+c.GetType().FullName).ToArray()
        };
        File.WriteAllText("Library/ApartmentPhysicsAudit.json", JsonUtility.ToJson(report, true));
        Debug.Log("PHYSICS_AUDIT_COMPLETE: Library/ApartmentPhysicsAudit.json");
    }
}
