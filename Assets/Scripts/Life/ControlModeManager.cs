using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class ControlModeManager : MonoBehaviour
{
    public bool IsAutoMode { get; private set; }
    public void SwitchMode()
    {
        var ai = GetComponent<UtilityAI>();
        if (IsAutoMode) { ai.StopAutonomy(); IsAutoMode = false; }
        else if (GetComponents<NeedAction>().Length > 0 && ai.StartAutonomy()) IsAutoMode = true;
    }
    void Update()
    {
        if (Time.timeScale > 0 && Application.isFocused && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            SwitchMode();
    }
    void OnDisable()
    {
        if (IsAutoMode) GetComponent<UtilityAI>()?.StopAutonomy();
        IsAutoMode = false;
    }
}
