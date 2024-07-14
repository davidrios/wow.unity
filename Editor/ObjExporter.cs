using UnityEngine;
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
        private int vertexOffset = 0;
        private int normalOffset = 0;
        private int uvOffset = 0;

        private string MeshToString(MeshFilter mf, Dictionary<string, ObjMaterial> materialList)
        {
            var m = mf.sharedMesh;
            var mats = mf.GetComponent<Renderer>().sharedMaterials;

            var sb = new StringBuilder();

            var groupName = mf.name;
            if (string.IsNullOrEmpty(groupName))
            {
                groupName = GetRandomStr();
            }

            sb.Append("g ").Append(groupName).Append("\n");
            foreach (var lv in m.vertices)
            {
                var wv = mf.transform.TransformPoint(lv);

                // Adjust for different coordinate system
                sb.Append(string.Format(
                    "v {0} {1} {2}\n",
                    FloatToStr(-wv.x),
                    FloatToStr(wv.y),
                    FloatToStr(wv.z)
                ));
            }

            sb.Append("\n");

            foreach (var lv in m.normals)
            {
                var wv = mf.transform.TransformDirection(lv);

                sb.Append(string.Format(
                    "vn {0} {1} {2}\n",
                    FloatToStr(-wv.x),
                    FloatToStr(wv.y),
                    FloatToStr(wv.z)
                ));
            }

            sb.Append("\n");

            foreach (var v in m.uv)
            {
                sb.Append(string.Format(
                    "vt {0} {1}\n",
                    FloatToStr(v.x),
                    FloatToStr(v.y)
                ));
            }

            for (var material = 0; material < m.subMeshCount; material++)
            {
                sb.Append("\n");
                sb.Append("usemtl ").Append(mats[material].name).Append("\n");
                sb.Append("usemap ").Append(mats[material].name).Append("\n");

                // See if this material is already in the material list.
                try
                {
                    var objMaterial = new ObjMaterial { name = mats[material].name };

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

                var triangles = m.GetTriangles(material);
                for (var i = 0; i < triangles.Length; i += 3)
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

        private void Clear()
        {
            vertexOffset = 0;
            normalOffset = 0;
            uvOffset = 0;
        }

        private void MeshesToFile(MeshFilter[] mf, string path)
        {
            Clear();
            var materialList = new Dictionary<string, ObjMaterial>();

            using StreamWriter sw = new(path);
            sw.Write("mtllib ./" + Path.GetFileNameWithoutExtension(path) + ".mtl\n");

            for (var i = 0; i < mf.Length; i++)
            {
                sw.Write(MeshToString(mf[i], materialList));
            }
        }

        public static void ExportObj(GameObject obj, string path)
        {
            var exportedObjects = 0;
            var mfList = new ArrayList();
            var meshfilter = obj.GetComponentsInChildren(typeof(MeshFilter));

            for (var m = 0; m < meshfilter.Length; m++)
            {
                exportedObjects++;
                mfList.Add(meshfilter[m]);
            }

            if (exportedObjects == 0)
                return;

            var mf = new MeshFilter[mfList.Count];

            for (int i = 0; i < mfList.Count; i++)
            {
                mf[i] = (MeshFilter)mfList[i];
            }

            (new ObjExporter()).MeshesToFile(mf, path);
        }

        private static string FloatToStr(double number)
        {
            return String.Format("{0:0.################}", number);
        }

        private static string GetRandomStr()
        {
            var s = Path.GetRandomFileName() + DateTime.Now.Millisecond.ToString();
            s = s.Replace(".", "");

            return s;
        }
    }
}