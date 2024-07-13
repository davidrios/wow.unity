using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    class BirpMaterialUtility
    {
        public const string LIT_SHADER = "Standard";
        public const string UNLIT_SHADER = "Particles/Standard Unlit";
        public const string ADT_CHUNK_SHADER = "wow.unity/BirpTerrainShader";

        public static Material GetMaterial(M2Utility.Texture texture, MaterialUtility.MaterialFor materialFor, short flags, uint blendingMode, int shader, Color materialColor) {
            var colorName = materialColor == Color.white ? "W" : ColorUtility.ToHtmlStringRGBA(materialColor);
            var matName = $"{Path.GetFileNameWithoutExtension(texture.fileNameExternal)}_TF{texture.flag}_F{flags:X}_B{blendingMode:X}_S{shader:X}_C{colorName}";
            var assetMatPath = Path.Join(Path.GetDirectoryName(texture.assetPath), $"{matName}.mat");

            var material = AssetDatabase.LoadAssetAtPath<Material>(assetMatPath);
            if (material != null)
                return material;

            Debug.Log($"{matName}: material does not exist, creating.");

            material = new Material(Shader.Find(LIT_SHADER));
            material.SetFloat("_Glossiness", 0);
            if (shader == 1)
            {
                material.SetFloat("_Glossiness", 1);
                material.SetFloat("_SmoothnessTextureChannel", 1);
            }

            //Flags first
            if ((flags & (short)MaterialUtility.MaterialFlags.Unlit) != (short)MaterialUtility.MaterialFlags.None)
            {
                material.shader = Shader.Find(UNLIT_SHADER);
                material.SetFloat("_Cull", 0);
            }

            Texture assetTexture = AssetDatabase.LoadAssetAtPath<Texture>(texture.assetPath);
            material.SetTexture("_MainTex", assetTexture);
            material.SetColor("_Color", materialColor);

            //Now blend modes
            if (blendingMode == (short)MaterialUtility.BlendModes.AlphaKey)
            {
                material.SetFloat("_Mode", 1);
            }

            if (blendingMode == (short)MaterialUtility.BlendModes.Alpha)
            {
                if (materialFor == MaterialUtility.MaterialFor.M2)
                    material.SetFloat("_Mode", 2);
                else
                    material.SetFloat("_Mode", 3);

                material.SetFloat("_ZWrite", 0);
            }

            if (blendingMode == (short)MaterialUtility.BlendModes.Add)
            {
                material.SetFloat("_Mode", 3);
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.SetFloat("_Cutoff", 0);
                material.SetFloat("_SrcBlend", 1);
                material.SetFloat("_DstBlend", 1);
                material.SetFloat("_ZWrite", 0);
                material.SetShaderPassEnabled("ShadowCaster", false);
            }

            if (materialFor == MaterialUtility.MaterialFor.WMO && (flags & 16) == 16)
            {
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", Color.white);
                material.SetTexture("_EmissionMap", assetTexture);
            }

            AssetDatabase.CreateAsset(material, assetMatPath);
            AssetDatabase.SaveAssets();

            return material;
        }

        public static Material GetTerrainMaterial(string dirName, string sectionName, ADTUtility.Tex metadata)
        {
            var matDir = Path.Join(dirName, "terrain_materials");
            if (!Directory.Exists(matDir))
                Directory.CreateDirectory(matDir);
            
            var assetMatPath = Path.Join(matDir, $"tex_{sectionName}.mat");

            var assetMat = AssetDatabase.LoadAssetAtPath<Material>(assetMatPath);
            if (assetMat != null)
                return assetMat;
            
            Debug.Log($"{assetMatPath}: material does not exist, creating.");

            var textures = metadata.layers.Select((item) => AssetDatabase.LoadAssetAtPath<Texture>(item.assetPath)).ToList();
            var mask = AssetDatabase.LoadAssetAtPath<Texture>(Path.Join(dirName, $"tex_{sectionName}.png"));

            assetMat = new Material(Shader.Find(ADT_CHUNK_SHADER));

            assetMat.SetTexture("_MaskTex", mask);
            assetMat.SetTexture("_Layer0Tex", textures[0]);
            assetMat.SetFloat("_Smoothness", metadata.layers[0].assetPath.EndsWith("_s.png") ? 1 : 0);
            if (textures.Count >= 2)
                assetMat.SetTexture("_Layer1Tex", textures[1]);
            if (textures.Count >= 3)
                assetMat.SetTexture("_Layer2Tex", textures[2]);
            if (textures.Count >= 4)
                assetMat.SetTexture("_Layer3Tex", textures[3]);
            if (textures.Count >= 5)
                assetMat.SetTexture("_Layer4Tex", textures[4]);

            AssetDatabase.CreateAsset(assetMat, assetMatPath);

            return assetMat;
        }
    }
}
