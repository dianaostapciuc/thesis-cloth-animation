using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Minimal, robust OBJ reader – vertices + faces (triangulated).
/// Handles face tokens v, v/vt, v/vt/vn, v//vn.
/// </summary>
public static class SimpleOBJImporter
{
    private static int SafeParseInt(string token)
    {
        try
        {
            int slash = token.IndexOf('/');
            string core = slash >= 0 ? token.Substring(0, slash) : token;

            int dot = core.IndexOf('.');
            if (dot >= 0)
                core = core.Substring(0, dot);   

            return int.Parse(core) - 1;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Int.Parse failed on token '{token}'  ({ex.Message})");
            throw;
        }
    }


    private static float SafeParseFloat(string token)
    {
        try
        {
            return float.Parse(token, CultureInfo.InvariantCulture);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Float.Parse failed on token '{token}'  ({ex.Message})");
            throw;
        }
    }

    public static Mesh Import(string filePath)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();

        foreach (string line in File.ReadLines(filePath))
        {
            if (line.StartsWith("v "))
            {
                string[] p = line.Split((char[])null,
                                System.StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 4)
                {
                    verts.Add(new Vector3(
                        SafeParseFloat(p[1]),
                        SafeParseFloat(p[2]),
                        SafeParseFloat(p[3])));
                }
            }
            else if (line.StartsWith("f "))
            {
                string[] p = line.Split((char[])null,
                                System.StringSplitOptions.RemoveEmptyEntries);

                var idx = new List<int>(p.Length - 1);
                for (int i = 1; i < p.Length; ++i)
                {
                    string tok = p[i];
                    int slash = tok.IndexOf('/');
                    string vStr = slash >= 0 ? tok.Substring(0, slash) : tok;
                    idx.Add(SafeParseInt(vStr));
                }

                for (int i = 1; i + 1 < idx.Count; ++i)
                {
                    tris.Add(idx[0]);
                    tris.Add(idx[i]);
                    tris.Add(idx[i + 1]);
                }
            }
        }

        var mesh = new Mesh
        {
            vertices = verts.ToArray()
        };
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
