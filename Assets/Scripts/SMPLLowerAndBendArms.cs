using UnityEngine;

public class SMPLLowerAndBendArms : MonoBehaviour
{
    public Transform smplRigRoot;

    [Header("SMPL Bone Indices")]
    public int leftShoulderIndex = 16;
    public int rightShoulderIndex = 17;
    public int spineRootIndex = 0;

    [Header("Animation Settings")]
    public float duration = 1.5f;
    public float shoulderDownAngleZ = 90f;
    public float bendOverAngle = 30f;

    [Header("Timing Delays")]
    public float initialDelay = 0.5f;
    public float betweenPhaseDelay = 0.5f;

    private Transform leftShoulder, rightShoulder;
    private Transform spineRoot;

    private Quaternion lShoulderStart, rShoulderStart;   
    private Quaternion lShoulderMid, rShoulderMid;   
    private Quaternion lShoulderRest, rShoulderRest; 
    private Quaternion spineStart, spineTarget;

    private float elapsed = 0f;
    private bool phase1Started = false;
    private bool phase1Finished = false;
    private bool phase2Started = false;

    void Start()
    {
        leftShoulder = FindRecursive(smplRigRoot, $"Bone_{leftShoulderIndex}");
        rightShoulder = FindRecursive(smplRigRoot, $"Bone_{rightShoulderIndex}");
        spineRoot = FindRecursive(smplRigRoot, $"Bone_{spineRootIndex}");

        if (!leftShoulder || !rightShoulder || !spineRoot)
        {
            Debug.LogError("Bone(s) not found.");
            enabled = false;
            return;
        }

        lShoulderStart = leftShoulder.localRotation;
        rShoulderStart = rightShoulder.localRotation;
        spineStart = spineRoot.localRotation;

        lShoulderMid = lShoulderStart * Quaternion.Euler(0f, 0f, -shoulderDownAngleZ);
        rShoulderMid = rShoulderStart * Quaternion.Euler(0f, 0f, shoulderDownAngleZ);
        spineTarget = spineStart * Quaternion.Euler(bendOverAngle, 0f, 0f);

        lShoulderRest = lShoulderMid;
        rShoulderRest = rShoulderMid;
    }

    void Update()
    {
        elapsed += Time.deltaTime;

        if (!phase1Started && elapsed >= initialDelay)
        {
            phase1Started = true;
            elapsed = 0f;
        }

        if (phase1Started && !phase1Finished && elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            leftShoulder.localRotation = Quaternion.Slerp(lShoulderStart, lShoulderMid, t);
            rightShoulder.localRotation = Quaternion.Slerp(rShoulderStart, rShoulderMid, t);
            spineRoot.localRotation = Quaternion.Slerp(spineStart, spineTarget, t);
        }

        if (phase1Started && !phase1Finished && elapsed >= duration)
        {
            phase1Finished = true;
            elapsed = 0f;
        }

        if (phase1Finished && !phase2Started && elapsed >= betweenPhaseDelay)
        {
            phase2Started = true;
            elapsed = 0f;
        }

        if (phase2Started && elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            leftShoulder.localRotation = Quaternion.Slerp(lShoulderMid, lShoulderRest, t); // remains down
            rightShoulder.localRotation = Quaternion.Slerp(rShoulderMid, rShoulderRest, t);
            spineRoot.localRotation = Quaternion.Slerp(spineTarget, spineStart, t); // straightens
        }
    }

    Transform FindRecursive(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;

        foreach (Transform child in parent)
        {
            var found = FindRecursive(child, name);
            if (found != null) return found;
        }

        return null;
    }
}
