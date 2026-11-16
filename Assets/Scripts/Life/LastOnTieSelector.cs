
using System.Collections.Generic;

public class LastOnTieSelector
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

            // При равной полезности выбираем
            // последнее действие в списке.
            if (score >= bestScore)
            {
                bestScore = score;
                bestAction = action;
            }
        }

        return bestAction;
    }
}
