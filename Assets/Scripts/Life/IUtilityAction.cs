
using System.Collections;

public interface IUtilityAction
{
    string ActionName { get; }

    bool IsAvailable();

    float GetUtility(INeedReader needs);

    IEnumerator Execute(NPCNeeds needs, NPCMovement movement);
}
