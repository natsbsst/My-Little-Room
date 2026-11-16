
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NPCNeeds))]
[RequireComponent(typeof(ControlModeManager))]
public class ManualInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField]
    private float interactionRadius = 1.5f;

    private NPCNeeds needs;
    private ControlModeManager controlMode;
    private NeedAction[] actions;

    private bool isBusy = false;
    public bool IsBusy => isBusy;
    public string CurrentActionName { get; private set; }

    public float ActionProgress { get; private set; }
    public NeedAction GetAvailableAction()
    {
        if (controlMode == null ||
            controlMode.IsAutoMode ||
            isBusy)
        {
            return null;
        }

        return FindNearestAction();
    }


    private void Awake()
    {
        needs = GetComponent<NPCNeeds>();
        controlMode = GetComponent<ControlModeManager>();
        actions = GetComponents<NeedAction>();
    }

    private void Update()
    {
        if (Time.timeScale <= 0 || !Application.isFocused || controlMode.IsAutoMode || isBusy)
            return;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryPerform();
        }
    }

    public bool TryPerform()
    {
        if (Time.timeScale <= 0 || controlMode.IsAutoMode || isBusy) return false;
        var action = FindNearestAction();
        if (!action) return false;
        StartCoroutine(PerformAction(action));
        return true;
    }

    void OnDisable() { StopAllCoroutines(); isBusy = false; ActionProgress = 0; CurrentActionName = null; }

    private NeedAction FindNearestAction()
    {
        NeedAction nearest = null;
        float bestDistance = interactionRadius * interactionRadius;

        foreach (NeedAction action in actions)
        {
            if (action == null || !action.enabled ||
                action.InteractionPoint == null ||
                !action.InteractionPoint.gameObject.activeInHierarchy)
                continue;

            Vector3 difference =
                action.InteractionPoint.position - transform.position;

            difference.y = 0f;

            float distance = difference.sqrMagnitude;

            if (distance <= bestDistance)
            {
                nearest = action;
                bestDistance = distance;
            }
        }

        return nearest;
    }

    private bool IsNear(NeedAction action)
    {
        if (action == null || action.InteractionPoint == null)
            return false;

        Vector3 difference =
            action.InteractionPoint.position - transform.position;

        difference.y = 0f;

        return difference.sqrMagnitude <=
            interactionRadius * interactionRadius;
    }

    private IEnumerator PerformAction(NeedAction action)
    {
        isBusy = true;
        CurrentActionName = action.ActionName;
        ActionProgress = 0f;
        Debug.Log("MANUAL: " + action.ActionName);

        float elapsed = 0f;
        float duration = Mathf.Max(0f, action.Duration);

        while (elapsed < duration)
        {
            if (controlMode.IsAutoMode || !IsNear(action))
            {
                Debug.Log("Действие отменено");
                isBusy = false;
                ActionProgress = 0;
                CurrentActionName = null;
                yield break;
            }

            elapsed += Time.deltaTime;

            ActionProgress = Mathf.Clamp01(
                elapsed / Mathf.Max(duration, 0.001f)
            );

            yield return null;
        }

        if (!controlMode.IsAutoMode && IsNear(action))
        {
            needs.ChangeNeed(
                action.AffectedNeed,
                action.RecoveryAmount
            );

            Debug.Log(
                "MANUAL: " + action.ActionName +
                " completed! +" + action.RecoveryAmount
            );
        }

        isBusy = false;
        ActionProgress = 0;
        CurrentActionName = null;
    }
}
