using System.Collections;
using UnityEngine;
using System.IO;

public class MeshLoader : MonoBehaviour
{
    public Material defaultMaterial;
    public IEnumerator LoadOBJAndInstantiate(string objFilePath)
    {
        if (!File.Exists(objFilePath))
        {
            Debug.LogError("OBJ file not found: " + objFilePath);
            yield break;
        }

        Mesh importedMesh = SimpleOBJImporter.Import(objFilePath);
        if (importedMesh == null)
        {
            Debug.LogError("Failed to import mesh from OBJ.");
            yield break;
        }

        GameObject objGO = new GameObject("ImportedOBJ");
        MeshFilter mf = objGO.AddComponent<MeshFilter>();
        MeshRenderer mr = objGO.AddComponent<MeshRenderer>();
        mf.mesh = importedMesh;
        mr.material = defaultMaterial;
        objGO.transform.position = Vector3.zero;

        yield return null;
    }
}
