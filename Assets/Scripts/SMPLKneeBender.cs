using UnityEngine;

public class SMPLKneeBender : MonoBehaviour
{
    [Tooltip("The knee bone to bend (e.g., Bone_5).")]
    public Transform kneeBone;

    [Tooltip("Bend angle in degrees (negative = backward).")]
    public float bendAngle = -60f;

    [Tooltip("Time it takes to bend the knee (seconds).")]
    public float bendDuration = 1.5f;

    [Tooltip("Pause before reversing bend (seconds).")]
    public float pauseDuration = 2f;

    [Tooltip("Delay before animation starts (seconds).")]
    public float startDelay = 1f;

    private float elapsed = 0f;
    private bool bending = true;
    private bool paused = false;
    private bool started = false;
    private float delayTimer = 0f;

    void Update()
    {
        if (kneeBone == null) return;

        if (!started)
        {
            delayTimer += Time.deltaTime;
            if (delayTimer >= startDelay)
                started = true;
            else
                return;
        }

        if (paused) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / bendDuration);

        float angle = bending ? Mathf.Lerp(0, bendAngle, t) : Mathf.Lerp(bendAngle, 0, t);
        kneeBone.localRotation = Quaternion.Euler(angle, 0, 0);

        if (t >= 1f)
        {
            paused = true;
            Invoke(nameof(Resume), pauseDuration);
            bending = !bending;
            elapsed = 0f;
        }
    }

    void Resume()
    {
        paused = false;
    }
}
