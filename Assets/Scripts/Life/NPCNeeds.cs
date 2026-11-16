
using UnityEngine;

public enum NeedType
{
    Satiety,
    Hygiene,
    Sleep,
    Work,
    Leisure
}

public interface INeedReader
{
    float GetValue(NeedType type);
}

public interface INeedWriter
{
    void ChangeNeed(NeedType type, float amount);
}

public class NPCNeeds : MonoBehaviour, INeedReader, INeedWriter
{
    [Header("Needs (0-100)")]
    [SerializeField, Range(0f, 100f)]
    private float satiety = 78f;

    [SerializeField, Range(0f, 100f)]
    private float hygiene = 92f;

    [SerializeField, Range(0f, 100f)]
    private float sleep = 100f;

    [SerializeField, Range(0f, 100f)]
    private float work = 85f;

    [SerializeField, Range(0f, 100f)]
    private float leisure = 82f;

    [Header("Decay Rates")]
    [Tooltip("Points per in-game hour")]
    [SerializeField] private float satietyDecay = 7f;
    [SerializeField] private float hygieneDecay = 2f;
    [SerializeField] private float sleepDecay = 4.6f;
    [SerializeField] private float workDecay = 3f;
    [SerializeField] private float leisureDecay = 2.8f;
    DayNightCycle clock;
    public float CurrentHour => clock ? clock.CurrentHour : 5f;
    void Start() { clock = FindFirstObjectByType<DayNightCycle>(); }

    private void Awake()
    {
        foreach (NeedType type in System.Enum.GetValues(typeof(NeedType)))
        {
            ChangeNeed(type, 0f);
        }
    }

    
    private void Update()
    {
        AdvanceHours(Time.deltaTime * 24f / DayNightCycle.CycleSeconds, CurrentHour);
    }

    public void AdvanceHours(float hours, float hour)
    {
        if (hours <= 0) return;
        ChangeNeed(NeedType.Satiety,
            -Mathf.Max(0f, satietyDecay) * hours);

        ChangeNeed(NeedType.Hygiene,
            -Mathf.Max(0f, hygieneDecay) * hours);

        ChangeNeed(NeedType.Sleep,
            -Mathf.Max(0f, sleepDecay) * hours * (hour >= 21f || hour < 5f ? 1.25f : 1f));

        ChangeNeed(NeedType.Work,
            -Mathf.Max(0f, workDecay) * hours * (hour >= 8f && hour < 18f ? 1f : .15f));

        ChangeNeed(NeedType.Leisure,
            -Mathf.Max(0f, leisureDecay) * hours * (hour >= 18f ? 1.4f : 1f));
    }


    public float GetValue(NeedType type)
    {
        switch (type)
        {
            case NeedType.Satiety: return satiety;
            case NeedType.Hygiene: return hygiene;
            case NeedType.Sleep: return sleep;
            case NeedType.Work: return work;
            case NeedType.Leisure: return leisure;
            default: return 0f;
        }
    }

    public void ChangeNeed(NeedType type, float amount)
    {
        float value = Mathf.Clamp(GetValue(type) + amount, 0f, 100f);

        switch (type)
        {
            case NeedType.Satiety: satiety = value; break;
            case NeedType.Hygiene: hygiene = value; break;
            case NeedType.Sleep: sleep = value; break;
            case NeedType.Work: work = value; break;
            case NeedType.Leisure: leisure = value; break;
        }
    }
}
