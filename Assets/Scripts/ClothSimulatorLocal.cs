using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ClothSimulatorLocal : MonoBehaviour
{
    public struct Particle { public Vector3 x, xPrev, v; public float invMass; }
    struct Constraint { public int i, j; public float rest; }

    public SkinnedColliderBaker colliderBaker;
    public SkinnedMeshRenderer skinnedRenderer;
    public Collider[] bodyColliders;

    [Range(1, 10)] public int substepsPerFrame = 4;

    float[] skinFollowWeights;

    [Header("Solver Settings")]
    public float timeStep = 0f;
    public int solverIterations = 10;
    public Vector3 gravity = new Vector3(0, -9.81f, 0);
    [Range(0, 1)] public float damping = 0.95f;

    [Header("Skinned Follow Constraint")]
    [Range(0f, 1f)] public float skinFollowWeight = 0.05f;
    [Range(0f, 50f)] public float skinFollowStiffness = 0.4f;

    [Header("Collision")]
    public float clothThickness = 0.005f;
    public float maxPenetrationPush = 0.04f;

    [Header("Skin-Follow Control")]
    [Range(0f, 1f)] public float skinFollowGlobal = 0.651f;


    [Header("Self-Collision")]
    [Range(0.001f, 0.05f)] public float selfCollisionRadius = 0.01f;
    [Range(0.01f, 1f)] public float selfCollisionStiffness = 0.5f;

    [Header("Smoothing")]
    [Range(0f, 1f)] public float smoothingStrength = 0.726f;

    [Header("Body-Offset & Collision")]
    [Tooltip("How far the cloth is pushed off the skinned surface")]
    public float normalOffset = 0.012f; 

    MeshFilter mf;
    MeshRenderer mr;
    Mesh bakedMesh;
    Mesh simMesh;
    public Particle[] particles;
    List<Constraint> structuralConstraints;

    List<Vector3> bakedVerticesWorld;
    Vector3[] simMeshVerticesLocal;

    GameObject probeObject;
    SphereCollider probeCollider;
    Dictionary<int, List<int>> vertexNeighbors = new Dictionary<int, List<int>>();
    List<Vector3> bakedNormalsLocal = new List<Vector3>();

    void Awake()
    {
        timeStep = Time.fixedDeltaTime;
        mf = GetComponent<MeshFilter>();
        mr = GetComponent<MeshRenderer>();
        bakedMesh = new Mesh { name = "bakedMesh" };
        simMesh = new Mesh { name = "simMesh" };
        mf.sharedMesh = simMesh;
        if (skinnedRenderer != null)
            mr.sharedMaterial = skinnedRenderer.sharedMaterial;
    }

    void Start()
    {
        if (skinnedRenderer == null)
        {
            Debug.LogError("ClothSimulatorLocal requires a SkinnedMeshRenderer.");
            enabled = false;
            return;
        }

        bakedVerticesWorld = new List<Vector3>(bakedMesh.vertexCount > 0 ? bakedMesh.vertexCount : 1000);
        for (int i = 0; i < bakedMesh.vertexCount; i++) bakedVerticesWorld.Add(Vector3.zero);

        BakeSkinnedMesh();
        simMeshVerticesLocal = new Vector3[bakedVerticesWorld.Count];
        for (int i = 0; i < bakedVerticesWorld.Count; i++)
            simMeshVerticesLocal[i] = transform.InverseTransformPoint(bakedVerticesWorld[i]);

        skinFollowWeights = new float[simMeshVerticesLocal.Length];
        Color[] colors = skinnedRenderer.sharedMesh.colors;

        for (int i = 0; i < skinFollowWeights.Length; i++)
        {
            skinFollowWeights[i] = (colors != null && i < colors.Length) ? Mathf.Clamp01(colors[i].r) : 1f;
            skinFollowWeights[i] = Mathf.SmoothStep(0f, 1f, skinFollowWeights[i]);
        }

        if (bakedMesh.vertexCount > 0)
            bakedNormalsLocal = new List<Vector3>(new Vector3[bakedMesh.vertexCount]);

        simMesh.vertices = simMeshVerticesLocal;
        simMesh.triangles = bakedMesh.triangles;
        simMesh.normals = bakedMesh.normals;
        simMesh.MarkDynamic();

        transform.position = Vector3.zero;
        transform.localScale = Vector3.one;

        InitializeParticlesLocal();
        BuildConstraintsLocal();

        probeObject = new GameObject("ClothProbeCollider");
        probeObject.hideFlags = HideFlags.HideAndDontSave;
        probeCollider = probeObject.AddComponent<SphereCollider>();
        probeCollider.radius = clothThickness;
        probeCollider.isTrigger = true;

        if (colliderBaker != null)
        {
            var col = colliderBaker.GetComponent<Collider>();
            if (col != null) bodyColliders = new Collider[] { col };
        }
    }

    void BakeSkinnedMesh()
    {
        skinnedRenderer.BakeMesh(bakedMesh);
        bakedMesh.GetVertices(bakedVerticesWorld);
        bakedMesh.GetNormals(bakedNormalsLocal);
        for (int i = 0; i < bakedVerticesWorld.Count; i++)
        {
            var v = bakedVerticesWorld[i];
            if (!IsValidVector(v))
            {
                Debug.LogError($"[PBD] Invalid baked vertex at {i}: {v}");
                bakedVerticesWorld[i] = Vector3.zero;
            }
        }
    }
    void FixedUpdate()
    {
        float substepTime = timeStep / substepsPerFrame;
        BakeSkinnedMesh();

        for (int step = 0; step < substepsPerFrame; step++)
        {

            DoCollisionPass_OverlapSphere();
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].xPrev = particles[i].x;
                particles[i].v += gravity * substepTime;
                particles[i].x += particles[i].v * substepTime;
            }

            for (int iter = 0; iter < solverIterations; iter++)
            {
                for (int i = 0; i < particles.Length; i++)
                {
                    Vector3 bakedPos = bakedVerticesWorld[i];
                    Vector3 bakedNormal = bakedNormalsLocal[i];
                    Vector3 worldNormal = skinnedRenderer.transform.TransformDirection(bakedNormal).normalized;

                    Vector3 offsetWorld = bakedPos + worldNormal * normalOffset;

                    Vector3 followTarget = transform.InverseTransformPoint(offsetWorld);
                    if (!IsValidVector(followTarget)) continue;

                    Vector3 delta = followTarget - particles[i].x;
                    if (!IsValidVector(delta)) continue;

                    float w = (skinFollowWeights != null && i < skinFollowWeights.Length)
                              ? skinFollowWeights[i]
                              : skinFollowWeight;
                    w *= skinFollowGlobal;

                    Vector3 worldPos = skinnedRenderer.transform.TransformPoint(particles[i].x);
                    Vector3 normal; float penetration;
                    if (Physics.ComputePenetration(
                            probeCollider, worldPos, Quaternion.identity,
                            bodyColliders[0], bodyColliders[0].transform.position, bodyColliders[0].transform.rotation,
                            out normal, out penetration))
                    {
                        continue;
                    }

                    Vector3 pull = delta * w * skinFollowStiffness;
                    pull = Vector3.ClampMagnitude(pull, maxPenetrationPush);
                    particles[i].x += pull;
                }

                foreach (var c in structuralConstraints)
                    ApplyConstraint(ref particles[c.i], ref particles[c.j], c.rest);

                if (iter == solverIterations - 1 && smoothingStrength > 0f)
                    ApplyLaplacianSmoothing();

            }

            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].v = (particles[i].x - particles[i].xPrev) / substepTime;
                particles[i].v *= damping;
            }
        }
        DoCollisionPass_OverlapSphere();

        for (int i = 0; i < particles.Length; i++)
        {
            Vector3 p = particles[i].x;
            if (!IsValidVector(p) || p.magnitude > 1000f)
            {
                Debug.LogError($"[PBD] Invalid particle position at {i}: {p}. Resetting.");
                p = Vector3.zero;
                particles[i].v = Vector3.zero;
            }
            simMeshVerticesLocal[i] = p;
        }

        simMesh.vertices = simMeshVerticesLocal;
        simMesh.RecalculateNormals();
        simMesh.RecalculateBounds();
        mf.sharedMesh = simMesh;
    }
    void DoCollisionPass_OverlapSphere()
    {
        if (bodyColliders == null || bodyColliders.Length == 0 || probeCollider == null) return;
        var skinTf = skinnedRenderer.transform;
        for (int i = 0; i < particles.Length; i++)
        {
            Vector3 worldPos = skinTf.TransformPoint(particles[i].x);
            probeObject.transform.position = worldPos;
            foreach (var bodyCol in bodyColliders)
            {
                if (!bodyCol.enabled) continue;
                if (Physics.ComputePenetration(
                    probeCollider, worldPos, Quaternion.identity,
                    bodyCol, bodyCol.transform.position, bodyCol.transform.rotation,
                    out Vector3 direction, out float distance))
                {
                    if (distance > 0f)
                    {
                        Vector3 correction = direction * Mathf.Min(distance, maxPenetrationPush);
                        Vector3 localCorrection = skinTf.InverseTransformVector(correction);
                        particles[i].x += localCorrection;
                        particles[i].v *= 0.9f;
                    }
                }
            }
        }
    }

    void ApplyConstraint(ref Particle a, ref Particle b, float restLength)
    {
        Vector3 d = b.x - a.x;
        float len = d.magnitude;
        if (len < 1e-6f) return;
        float w = a.invMass + b.invMass;
        if (w <= 1e-8f) return;
        Vector3 corr = d.normalized * (len - restLength);
        a.x += corr * (a.invMass / w);
        b.x -= corr * (b.invMass / w);
    }

    void ApplyLaplacianSmoothing()
    {
        if (smoothingStrength <= 0f) return;

        Vector3[] smoothed = new Vector3[particles.Length];

        for (int i = 0; i < particles.Length; i++)
        {
            if (!vertexNeighbors.TryGetValue(i, out var neighbors) || neighbors.Count == 0)
            {
                smoothed[i] = particles[i].x;
                continue;
            }

            Vector3 avg = Vector3.zero;
            foreach (int j in neighbors)
            {
                avg += particles[j].x;
            }

            avg /= neighbors.Count;
            smoothed[i] = Vector3.Lerp(particles[i].x, avg, smoothingStrength);
        }

        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].x = smoothed[i];
        }
    }

    void InitializeParticlesLocal()
    {
        var V = simMeshVerticesLocal;
        particles = new Particle[V.Length];
        for (int i = 0; i < V.Length; i++)
        {
            Vector3 pos = V[i];
            if (!IsValidVector(pos)) pos = Vector3.zero;
            particles[i] = new Particle { x = pos, xPrev = pos, v = Vector3.zero, invMass = 1f };
        }
    }

    void BuildConstraintsLocal()
    {
        structuralConstraints = new List<Constraint>();
        var T = simMesh.triangles;
        var V = simMesh.vertices;
        var edgeSet = new HashSet<(int, int)>();

        for (int i = 0; i < T.Length; i += 3)
        {
            AddEdge(T[i], T[i + 1], V, edgeSet);
            AddEdge(T[i + 1], T[i + 2], V, edgeSet);
            AddEdge(T[i + 2], T[i], V, edgeSet);
        }

        var shared = new Dictionary<(int, int), List<int>>();
        for (int i = 0; i < T.Length; i += 3)
        {
            int a = T[i], b = T[i + 1], c = T[i + 2];
            void Record(int u, int v, int w)
            {
                var key = u < v ? (u, v) : (v, u);
                if (!shared.ContainsKey(key)) shared[key] = new List<int>();
                shared[key].Add(w);
            }
            Record(a, b, c);
            Record(b, c, a);
            Record(c, a, b);
        }

        foreach (var kv in shared)
        {
            var opp = kv.Value;
            if (opp.Count == 2)
            {
                int v0 = opp[0], v1 = opp[1]; 
                int a = Mathf.Min(v0, v1), b = Mathf.Max(v0, v1);
                if (edgeSet.Add((a, b)))
                {
                    float rest = Vector3.Distance(V[v0], V[v1]);
                    structuralConstraints.Add(new Constraint { i = v0, j = v1, rest = rest });
                    rest *= 1.1f;
                }
            }
        }
    }


    void AddEdge(int i, int j, Vector3[] V, HashSet<(int, int)> edgeSet)
    {
        if (i == j) return;
        int a = Mathf.Min(i, j);
        int b = Mathf.Max(i, j);
        if (!edgeSet.Add((a, b))) return;
        float rest = Vector3.Distance(V[i], V[j]);
        structuralConstraints.Add(new Constraint { i = i, j = j, rest = rest });
        if (!vertexNeighbors.ContainsKey(i)) vertexNeighbors[i] = new List<int>();
        if (!vertexNeighbors.ContainsKey(j)) vertexNeighbors[j] = new List<int>();
        vertexNeighbors[i].Add(j);
        vertexNeighbors[j].Add(i);
    }

    static bool IsValidVector(Vector3 v)
    {
        return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                 float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }
}
