using System.Linq;
using UnityEngine;

[RequireComponent(typeof(ClothSimulatorLocal), typeof(SkinnedMeshRenderer))]
public class GarmentSkinnedPBDSetup : MonoBehaviour
{
    [Header("Body for Collision")]
    public SkinnedMeshRenderer bodySMR;

    void Awake()
    {
        var sim = GetComponent<ClothSimulatorLocal>();
        var garmentSMR = GetComponent<SkinnedMeshRenderer>();
        sim.skinnedRenderer = garmentSMR;
        garmentSMR.enabled = false;

        var bodyBaker = bodySMR.GetComponent<SkinnedColliderBaker>();
        if (bodyBaker == null)
            bodyBaker = bodySMR.gameObject.AddComponent<SkinnedColliderBaker>();
        sim.colliderBaker = bodyBaker;
    }
}
