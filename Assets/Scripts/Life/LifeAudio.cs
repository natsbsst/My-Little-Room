using System.Linq;
using UnityEngine;

public class LifeAudio : MonoBehaviour
{
    AudioSource actions;
    string previousAction;
    void Awake()
    {
        actions = gameObject.AddComponent<AudioSource>();
        actions.playOnAwake = false;
        actions.spatialBlend = 0;
        actions.volume = .55f;
        var steps = gameObject.AddComponent<CharacterFootsteps>();
        var footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.playOnAwake = false;
        footstepSource.spatialBlend = 0;
        footstepSource.volume = .3f;
        var clips = Resources.LoadAll<AudioClip>("LifeAudio/SocksWood").OrderBy(clip => clip.name).ToArray();
        steps.Configure(footstepSource, clips);
    }
    public void PlayAction(string name)
    {
        var clip = Resources.Load<AudioClip>("LifeAudio/" + name);
        if (clip) actions.PlayOneShot(clip);
    }
    void Update()
    {
        var manual = GetComponent<ManualInteraction>();
        string current = manual && manual.IsBusy ? manual.CurrentActionName : null;
        if (current != previousAction && current != null) PlayAction(current);
        previousAction = current;
    }
}
