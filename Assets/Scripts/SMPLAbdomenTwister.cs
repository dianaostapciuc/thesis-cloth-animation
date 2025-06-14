using UnityEngine;

public class SMPLAbdomenTwister : MonoBehaviour
{
    [Tooltip("The abdomen (spine) bone to twist (e.g., Spine_3).")]
    public Transform abdomenBone;

    [Tooltip("Twist angle in degrees (positive = right, negative = left).")]
    public float twistAngle = 30f;

    [Tooltip("Time it takes to twist the abdomen (seconds).")]
    public float twistDuration = 2f;

    [Tooltip("Pause before reversing twist (seconds).")]
    public float pauseDuration = 1.5f;

    [Tooltip("Delay before animation starts (seconds).")]
    public float startDelay = 1f;

    private float elapsed = 0f;
    private bool twistingRight = true;
    private bool paused = false;
    private bool started = false;
    private float delayTimer = 0f;

    void Update()
    {
        if (abdomenBone == null) return;

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
        float t = Mathf.Clamp01(elapsed / twistDuration);

        float angle = twistingRight
            ? Mathf.Lerp(0f, twistAngle, t)
            : Mathf.Lerp(twistAngle, -twistAngle, t);

        abdomenBone.localRotation = Quaternion.Euler(0f, angle, 0f);

        if (t >= 1f)
        {
            paused = true;
            Invoke(nameof(Resume), pauseDuration);
            twistingRight = !twistingRight;
            elapsed = 0f;
        }
    }

    void Resume()
    {
        paused = false;
    }
}
