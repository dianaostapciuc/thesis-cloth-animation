using UnityEngine;
using System.Collections.Generic;

[ExecuteInEditMode]
[RequireComponent(typeof(SkinnedMeshRenderer))]
public class SMPLSkinner : MonoBehaviour
{
    [Header("Extended Skin JSON (with vertices_flat & triangles)")]
    public TextAsset skinJsonAsset;

    [Header("Skeleton Root (e.g. your SMPL_Rig GameObject)")]
    public Transform skeletonRoot;

    void Awake()
    {
        if (skinJsonAsset == null)
        {
            Debug.LogError("SMPLSkinner: please assign a skin JSON TextAsset.");
            return;
        }
        if (skeletonRoot == null)
        {
            Debug.LogError("SMPLSkinner: please assign the Skeleton Root Transform.");
            return;
        }

        var data = JsonUtility.FromJson<SkinData>(skinJsonAsset.text);
        if (data == null)
        {
            Debug.LogError("SMPLSkinner: failed to parse SkinData from JSON.");
            return;
        }

        var smr = GetComponent<SkinnedMeshRenderer>();

        Mesh mesh;
        if (smr.sharedMesh != null)
        {
            mesh = Instantiate(smr.sharedMesh);
        }
        else
        {
            mesh = new Mesh();
            mesh.name = skinJsonAsset.name + "_generated";

            mesh.vertices = ToVector3Array(data.vertices_flat);
            mesh.triangles = data.triangles;

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        smr.sharedMesh = mesh;

        var allBones = skeletonRoot.GetComponentsInChildren<Transform>(true);
        var boneMap = new Dictionary<string, Transform>(allBones.Length);
        foreach (var t in allBones)
            boneMap[t.name] = t;

        int J = data.jointCount;
        var binds = new Matrix4x4[J];
        var meshToWorld = smr.transform.localToWorldMatrix;
        var bonesArr = new Transform[J];
        for (int j = 0; j < J; j++)
        {
            string key = $"Bone_{j}";
            if (!boneMap.TryGetValue(key, out var bone))
            {
                Debug.LogError($"SMPLSkinner: could not find {key} under {skeletonRoot.name}");
                return;
            }

            binds[j] = bone.worldToLocalMatrix * meshToWorld;
            bonesArr[j] = bone;
        }
        mesh.bindposes = binds;

        int vcount = mesh.vertexCount;
        if (data.boneIndices_flat.Length != vcount * 4 ||
            data.boneWeights_flat.Length != vcount * 4)
        {
            Debug.LogError(
              $"SMPLSkinner: bone arrays must be length vertexCount*4 ({vcount * 4})."
            );
            return;
        }

        var bw = new BoneWeight[vcount];
        for (int v = 0; v < vcount; v++)
        {
            int i0 = data.boneIndices_flat[v * 4 + 0];
            int i1 = data.boneIndices_flat[v * 4 + 1];
            int i2 = data.boneIndices_flat[v * 4 + 2];
            int i3 = data.boneIndices_flat[v * 4 + 3];

            float w0 = data.boneWeights_flat[v * 4 + 0];
            float w1 = data.boneWeights_flat[v * 4 + 1];
            float w2 = data.boneWeights_flat[v * 4 + 2];
            float w3 = data.boneWeights_flat[v * 4 + 3];

            bw[v] = new BoneWeight
            {
                boneIndex0 = i0,
                weight0 = w0,
                boneIndex1 = i1,
                weight1 = w1,
                boneIndex2 = i2,
                weight2 = w2,
                boneIndex3 = i3,
                weight3 = w3
            };
        }
        mesh.boneWeights = bw;

        smr.bones = bonesArr;
        smr.rootBone = skeletonRoot;
    }
    Vector3[] ToVector3Array(float[] flat)
    {
        int n = flat.Length / 3;
        var arr = new Vector3[n];
        for (int i = 0; i < n; i++)
            arr[i] = new Vector3(
                flat[i * 3 + 0],
                flat[i * 3 + 1],
                flat[i * 3 + 2]
            );
        return arr;
    }

    [System.Serializable]
    public class SkinData
    {
        public int jointCount;
        public float[] vertices_flat;
        public int[] triangles; 
        public int[] boneIndices_flat;
        public float[] boneWeights_flat; 
    }
}
