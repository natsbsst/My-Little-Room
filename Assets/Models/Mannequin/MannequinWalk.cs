using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class MannequinWalk : MonoBehaviour
{
    const float WalkSpeed = 2.5f;
    const float TurnSpeed = 140f;
    static readonly int Speed = Animator.StringToHash("Speed");
    static readonly int Idle = Animator.StringToHash("Idle");
    static readonly int Walk = Animator.StringToHash("Walk");

    Animator animator;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Connect()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            OnSceneLoaded(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        if (!scene.isLoaded || scene.path != "Assets/Scenes/SampleScene.unity")
            return;
        var mannequin = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "MyLittleRoom_Mannequin");
        if (mannequin && !mannequin.GetComponent<MannequinWalk>())
            mannequin.AddComponent<MannequinWalk>();
        if (mannequin && Camera.main && Camera.main.gameObject.scene == scene)
        {
            var camera = Camera.main;
            var follow = camera.GetComponent<ThirdPersonMannequinCamera>();
            if (!follow)
                follow = camera.gameObject.AddComponent<ThirdPersonMannequinCamera>();
            follow.Follow(mannequin.transform);
        }
    }

    void Start()
    {
        animator = GetComponent<Animator>();
        if (!animator.parameters.Any(parameter => parameter.nameHash == Speed))
        {
            Debug.LogError("Mannequin Animator is missing its Speed parameter.", this);
            enabled = false;
            return;
        }
    }

    void OnAnimatorMove()
    {
        if (!animator)
            return;
        int state = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        if (state != Idle && state != Walk)
            animator.ApplyBuiltinRootMotion();
    }

    void Update()
    {
        if (!animator)
            return;

        var state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash != Idle && state.shortNameHash != Walk)
        {
            animator.SetFloat(Speed, 0f);
            return;
        }

        var keyboard = Keyboard.current;
        if (!Application.isFocused || keyboard == null)
        {
            animator.SetFloat(Speed, 0f);
            return;
        }

        float forward = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
        float turn = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
        float dt = Time.deltaTime;
        transform.Rotate(Vector3.up, turn * TurnSpeed * dt, Space.World);
        transform.position += transform.forward * (forward * WalkSpeed * dt);
        animator.SetFloat(Speed, Mathf.Abs(forward));
    }
}
