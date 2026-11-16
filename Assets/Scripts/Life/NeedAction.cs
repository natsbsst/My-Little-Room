using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class NeedAction : MonoBehaviour, IUtilityAction
{
    [SerializeField] string actionName;
    [SerializeField] NeedType needType;
    [SerializeField] Transform target;
    [SerializeField] float restoreAmount = 40;
    [SerializeField] float actionDuration = 3;
    public string ActionName => actionName;
    public Transform InteractionPoint => target;
    public NeedType AffectedNeed => needType;
    public float RecoveryAmount => restoreAmount;
    public float Duration => actionDuration;
    public bool IsPerforming { get; private set; }
    public float Progress { get; private set; }
    public void Configure(string name, NeedType type, Transform point)
    { actionName = name; needType = type; target = point; restoreAmount = type == NeedType.Sleep ? 90f : 40f; }
    public bool IsAvailable()
    {
        if (!enabled || !target || !target.gameObject.activeInHierarchy) return false;
        var agent = GetComponent<NavMeshAgent>();
        if (!agent) return false;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas };
        var path = new NavMeshPath();
        return NavMesh.SamplePosition(transform.position, out var start, .6f, filter) &&
            NavMesh.SamplePosition(target.position, out var end, .3f, filter) &&
            NavMesh.CalculatePath(start.position, end.position, filter, path) && path.status == NavMeshPathStatus.PathComplete;
    }
    public float GetUtility(INeedReader needs)
    {
        float value = needs.GetValue(needType);
        float score = 100 - value;
        var npc = needs as NPCNeeds;
        if (needType == NeedType.Sleep && npc)
        {
            float hour = npc.CurrentHour;
            if (hour >= 21 || hour < 5) score *= 1.35f;
            else if (hour < 19 && value > 15) score *= .35f;
        }
        return score;
    }
    public IEnumerator Execute(NPCNeeds needs, NPCMovement movement)
    {
        if (!IsAvailable()) yield break;
        yield return movement.MoveTo(target);
        if (!target || Vector3.ProjectOnPlane(transform.position - target.position, Vector3.up).magnitude > .8f) yield break;
        IsPerforming = true;
        Progress = 0;
        GetComponent<LifeAudio>()?.PlayAction(actionName);
        try
        {
            for (float elapsed = 0; elapsed < actionDuration; elapsed += Time.deltaTime)
            {
                Progress = Mathf.Clamp01(elapsed / actionDuration);
                yield return null;
            }
            needs.ChangeNeed(needType, restoreAmount);
        }
        finally { IsPerforming = false; Progress = 0; }
    }
    public void Cancel() { IsPerforming = false; Progress = 0; }
}
