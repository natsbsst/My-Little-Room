using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class ThirdPersonMannequinCamera : MonoBehaviour
{
    const float Distance = 1.45f;
    const float CameraHeight = 1.98f;
    const float LookHeight = 1.18f;
    const float LookAhead = .22f;

    Transform target;
    Quaternion heading;
    Vector3 velocity;
    bool initialized;

    public void Follow(Transform actor)
    {
        target = actor;
        initialized = false;
        var camera = GetComponent<Camera>();
        camera.orthographic = false;
        camera.fieldOfView = 66f;
        camera.nearClipPlane = .04f;
        camera.clearFlags = CameraClearFlags.SolidColor;
    }

    void LateUpdate()
    {
        if (!target)
            return;

        float dt = Time.deltaTime;
        var forward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .01f)
            return;

        var desiredHeading = Quaternion.LookRotation(forward);
        bool snap = !initialized || (transform.position - target.position).sqrMagnitude > 36f;
        heading = snap ? desiredHeading : Quaternion.RotateTowards(heading, desiredHeading, 280f * dt);
        var behind = heading * Vector3.back;
        var desiredPosition = target.position + Vector3.up * CameraHeight + behind * Distance;

        if (snap)
        {
            velocity = Vector3.zero;
            transform.position = desiredPosition;
            initialized = true;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, .105f,
                Mathf.Infinity, dt);
        }

        var lookAt = target.position + Vector3.up * LookHeight - behind * LookAhead;
        transform.rotation = Quaternion.LookRotation(lookAt - transform.position, Vector3.up);
    }
}
