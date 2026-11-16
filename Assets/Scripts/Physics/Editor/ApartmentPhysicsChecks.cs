using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Интеграционные проверки настоящего персонажа в Play Mode. Сцена не сохраняется.
[InitializeOnLoad]
public static class ApartmentPhysicsChecks
{
    const string RunKey = "MyLittleRoom.PhysicsChecks";
    static readonly Queue<(Action start, double timeout, Func<bool> done, Action verify)> steps = new();
    static readonly List<string> log = new();
    static double deadline;
    static bool started;
    static Action verify;
    static Func<bool> done;
    static MannequinWalk motor;
    static CharacterController body;
    static Transform target;
    static DoorController[] doors;
    static int passed;
    static float minimumSole, maximumSole;
    static int soleSamples;
    static double nextSoleSample;

    static ApartmentPhysicsChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(RunKey, false))
            {
                started = false; verify = null; done = null;
                deadline = EditorApplication.timeSinceStartup + 1;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                SessionState.SetBool(RunKey, false);
            }
        };
    }

    [MenuItem("My Little Room/Physics/Run collision and navigation checks")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(RunKey, true);
        EditorApplication.isPlaying = true;
    }

    static void Check(bool valid, string message)
    {
        if (!valid) throw new Exception(message);
        passed++;
        string line = "PHYSICS_CHECK_PASS: " + message;
        log.Add(line); Debug.Log(line);
    }

    static void Place(Vector3 position)
    {
        motor.StopNavigation();
        body.enabled = false;
        target.position = position;
        body.enabled = true;
        Physics.SyncTransforms();
    }

    static void Prepare()
    {
        log.Clear(); steps.Clear(); passed = 0;
        minimumSole = float.PositiveInfinity; maximumSole = float.NegativeInfinity;
        soleSamples = 0; nextSoleSample = 0;
        motor = UnityEngine.Object.FindFirstObjectByType<MannequinWalk>();
        target = motor.transform; body = motor.GetComponent<CharacterController>();
        doors = UnityEngine.Object.FindObjectsByType<DoorController>();
        Check(body && doors.Length == 2, "Существующий персонаж имеет CharacterController; найдены две межкомнатные двери");
        motor.enabled = false;
        var all = UnityEngine.Object.FindObjectsByType<Transform>();
        Transform Find(string name) => all.Single(t=>t.name == name);
        Bounds BoundsOf(string name) => Find(name).GetComponent<Renderer>().bounds;
        void Add(Action action, double seconds, Action assertion, Func<bool> until = null) => steps.Enqueue((action, seconds, until, assertion));

        void Block(string name, Vector3 direction)
        {
            Bounds bounds = BoundsOf(name);
            Vector3 start = bounds.center - Vector3.Scale(bounds.extents, direction) - direction * 0.65f;
            start.y = 0.09f;
            Place(start);
            for (int i = 0; i < 80; i++) body.Move(direction * 0.05f + Vector3.down * 0.01f);
            float near = Vector3.Dot(bounds.center - Vector3.Scale(bounds.extents, direction), direction);
            Check(Vector3.Dot(target.position, direction) < near - 0.15f && target.position.y > -0.1f,
                name + ": CharacterController блокирует проход");
        }

        Add(() =>
        {
            Block("Wall_Exterior_South", Vector3.forward);
            Block("Bed_Frame", Vector3.right);
            Block("Living_Sofa_Base", Vector3.right);
            Block("Fridge_Body", Vector3.back);
            Block("Entrance_Closed_Door_095x210", Vector3.left);
            Block("Bedroom_Panorama_Uninterrupted_Glass", Vector3.right);
            Block("Living_Panorama_Clear_Glass", Vector3.right);
            Vector3 floor = BoundsOf("Floor_Bedroom_7x7").center;
            floor.y = 1;
            Place(floor);
            for (int i = 0; i < 80; i++) body.Move(Vector3.down * 0.05f);
            Check(Mathf.Abs(target.position.y) < 0.005f && body.isGrounded, "Пол удерживает корень у подошв без зависания (допуск 5 мм)");
            Check(Mathf.Abs(MannequinGroundingTools.SoleHeight(target)) < 0.01f, "Реальные вершины стоп стоят у пола (допуск 1 см)");
            Check(doors.All(d=>d.doorColliders.Length > 0 && d.doorColliders.All(c=>c.enabled && !c.isTrigger)), "Оба дверных полотна имеют включённые твёрдые Collider");
        }, 0.2, () => { });

        foreach (var item in doors)
        {
            var door = item;
            Vector3 center = door.doorwayCenter.position;
            Vector3 normal = door.doorwayCenter.forward;
            Add(() => Place(new Vector3(41.64f, 0.09f, 26.98f)), 1.1, () => Check(door.OpenFraction == 0, door.name + ": закрыта перед физическим тестом"));
            Add(() =>
            {
                door.enabled = false;
                Place(center + normal * 0.8f + Vector3.up * 0.09f);
                for (int i = 0; i < 50; i++) body.Move(-normal * 0.05f);
                Check(Vector3.Dot(target.position - center, normal) > 0.15f, door.name + ": закрытое полотно блокирует прямой Move");
                Place(center + normal * 1.45f + Vector3.up * 0.09f);
                door.enabled = true;
                motor.enabled = true;
                target.rotation = Quaternion.LookRotation(-normal);
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.W));
            }, 2.3, () =>
            {
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                motor.enabled = false;
                Check(Vector3.Dot(target.position - center, normal) < -0.4f,
                    door.name + ": ручное W проходит после открытия, коллайдер не выключается");
                Check(door.doorColliders.All(c=>c.enabled), door.name + ": Collider активен после прохода");
            });
            Add(() => Place(center + Vector3.up * 0.09f), 1.1,
                () => Check(door.OpenFraction > 0.98f, door.name + ": не закрывается на персонаже в проёме"));
        }

        var agent = motor.GetComponent<NavMeshAgent>();
        var filter = new NavMeshQueryFilter {agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas};
        Vector3 Room(string name)
        {
            Vector3 p = BoundsOf(name).center; p.y = 0;
            if (!NavMesh.SamplePosition(p, out var hit, 1.5f, filter)) throw new Exception("Нет NavMesh в комнате: " + name);
            return hit.position;
        }
        Vector3 bedroom = Room("Floor_Bedroom_7x7"), bath = Room("Floor_Bathroom_6x5"), living = Room("Floor_KitchenLiving_10x8");
        Add(() => { Place(bedroom + Vector3.up * 0.09f); motor.enabled = true; }, 0.3, () => { });
        foreach (var trip in new[] { (bath, "Спальня → ванная"), (bedroom, "Ванная → спальня"), (living, "Спальня → кухня-гостиная"), (bedroom, "Кухня-гостиная → спальня") })
        {
            var destination = trip.Item1; string label = trip.Item2;
            Add(() => Check(motor.SetDestination(destination), label + ": полный NavMesh-маршрут найден"), 25,
                () => {
                    float remaining = Vector3.ProjectOnPlane(target.position - destination, Vector3.up).magnitude;
                    Check(remaining < 0.45f, label + ": автономный проход завершён, distance=" + remaining.ToString("F3"));
                    Check(!motor.IsNavigating, label + ": состояние маршрута завершено");
                    Check(target.position.y > -0.05f && target.position.y < 0.3f, label + ": персонаж остаётся на полу");
                    Check(doors.All(d=>d.doorColliders.All(c=>c.enabled)), label + ": двери сохранили Collider");
                }, () => !motor.IsNavigating && Vector3.ProjectOnPlane(target.position - destination, Vector3.up).magnitude < 0.45f);
        }
        Add(() => { motor.StopNavigation(); }, 0.5, () =>
        {
            Check(!Find("Entrance_Closed_Door_095x210").GetComponentInParent<DoorController>(), "Входная дверь не имеет автоматического контроллера");
            var surface = Find("MyLittleRoom_Apartment_URP").GetComponent<Unity.AI.Navigation.NavMeshSurface>();
            Check(surface && surface.navMeshData, "NavMesh сохранён и подключён к сцене");
        });
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (started && motor && motor.enabled && motor.IsNavigating && body.isGrounded &&
            EditorApplication.timeSinceStartup >= nextSoleSample)
        {
            float sole = MannequinGroundingTools.SoleHeight(target);
            minimumSole = Mathf.Min(minimumSole, sole);
            maximumSole = Mathf.Max(maximumSole, sole);
            soleSamples++;
            nextSoleSample = EditorApplication.timeSinceStartup + 0.1;
        }
        if (EditorApplication.timeSinceStartup < deadline && (done == null || !done())) return;
        try
        {
            if (!started) { Prepare(); started = true; }
            verify?.Invoke(); verify = null; done = null;
            if (steps.Count == 0)
            {
                string contact = $"GROUND_WALK_SAMPLES: count={soleSamples}, minSoleY={minimumSole:F6}, maxSoleY={maximumSole:F6}";
                log.Add(contact); Debug.Log(contact);
                string result = $"PHYSICS_CHECK_COMPLETE: {passed} assertions passed.";
                log.Add(result); Debug.Log(result); Stop(); return;
            }
            var step = steps.Dequeue(); step.start();
            verify = step.verify; done = step.done;
            deadline = EditorApplication.timeSinceStartup + step.timeout;
        }
        catch (Exception e)
        {
            string result = "PHYSICS_CHECK_FAILED: " + e;
            log.Add(result); Debug.LogError(result); Stop();
        }
    }

    static void Stop()
    {
        if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        File.WriteAllLines("Library/ApartmentPhysicsChecks.txt", log);
        EditorApplication.update -= Tick;
        SessionState.SetBool(RunKey, false);
        EditorApplication.isPlaying = false;
    }
}
