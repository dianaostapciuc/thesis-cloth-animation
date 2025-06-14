using UnityEngine;

public class SMPLLegSwinger : MonoBehaviour
{
    [Tooltip("The upper leg bone (e.g., left thigh).")]
    public Transform upperLegBone;

    [Tooltip("Swing angle in degrees (positive = forward, negative = backward).")]
    public float swingAngle = 30f;

    [Tooltip("Time for first forward leg raise (neutral → forward).")]
    public float forwardPhaseDuration = 1f;

    [Tooltip("Time for backward to forward return (backward → neutral).")]
    public float returnPhaseDuration = 2f;

    [Tooltip("Time for middle phase (forward → backward).")]
    public float middlePhaseDuration = 1f;

    [Tooltip("Pause before starting next cycle (seconds).")]
    public float pauseDuration = 1.5f;

    [Tooltip("Delay before animation starts (seconds).")]
    public float startDelay = 0.5f;

    private float elapsed = 0f;
    private int phase = 0; // 0 = neutral → forward, 1 = forward → backward, 2 = backward → neutral
    private bool paused = false;
    private bool started = false;
    private float delayTimer = 0f;

    private float CurrentPhaseDuration =>
        phase == 0 ? forwardPhaseDuration :
        phase == 1 ? middlePhaseDuration :
                     returnPhaseDuration;

    void Update()
    {
        if (upperLegBone == null) return;

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
        float t = Mathf.Clamp01(elapsed / CurrentPhaseDuration);

        float angle = 0f;
        switch (phase)
        {
            case 0:
                angle = Mathf.Lerp(0f, swingAngle, t);
                break;
            case 1:
                angle = Mathf.Lerp(swingAngle, -swingAngle, t);
                break;
            case 2:
                angle = Mathf.Lerp(-swingAngle, 0f, t);
                break;
        }

        upperLegBone.localRotation = Quaternion.Euler(angle, 0f, 0f);

        if (t >= 1f)
        {
            elapsed = 0f;
            phase = (phase + 1) % 3;
            paused = true;
            Invoke(nameof(Resume), pauseDuration);
        }
    }

    void Resume()
    {
        paused = false;
    }
}
