using System.Collections;
using UnityEngine;

public class NPCMovement : MonoBehaviour
{
    public IEnumerator MoveTo(Transform target)
    {
        var motor = GetComponent<MannequinWalk>();
        if (!target || !motor.SetDestination(target.position)) yield break;
        float elapsed = 0;
        float stalled = 0;
        Vector3 previous = transform.position;
        while (target && motor.IsNavigating && elapsed < 40f)
        {
            elapsed += Time.deltaTime;
            stalled = (transform.position - previous).sqrMagnitude < .00001f ? stalled + Time.deltaTime : 0;
            previous = transform.position;
            if (stalled > 1.5f)
            {
                motor.SetDestination(target.position);
                stalled = 0;
            }
            yield return null;
        }
        motor.StopNavigation();
    }
    public void StopMovement() => GetComponent<MannequinWalk>()?.StopNavigation();
}
