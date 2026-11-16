using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

public static class ApartmentDoorSetup
{
    [MenuItem("My Little Room/Doors/Configure existing doors")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Остановите Play Mode.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity") throw new InvalidOperationException("Откройте SampleScene.");
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        Transform Find(string name) => all.Single(t => t.name == name);
        var character = Find("MyLittleRoom_Mannequin");
        // Проверяем всё до внесения изменений. Используем существующие петли Blender.
        var hinges = new[] { Find("BedroomBathroom_Hinge"), Find("KitchenBedroom_Hinge") };
        var leaves = new[] { Find("BedroomBathroom_Door"), Find("KitchenBedroom_Door") };
        for (int i = 0; i < hinges.Length; i++)
            if (!leaves[i].IsChildOf(hinges[i])) throw new InvalidOperationException("Неожиданная иерархия двери.");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Настроить автоматические двери");
        for (int i = 0; i < hinges.Length; i++)
        {
            var hinge = hinges[i];
            var controller = hinge.GetComponent<DoorController>();
            if (controller) { Undo.RecordObject(controller, "Направление двери"); controller.openAngle = i == 0 ? 90f : -90f; PrefabUtility.RecordPrefabInstancePropertyModifications(controller); continue; }
            Vector3 half = Vector3.ProjectOnPlane(leaves[i].position - hinge.position, Vector3.up);
            if (half.magnitude < 0.1f) throw new InvalidOperationException("Проверьте позицию петель.");
            var anchor = new GameObject(hinge.name + "_Doorway");
            Undo.RegisterCreatedObjectUndo(anchor, "Центр проёма");
            anchor.transform.SetParent(hinge.parent, false);
            anchor.transform.position = hinge.position + half;
            anchor.transform.rotation = Quaternion.LookRotation(Vector3.Cross(half.normalized, Vector3.up), Vector3.up);
            controller = Undo.AddComponent<DoorController>(hinge.gameObject);
            controller.doorHinge = hinge;
            controller.targetCharacter = character;
            controller.doorwayCenter = anchor.transform;
            controller.doorwayHalfWidth = half.magnitude;
            // Обе двери открываются от спальни в соседние помещения.
            controller.openAngle = i == 0 ? 90f : -90f;
            controller.doorColliders = hinge.GetComponentsInChildren<Collider>(true);
            // Исключаем только движущиеся полотна из последующих запеканий NavMesh.
            var modifier = hinge.GetComponent<NavMeshModifier>();
            if (!modifier) modifier = Undo.AddComponent<NavMeshModifier>(hinge.gameObject);
            Undo.RecordObject(modifier, "Не запекать дверное полотно");
            modifier.ignoreFromBuild = true;
            foreach (var obstacle in hinge.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true))
            {
                Undo.RecordObject(obstacle, "Не перекрывать маршрут дверью");
                obstacle.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(obstacle);
            }
            EditorUtility.SetDirty(controller);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            PrefabUtility.RecordPrefabInstancePropertyModifications(modifier);
            Debug.Log($"DOOR_SETUP {hinge.name}: hinge={hinge.position:F3}, doorway={anchor.transform.position:F3}, halfWidth={half.magnitude:F3}, colliders={controller.doorColliders.Length}");
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("DOOR_SETUP_COMPLETE: две межкомнатные двери настроены и сцена сохранена.");
    }
}
