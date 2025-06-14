using UnityEngine;
using UnityEditor;
using System;

public class BuildSmplArmature : EditorWindow
{
    TextAsset skinJson;

    [MenuItem("Tools/SMPL/Build Armature")]
    public static void ShowWindow()
    {
        GetWindow<BuildSmplArmature>("SMPL Armature Builder");
    }

    void OnGUI()
    {
        GUILayout.Label("SMPL Armature Builder", EditorStyles.boldLabel);

        skinJson = (TextAsset)EditorGUILayout.ObjectField(
            "Skin JSON",
            skinJson,
            typeof(TextAsset),
            false
        );

        if (skinJson != null && GUILayout.Button("Build Armature"))
            BuildArmature();
    }

    void BuildArmature()
    {
        if (skinJson == null)
        {
            Debug.LogError("Please assign a skin JSON asset.");
            return;
        }

        var data = JsonUtility.FromJson<FlatSkinJson>(skinJson.text);
        int J = data.jointCount;

        if (data.jointPos_flat == null || data.jointPos_flat.Length != J * 3)
        {
            Debug.LogError($"Expected jointPos_flat length {J * 3}, got {data.jointPos_flat?.Length}");
            return;
        }
        if (data.parents == null || data.parents.Length != J)
        {
            Debug.LogError($"Expected parents length {J}, got {data.parents?.Length}");
            return;
        }

        GameObject rigGO = GameObject.Find("SMPL_Rig");
        if (rigGO == null)
            rigGO = new GameObject("SMPL_Rig");

        var bones = new Transform[J];
        for (int i = 0; i < J; i++)
        {
            float x = data.jointPos_flat[i * 3 + 0];
            float y = data.jointPos_flat[i * 3 + 1];
            float z = data.jointPos_flat[i * 3 + 2];

            var boneGO = new GameObject($"Bone_{i}");
            boneGO.transform.position = new Vector3(x, y, z);
            bones[i] = boneGO.transform;

            var sph = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sph.name = $"Viz_{i}";
            sph.transform.SetParent(boneGO.transform, true);
            sph.transform.localPosition = Vector3.zero;
            sph.transform.localScale = Vector3.one * 0.02f;
            DestroyImmediate(sph.GetComponent<Collider>());
        }

        for (int i = 0; i < J; i++)
        {
            int p = data.parents[i];
            if (p < 0)
                bones[i].SetParent(rigGO.transform, true);
            else
                bones[i].SetParent(bones[p], true);
        }

        Selection.activeGameObject = rigGO;
    }

    [Serializable]
    class FlatSkinJson
    {
        public float[] jointPos_flat; 
        public int jointCount;
        public int[] parents;       
    }
}
