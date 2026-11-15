using UnityEngine;

public sealed class ApartmentTimeOfDaySettings : ScriptableObject
{
    [SerializeField] Material daySky;

    public Material DaySky => daySky;
}
