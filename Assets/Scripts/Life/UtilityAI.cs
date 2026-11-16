
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Две стратегии выбора действий Utility AI.
public enum UtilitySelectionStrategy
{
    FirstOnTie,
    LastOnTie
}

[RequireComponent(typeof(NPCNeeds))]
[RequireComponent(typeof(NPCMovement))]
public class UtilityAI : MonoBehaviour
{
    [Header("AI Settings")]
    [SerializeField, Range(0.25f, 1f)]
    private float decisionInterval = 0.5f;

    [Header("AI Strategy")]
    [SerializeField]
    private UtilitySelectionStrategy selectionStrategy =
        UtilitySelectionStrategy.FirstOnTie;

    private NPCNeeds needs;
    private NPCMovement movement;



    private readonly List<IUtilityAction> actions =
        new List<IUtilityAction>();

    // Стратегия выбора первого действия при равенстве.
    private readonly ActionSelector selector =
        new ActionSelector();

    // Стратегия выбора последнего действия при равенстве.
    private readonly LastOnTieSelector lastOnTieSelector =
        new LastOnTieSelector();

    private Coroutine aiRoutine;

    public string CurrentAction { get; private set; } =
        "Ожидание";

    public UtilitySelectionStrategy CurrentStrategy =>
        selectionStrategy;

    // Изменение стратегии выбора действий.
    public void SetStrategy(UtilitySelectionStrategy strategy)
    {
        selectionStrategy = strategy;

        Debug.Log(
            "AI strategy changed to: " + selectionStrategy
        );
    }

    private void Awake()
    {
        needs = GetComponent<NPCNeeds>();
        movement = GetComponent<NPCMovement>();

        // Находим действия персонажа.
        NeedAction[] foundActions =
            GetComponents<NeedAction>();

        foreach (NeedAction action in foundActions)
        {
            actions.Add(action);
        }
    }

    public bool StartAutonomy()
    {
        actions.Clear();
        foreach (var action in GetComponents<NeedAction>()) actions.Add(action);
        if (aiRoutine != null)
            return true;

        if (actions.Count == 0)
        {
            Debug.LogError("No AI actions configured!");
            return false;
        }

        Debug.Log(
            "AI started with strategy: " +
            selectionStrategy
        );

        aiRoutine = StartCoroutine(AILoop());

        return true;
    }

    public void StopAutonomy()
    {
        if (aiRoutine != null)
        {
            StopCoroutine(aiRoutine);
            aiRoutine = null;
        }

        movement.StopMovement();
        foreach (var action in GetComponents<NeedAction>()) action.Cancel();

        CurrentAction = "Ожидание";
    }

    private IEnumerator AILoop()
    {
        // Ждём инициализации NavMeshAgent.
        yield return null;

        while (true)
        {
            // =====================================
            // ИЗМЕРЕНИЕ ВРЕМЕНИ ВЫБОРА ДЕЙСТВИЯ
            // =====================================

            System.Diagnostics.Stopwatch stopwatch =
                System.Diagnostics.Stopwatch.StartNew();

            IUtilityAction bestAction;

            // Выбираем стратегию Utility AI.
            if (selectionStrategy ==
                UtilitySelectionStrategy.LastOnTie)
            {
                bestAction =
                    lastOnTieSelector.SelectBestAction(
                        actions, needs
                    );
            }
            else
            {
                bestAction =
                    selector.SelectBestAction(
                        actions, needs
                    );
            }

            // Получаем полезность выбранного действия.
            float utility = 0f;

            if (bestAction != null)
            {
                utility = bestAction.GetUtility(needs);
            }

            // Останавливаем измерение времени.
            stopwatch.Stop();

            double decisionTimeMs =
                stopwatch.Elapsed.TotalMilliseconds;

            // =====================================
            // ВЫПОЛНЕНИЕ ВЫБРАННОГО ДЕЙСТВИЯ
            // =====================================

            if (bestAction != null)
            {
                CurrentAction = bestAction.ActionName;

                Debug.Log(
                    "AI selected: " +
                    bestAction.ActionName +
                    " | Utility: " +
                    utility.ToString("F1") +
                    " | Strategy: " +
                    selectionStrategy +
                    " | Decision Time: " +
                    decisionTimeMs.ToString("F3") +
                    " ms"
                );

                // Выполняем выбранное действие.
                yield return bestAction.Execute(
                    needs, movement
                );

                CurrentAction = "Ожидание";
            }
            else
            {
                Debug.LogWarning(
                    "No available actions for NPC"
                );
            }

            yield return new WaitForSeconds(
                decisionInterval
            );
        }
    }
}
