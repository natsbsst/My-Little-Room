using UnityEditor;

// Старый пункт меню теперь запускает проверки с учётом настоящих коллизий.
// Дверные тесты без физики не должны перемещать капсулу сквозь препятствия.
public static class ApartmentDoorChecks
{
    [MenuItem("My Little Room/Doors/Run Play Mode checks")]
    public static void Run() => ApartmentPhysicsChecks.Run();
}
