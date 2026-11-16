using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class ApartmentPhysicsSetup
{
    const string NavPath = "Assets/Scripts/Physics/ApartmentNavigation.asset";
    static readonly Regex Furniture = new Regex(@"^(Bed_(Frame|Headboard|Mattress)$|Bedroom_Wardrobe_Carcass$|Desk_(Top|Chair_Seat|Chair_Back)$|Computer_Tower$|Dining_Table_Top$|Dining_Chair_(Seat|Back)_|Kitchen_(Base_Cabinet_|Counter_|Upper_Cabinet_|Open_Shelf_)|Fridge_Body$|Living_(Sofa_(Base|Back|Arm_)|Armchair_(Base|Back|Arm_|Seat)|Coffee_Table$|Side_Table$|Console$|Fireplace_(Back|Column_|Hearth|Mantel)|Bookcase_(Side_|Shelf_))|Bathroom_(Freestanding_Tub$|Vanity_(Cabinet|Counter)$|Sink_(Bottom|Side_|Rim_)|Toilet_(Base|Bowl|Cistern)$|Shower_(Glass_West|Tray)$|Tall_Shelf_(Side_|Level_)|Wall_Cabinet_(Side_|Shelf_))|Panorama_Bench_(Oak_Base|Soft_Seat)$|Pouf_Base$|Plant_Pot$|Living_Ficus_Pot$|Bathroom_Plant_Pot$)");

    [MenuItem("My Little Room/Physics/Configure colliders and bake NavMesh")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Остановите Play Mode.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity") throw new InvalidOperationException("Откройте SampleScene.");
        var roots = scene.GetRootGameObjects();
        var apartment = roots.Single(g => g.name == "MyLittleRoom_Apartment_URP");
        var character = roots.Single(g => g.name == "MyLittleRoom_Mannequin");
        var transforms = apartment.GetComponentsInChildren<Transform>(true);
        Transform Find(string name) => transforms.Single(t=>t.name == name);
        Renderer Render(string name) => Find(name).GetComponent<Renderer>();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Физика квартиры и навигация");

        foreach (var t in transforms)
        {
            if (t.name.StartsWith("Floor_") || Furniture.IsMatch(t.name))
            {
                AddMeshBox(t);
                if (!t.name.StartsWith("Floor_")) MarkNotWalkable(t);
            }
            if (t.name.StartsWith("Wall_Exterior_") && t.name != "Wall_Exterior_West" && t.name != "Wall_Exterior_Living_East")
                AddMeshBox(t);
        }
        // Проёмы вырезаются в физических стенах, а не закрываются общим AABB.
        SplitWall(Find("Partition_BedroomBath_Continuous"), new[] { Render("BedroomBathroom_Door").bounds }, 2);
        SplitWall(Find("Partition_BedroomLiving_Continuous"), new[] { Render("KitchenBedroom_Door").bounds }, 0);
        SplitWall(Find("Wall_Exterior_Living_East"), new[] { Render("Entrance_Closed_Door_095x210").bounds }, 2);
        SplitWall(Find("Wall_Exterior_West"), new[] { Render("Bedroom_Panorama_Uninterrupted_Glass").bounds, Render("Living_Panorama_Clear_Glass").bounds }, 2);
        foreach (string name in new[] { "Entrance_Closed_Door_095x210", "Bedroom_Panorama_Uninterrupted_Glass", "Living_Panorama_Clear_Glass" })
            AddMeshBox(Find(name));

        foreach (var door in apartment.GetComponentsInChildren<DoorController>(true))
        {
            string leaf = door.name == "BedroomBathroom_Hinge" ? "BedroomBathroom_Door" : "KitchenBedroom_Door";
            var box = AddMeshBox(Find(leaf));
            Undo.RecordObject(door, "Физическое дверное полотно");
            door.doorColliders = new Collider[] { box };
            PrefabUtility.RecordPrefabInstancePropertyModifications(door);
            // Полы импортированы раздельно: физическая опора внутри существующего порога.
            var threshold = GetOrAdd<BoxCollider>(door.doorwayCenter.gameObject);
            Undo.RecordObject(threshold, "Опора порога");
            threshold.center = new Vector3(0, -0.1f, 0);
            threshold.size = new Vector3(door.doorwayHalfWidth * 2, 0.2f, 0.36f);
            threshold.isTrigger = false;
            // Модификатор петель уже исключает только полотна из NavMesh, не стены.
        }

        var cc = GetOrAdd<CharacterController>(character);
        Undo.RecordObject(cc, "Размеры капсулы персонажа");
        cc.height = 1.88f; cc.radius = 0.25f;
        cc.skinWidth = 0.025f; cc.stepOffset = 0.15f; cc.slopeLimit = 45f; cc.minMoveDistance = 0;
        // Подошвы модели находятся на Y=0. Учитываем контактный зазор капсулы,
        // чтобы grounded-контроллер не держал видимую модель над полом.
        cc.center = new Vector3(0, cc.height * 0.5f + cc.skinWidth, 0);
        GetOrAdd<MannequinWalk>(character);
        int agentType = ConfigureAgentType();
        var agent = GetOrAdd<NavMeshAgent>(character);
        Undo.RecordObject(agent, "Планировщик пути персонажа");
        agent.enabled = false; agent.agentTypeID = agentType;
        agent.radius = 0.3f; agent.height = 1.88f; agent.baseOffset = 0f;
        agent.speed = 2.5f; agent.angularSpeed = 140; agent.acceleration = 12;
        agent.stoppingDistance = 0.15f; agent.autoBraking = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.updatePosition = false; agent.updateRotation = false;
        Undo.RecordObject(character.transform, "Положение ног относительно пола");
        character.transform.position = new Vector3(character.transform.position.x, 0f, character.transform.position.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(character.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(cc);
        PrefabUtility.RecordPrefabInstancePropertyModifications(agent);

        var surface = GetOrAdd<NavMeshSurface>(apartment);
        Undo.RecordObject(surface, "Навигация по коллайдерам квартиры");
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.agentTypeID = agentType;
        surface.overrideVoxelSize = true; surface.voxelSize = 0.05f;
        surface.overrideTileSize = true; surface.tileSize = 128;
        Physics.SyncTransforms();
        surface.BuildNavMesh();
        if (!surface.navMeshData) throw new InvalidOperationException("NavMesh не построен.");
        var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavPath);
        if (existing && existing != surface.navMeshData)
        {
            var generated = surface.navMeshData;
            surface.RemoveData();
            EditorUtility.CopySerialized(generated, existing);
            surface.navMeshData = existing;
            UnityEngine.Object.DestroyImmediate(generated);
            surface.AddData();
            EditorUtility.SetDirty(existing);
        }
        else if (!existing) AssetDatabase.CreateAsset(surface.navMeshData, NavPath);
        PrefabUtility.RecordPrefabInstancePropertyModifications(surface);
        EditorUtility.SetDirty(surface);
        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        var colliders = apartment.GetComponentsInChildren<Collider>(true);
        File.WriteAllLines("Library/ApartmentColliderInventory.txt", colliders.Select(c=>$"{c.name}: {c.GetType().Name}; center={c.bounds.center:F3}; size={c.bounds.size:F3}"));
        Debug.Log($"PHYSICS_SETUP_COMPLETE: colliders={colliders.Length}, agentType={agentType}, triangles={NavMesh.CalculateTriangulation().indices.Length / 3}");
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        // Unity использует собственную проверку null для отсутствующих компонентов.
        var existing = go.GetComponent<T>();
        return existing ? existing : Undo.AddComponent<T>(go);
    }

    static BoxCollider AddMeshBox(Transform t)
    {
        var mesh = t.GetComponent<MeshFilter>();
        if (!mesh || !mesh.sharedMesh) throw new InvalidOperationException("Нет MeshFilter: " + t.name);
        var box = GetOrAdd<BoxCollider>(t.gameObject);
        Undo.RecordObject(box, "Коллайдер " + t.name);
        box.center = mesh.sharedMesh.bounds.center;
        var size = mesh.sharedMesh.bounds.size;
        // Очень тонкое стекло получает минимальную физическую толщину 4 см.
        box.size = Vector3.Max(size, Vector3.one * 0.04f);
        box.isTrigger = false; box.enabled = true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(box);
        return box;
    }

    static void MarkNotWalkable(Transform t)
    {
        var modifier = GetOrAdd<NavMeshModifier>(t.gameObject);
        Undo.RecordObject(modifier, "Не ходить по мебели");
        modifier.overrideArea = true; modifier.area = 1;
        PrefabUtility.RecordPrefabInstancePropertyModifications(modifier);
    }

    static void SplitWall(Transform wall, Bounds[] openings, int widthAxis)
    {
        Bounds bounds = wall.GetComponent<Renderer>().bounds;
        var sections = new List<Bounds>();
        float cursor = bounds.min[widthAxis];
        foreach (Bounds hole in openings.OrderBy(b=>b.min[widthAxis]))
        {
            float left = hole.min[widthAxis], right = hole.max[widthAxis];
            Section(cursor, left, bounds.min.y, bounds.max.y);
            Section(left, right, bounds.min.y, Mathf.Max(bounds.min.y, hole.min.y));
            Section(left, right, hole.max.y, bounds.max.y);
            cursor = right;
        }
        Section(cursor, bounds.max[widthAxis], bounds.min.y, bounds.max.y);
        var boxes = wall.GetComponents<BoxCollider>();
        if (boxes.Length > sections.Count) throw new InvalidOperationException("Неожиданные коллайдеры стены: " + wall.name);
        for (int i = 0; i < sections.Count; i++)
        {
            var box = i < boxes.Length ? boxes[i] : Undo.AddComponent<BoxCollider>(wall.gameObject);
            Undo.RecordObject(box, "Сегмент стены с проёмом");
            Bounds local = new Bounds(wall.InverseTransformPoint(sections[i].center), Vector3.zero);
            for (int k = 0; k < 8; k++)
            {
                Vector3 v = sections[i].center + Vector3.Scale(sections[i].extents,
                    new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                local.Encapsulate(wall.InverseTransformPoint(v));
            }
            box.center = local.center; box.size = local.size; box.isTrigger = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(box);
        }
        void Section(float from, float to, float bottom, float top)
        {
            if (to - from < 0.001f || top - bottom < 0.001f) return;
            Vector3 min = bounds.min, max = bounds.max;
            min[widthAxis] = from; max[widthAxis] = to; min.y = bottom; max.y = top;
            var b = new Bounds(); b.SetMinMax(min, max); sections.Add(b);
        }
    }

    static int ConfigureAgentType()
    {
        // Используем тот же сериализованный объект, что окно Navigation пакета Unity.
        var settingsObject = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("NavMeshProjectSettings"));
        var names = settingsObject.FindProperty("m_SettingNames");
        int index = -1;
        for (int i = 0; i < names.arraySize; i++) if (names.GetArrayElementAtIndex(i).stringValue == "MyLittleRoom Character") index = i;
        if (index < 0)
        {
            NavMesh.CreateSettings();
            settingsObject.Update();
            names = settingsObject.FindProperty("m_SettingNames");
            index = names.arraySize - 1;
            names.GetArrayElementAtIndex(index).stringValue = "MyLittleRoom Character";
        }
        var entry = settingsObject.FindProperty("m_Settings").GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("agentRadius").floatValue = 0.3f;
        entry.FindPropertyRelative("agentHeight").floatValue = 1.88f;
        entry.FindPropertyRelative("agentClimb").floatValue = 0.15f;
        entry.FindPropertyRelative("agentSlope").floatValue = 45f;
        int id = entry.FindPropertyRelative("agentTypeID").intValue;
        settingsObject.ApplyModifiedProperties();
        return id;
    }
}
