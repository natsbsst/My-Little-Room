
using System.Collections.Generic;

public class ActionSelector
{
    public IUtilityAction SelectBestAction(
        IReadOnlyList<IUtilityAction> actions,
        INeedReader needs)
    {
        IUtilityAction bestAction = null;
        float bestScore = float.NegativeInfinity;

        foreach (IUtilityAction action in actions)
        {
            if (action == null || !action.IsAvailable())
                continue;

            float score = action.GetUtility(needs);

            if (score > bestScore)
            {
                bestScore = score;
                bestAction = action;
            }
        }

        return bestAction;
    }
}
