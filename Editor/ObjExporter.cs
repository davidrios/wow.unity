﻿using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;

namespace WowUnity
{

    struct ObjMaterial
    {
        public string name;
        public string textureName;
    }

    public class ObjExporter
    {
        private static int vertexOffset = 0;
        private static int normalOffset = 0;
        private static int uvOffset = 0;

        private static string MeshToString(MeshFilter mf, Dictionary<string, ObjMaterial> materialList)
        {
            Mesh m = mf.sharedMesh;
            Material[] mats = mf.GetComponent<Renderer>().sharedMaterials;

            StringBuilder sb = new StringBuilder();

            string groupName = mf.name;
            if (string.IsNullOrEmpty(groupName))
            {
                groupName = getRandomStr();
            }

            sb.Append("g ").Append(groupName).Append("\n");
            foreach (Vector3 lv in m.vertices)
            {
                Vector3 wv = mf.transform.TransformPoint(lv);

                // This is sort of ugly - inverting x-component since we're in
                // a different coordinate system than "everyone" is "used to".
                sb.Append(string.Format(
                    "v {0} {1} {2}\n",
                    floatToStr(-wv.x),
                    floatToStr(wv.y),
                    floatToStr(wv.z)
                ));
            }

            sb.Append("\n");

            foreach (Vector3 lv in m.normals)
            {
                Vector3 wv = mf.transform.TransformDirection(lv);

                sb.Append(string.Format(
                    "vn {0} {1} {2}\n",
                    floatToStr(-wv.x),
                    floatToStr(wv.y),
                    floatToStr(wv.z)
                ));
            }

            sb.Append("\n");

            foreach (Vector3 v in m.uv)
            {
                sb.Append(string.Format(
                    "vt {0} {1}\n",
                    floatToStr(v.x),
                    floatToStr(v.y)
                ));
            }

            for (int material = 0; material < m.subMeshCount; material++)
            {
                sb.Append("\n");
                sb.Append("usemtl ").Append(mats[material].name).Append("\n");
                sb.Append("usemap ").Append(mats[material].name).Append("\n");

                // See if this material is already in the material list.
                try
                {
                    ObjMaterial objMaterial = new ObjMaterial { name = mats[material].name };

                    if (mats[material].mainTexture)
                        objMaterial.textureName = AssetDatabase.GetAssetPath(mats[material].mainTexture);
                    else
                        objMaterial.textureName = null;

                    materialList.Add(objMaterial.name, objMaterial);
                }
                catch (ArgumentException)
                {
                    // Already in the dictionary
                }

                int[] triangles = m.GetTriangles(material);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    // Because we inverted the x-component, we also needed to alter the triangle winding.
                    sb.Append(
                        string.Format(
                            "f {1}/{1}/{1} {0}/{0}/{0} {2}/{2}/{2}\n",
                            triangles[i + 0] + 1 + vertexOffset,
                            triangles[i + 1] + 1 + normalOffset,
                            triangles[i + 2] + 1 + uvOffset
                        )
                    );
                }
            }

            vertexOffset += m.vertices.Length;
            normalOffset += m.normals.Length;
            uvOffset += m.uv.Length;

            return sb.ToString();
        }

        private static void Clear()
        {
            vertexOffset = 0;
            normalOffset = 0;
            uvOffset = 0;
        }

        private static Dictionary<string, ObjMaterial> PrepareFileWrite()
        {
            Clear();
            return new Dictionary<string, ObjMaterial>();
        }

        private static void MeshesToFile(MeshFilter[] mf, string path)
        {
            Dictionary<string, ObjMaterial> materialList = PrepareFileWrite();

            using (StreamWriter sw = new(path))
            {
                sw.Write("mtllib ./" + Path.GetFileNameWithoutExtension(path) + ".mtl\n");

                for (int i = 0; i < mf.Length; i++)
                {
                    sw.Write(MeshToString(mf[i], materialList));
                }
            }
        }

        public static void ExportObj(GameObject obj, string path)
        {
            int exportedObjects = 0;

            ArrayList mfList = new();

            Component[] meshfilter = obj.GetComponentsInChildren(typeof(MeshFilter));

            for (int m = 0; m < meshfilter.Length; m++)
            {
                exportedObjects++;
                mfList.Add(meshfilter[m]);
            }

            if (exportedObjects == 0)
            {
                return;
            }

            MeshFilter[] mf = new MeshFilter[mfList.Count];

            for (int i = 0; i < mfList.Count; i++)
            {
                mf[i] = (MeshFilter)mfList[i];
            }

            MeshesToFile(mf, path);
        }

        private static string floatToStr(double number)
        {
            return String.Format("{0:0.################}", number);
        }

        private static string getRandomStr()
        {
            string s = Path.GetRandomFileName() + DateTime.Now.Millisecond.ToString();
            s = s.Replace(".", "");

            return s;
        }
    }
}