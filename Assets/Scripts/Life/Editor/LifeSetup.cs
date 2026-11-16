using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LifeSetup
{
    [MenuItem("My Little Room/Life/Configure character")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var actor=GameObject.Find("MyLittleRoom_Mannequin");
        if (!actor) throw new InvalidOperationException("Mannequin not found.");
        if (!actor.GetComponent<LifeBootstrap>()) Undo.AddComponent<LifeBootstrap>(actor);
        var camera=Camera.main;
        if (camera && !camera.GetComponent<ThirdPersonMannequinCamera>()) Undo.AddComponent<ThirdPersonMannequinCamera>(camera.gameObject);
        EditorSceneManager.MarkSceneDirty(actor.scene);
        EditorSceneManager.SaveScene(actor.scene);
        Debug.Log("LIFE_SETUP_PASS: controls attached to the existing mannequin.");
    }
}
