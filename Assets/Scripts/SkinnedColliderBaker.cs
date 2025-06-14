using UnityEngine;

[RequireComponent(typeof(SkinnedMeshRenderer))]
public class SkinnedColliderBaker : MonoBehaviour
{
    MeshCollider _col;
    SkinnedMeshRenderer _smr;
    Mesh _bakedMesh;

    void Awake()
    {
        _smr = GetComponent<SkinnedMeshRenderer>();

        _col = GetComponent<MeshCollider>();
        if (_col == null) _col = gameObject.AddComponent<MeshCollider>();

        _bakedMesh = new Mesh
        {
            name = "Baked_" + _smr.sharedMesh.name
        };

        _col.convex = false; // so the cloth can wrap around it
    }

    void LateUpdate()
    {
        _smr.BakeMesh(_bakedMesh);
        _col.sharedMesh = _bakedMesh;
    }
}
