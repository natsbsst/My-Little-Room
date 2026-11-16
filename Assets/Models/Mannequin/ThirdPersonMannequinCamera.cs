using UnityEngine;
using UnityEngine.InputSystem;

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
    float yaw;
    float pitch = 15f;

    public void Follow(Transform actor)
    {
        target = actor;
        initialized = false;
        yaw = actor.eulerAngles.y;
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
        if (Time.timeScale <= 0) return;

        float dt = Time.deltaTime;
        var forward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .01f)
            return;

        var mode = target.GetComponent<ControlModeManager>();
        bool auto = mode && mode.IsAutoMode;
        if (!auto && Application.isFocused && Mouse.current != null && Mouse.current.rightButton.isPressed)
        {
            var delta = Mouse.current.delta.ReadValue();
            yaw += delta.x * .15f;
            pitch = Mathf.Clamp(pitch - delta.y * .12f, -12f, 50f);
        }
        if (auto) yaw = Mathf.MoveTowardsAngle(yaw, target.eulerAngles.y, 110f * dt);
        var desiredHeading = Quaternion.Euler(0, yaw, 0);
        bool snap = !initialized || (transform.position - target.position).sqrMagnitude > 36f;
        heading = snap ? desiredHeading : Quaternion.RotateTowards(heading, desiredHeading, 280f * dt);
        var behind = heading * Vector3.back;
        var desiredPosition = target.position + Vector3.up * (CameraHeight + Mathf.Sin(pitch * Mathf.Deg2Rad) * .8f) + behind * Distance;
        var pivot = target.position + Vector3.up * LookHeight;
        if (Physics.Linecast(pivot, desiredPosition, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
            !hit.transform.IsChildOf(target)) desiredPosition = hit.point + hit.normal * .08f;

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
