
using UnityEngine;

public class CharacterFootsteps : MonoBehaviour
{
    public void Configure(AudioSource source, AudioClip[] clips)
    { footstepAudio = source; footsteps = clips; }
    [Header("Audio")]
    [SerializeField] private AudioSource footstepAudio;
    [SerializeField] private AudioClip[] footsteps;

    [Header("Settings")]
    [SerializeField] private float stepInterval = 0.42f;
    [SerializeField] private float minMoveSpeed = 0.15f;

    private Vector3 previousPosition;
    private float stepTimer;
    private int stepIndex;

    private void Start()
    {
        previousPosition = transform.position;
    }

    private void LateUpdate()
    {
        Vector3 currentPosition = transform.position;
        Vector3 movement = currentPosition - previousPosition;

        previousPosition = currentPosition;
        movement.y = 0f;

        if (Time.timeScale == 0f ||
            Time.deltaTime <= 0f)
        {
            stepTimer = 0f;
            return;
        }

        float speed = movement.magnitude / Time.deltaTime;

        // Не воспроизводим шаги,
        // если персонаж стоит или телепортировался.
        if (speed < minMoveSpeed ||
            movement.magnitude > 1f)
        {
            stepTimer = 0f;
            return;
        }

        stepTimer -= Time.deltaTime;

        if (stepTimer <= 0f)
        {
            PlayFootstep();
            stepTimer = stepInterval;
        }
    }

    private void PlayFootstep()
    {
        if (footstepAudio == null ||
            footsteps == null ||
            footsteps.Length == 0)
        {
            return;
        }

        AudioClip clip =
            footsteps[stepIndex % footsteps.Length];

        if (clip != null)
            footstepAudio.PlayOneShot(clip);

        stepIndex++;
    }
}
