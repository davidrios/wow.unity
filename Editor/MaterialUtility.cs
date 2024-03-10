using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    class MaterialUtility
    {
        public const string LIT_SHADER = "Standard";
        public const string UNLIT_SHADER = "Unlit/Texture";
        public const string EFFECT_SHADER = "Universal Render Pipeline/Particles/Unlit";
        public const string ADT_CHUNK_SHADER = "wow.unity/TerrainChunk";

        public enum MaterialFlags : short
        {
            None = 0x0,
            Unlit = 0x1,
            Unfogged = 0x2,
            TwoSided = 0x4
        }

        public enum BlendModes : short
        {
            Opaque = 0,
            AlphaKey = 1,
            Alpha = 2,
            NoAlphaAdd = 3,
            Add = 4,
            Mod = 5,
            Mod2X = 6,
            BlendAdd = 7
        }

        public static Material GetMaterial(M2Utility.Texture texture, short flags, uint blendingMode, int shader) {
            var matName = $"{Path.GetFileNameWithoutExtension(texture.fileNameExternal)}_TF{texture.flag}_F{flags}_B{blendingMode}_S{shader}";
            var assetMatPath = Path.Join(Path.GetDirectoryName(texture.assetPath), $"{matName}.mat");

            var assetMat = AssetDatabase.LoadAssetAtPath<Material>(assetMatPath);
            if (assetMat == null) {
                Debug.Log($"{matName}: material does not exist, creating.");

                Texture assetTexture = AssetDatabase.LoadAssetAtPath<Texture>(texture.assetPath);

                assetMat = new Material(Shader.Find(LIT_SHADER));
                assetMat.SetFloat("_Glossiness", 0);
                assetMat.SetTexture("_MainTex", assetTexture);
                    
                ProcessFlagsForMaterial(assetMat, flags, blendingMode);

                if (shader == 1) {
                    assetMat.SetFloat("_SmoothnessTextureChannel", 1);
                }

                if ((flags & 16) == 16) {
                    assetMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    assetMat.SetColor("_EmissionColor", Color.white);
                    assetMat.SetTexture("_EmissionMap", assetTexture);
                }

                AssetDatabase.CreateAsset(assetMat, assetMatPath);
                AssetDatabase.SaveAssets();
            }

            return assetMat;
        }

        public static Dictionary<uint, Material> GetSkinMaterials(M2Utility.M2 metadata)
        {
            Dictionary<uint, Material> mats = new();

            foreach (var textureUnit in metadata.skin.textureUnits) {
                var texture = metadata.textures[metadata.textureCombos[checked((int)textureUnit.textureComboIndex)]];
                var unitMat = metadata.materials[checked((int)textureUnit.materialIndex)];
                mats[textureUnit.skinSectionIndex] = GetMaterial(texture, unitMat.flags, unitMat.blendingMode, 0);
            }

            return mats;
        }

        public static Dictionary<string, Material> GetWMOMaterials(WMOUtility.WMO metadata)
        {
            Dictionary<string, Material> mats = new();

            var texturesById = metadata.textures.ToDictionary((item) => item.fileDataID, (item) => item);

            foreach (var group in metadata.groups) {
                for (var batchIdx = 0; batchIdx < group.renderBatches.Count; batchIdx++) {
                    var batch = group.renderBatches[batchIdx];
                    var batchMat = metadata.materials[checked((int)batch.materialID)];
                    var texture = texturesById[batchMat.texture1];

                    var material = GetMaterial(texture, batchMat.flags, batchMat.blendMode, batchMat.shader);
                    mats[$"{group.groupName}{batchIdx}"] = material;
                }
            }

            return mats;
        }

        public static void ProcessFlagsForMaterial(Material material, short flags, uint blendingMode)
        {
            //Flags first
            if ((flags & (short)MaterialFlags.Unlit) != (short)MaterialFlags.None)
            {
                material.shader = Shader.Find(UNLIT_SHADER);
            }

            if ((flags & (short)MaterialFlags.TwoSided) != (short)MaterialFlags.None)
            {
                material.doubleSidedGI = true;
                material.SetFloat("_Cull", 0);
            }

            //Now blend modes
            if (blendingMode == (short)BlendModes.AlphaKey)
            {
                // material.SetFloat("_AlphaClip", 1);
                material.SetFloat("_Mode", 1);
            }

            if (blendingMode == (short)BlendModes.Alpha)
            {
                material.SetFloat("_Mode", 3);
                // material.SetOverrideTag("RenderType", "Transparent");
                // material.SetFloat("_Blend", 0);
                // material.SetFloat("_Surface", 1);
                material.SetFloat("_ZWrite", 0);
            }

            if (blendingMode == (short)BlendModes.Add)
            {
                material.SetFloat("_Mode", 3);
                // material.SetOverrideTag("RenderType", "Transparent");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                // material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                material.SetFloat("_Cutoff", 0);
                // material.SetFloat("_Blend", 1);
                // material.SetFloat("_Surface", 1);
                material.SetFloat("_SrcBlend", 1);
                material.SetFloat("_DstBlend", 1);
                material.SetFloat("_ZWrite", 0);
                material.SetShaderPassEnabled("ShadowCaster", false);
            }
        }

        public static Color ProcessMaterialColors(Material material, M2Utility.M2 metadata)
        {
            int i, j, k;
            Color newColor = Color.white;
            if (metadata.skin == null || metadata.skin.textureUnits.Count <= 0)
            {
                return newColor;
            }

            for (i = 0; i < metadata.textures.Count; i++)
            {
                if (material.name == metadata.textures[i].mtlName)
                    break;
            }

            for (j = 0; j < metadata.skin.textureUnits.Count; j++)
            {
                if (metadata.skin.textureUnits[j].geosetIndex == i)
                    break;
            }

            if (j < metadata.skin.textureUnits.Count)
                k = (int)metadata.skin.textureUnits[j].colorIndex;
            else
                return newColor;

            if (k < metadata.colors.Count)
            {
                newColor.r = metadata.colors[k].color.values[0][0][0];
                newColor.g = metadata.colors[k].color.values[0][0][1];
                newColor.b = metadata.colors[k].color.values[0][0][2];
                newColor.a = 1;
            }

            return newColor;
        }

        public static Material GetTerrainMaterial(string dirName, string sectionName, ADTUtility.Tex metadata)
        {
            var assetMatPath = Path.Join(dirName, $"tex_{sectionName}.mat");

            var assetMat = AssetDatabase.LoadAssetAtPath<Material>(assetMatPath);
            if (assetMat == null)
            {
                Debug.Log($"{assetMatPath}: material does not exist, creating.");

                var textures = metadata.layers.Select((item) => AssetDatabase.LoadAssetAtPath<Texture>(item.assetPath)).ToList();
                var mask = AssetDatabase.LoadAssetAtPath<Texture>(Path.Join(dirName, $"tex_{sectionName}.png"));

                assetMat = new Material(Shader.Find("Custom/wow.unity/TerrainShader"));

                assetMat.SetTexture("_MaskTex", mask);
                assetMat.SetTexture("_Layer0Tex", textures[0]);
                if (textures.Count >= 2)
                {
                       assetMat.SetTexture("_Layer1Tex", textures[1]);
                }
                if (textures.Count >= 3)
                {
                       assetMat.SetTexture("_Layer2Tex", textures[2]);
                }
                if (textures.Count >= 4)
                {
                       assetMat.SetTexture("_Layer3Tex", textures[3]);
                }
                if (textures.Count >= 5)
                {
                       assetMat.SetTexture("_Layer4Tex", textures[4]);
                }

                AssetDatabase.CreateAsset(assetMat, assetMatPath);
                AssetDatabase.SaveAssets();
            }

            return assetMat;
        }
    }
}
