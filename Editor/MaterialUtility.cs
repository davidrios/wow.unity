using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
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
            var matName = $"{texture.uniqMtlName}_TF{texture.flag}_F{flags}_B{blendingMode}_S{shader}";
            var assetMatPath = $"Assets/Materials/wow/{matName}.mat";

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

        public static Material ProcessADTMaterial(MaterialDescription description, Material material, string modelImportPath)
        {
            material.shader = Shader.Find(ADT_CHUNK_SHADER);

            TexturePropertyDescription textureProperty;
            if (description.TryGetProperty("DiffuseColor", out textureProperty) && textureProperty.texture != null)
            {
                material.SetTexture("_BaseMap", textureProperty.texture);
            }

            LoadMetadataAndConfigureADT(material, modelImportPath);

            return material;
        }

        public static void LoadMetadataAndConfigureADT(Material mat, string assetPath)
        {
            string jsonFilePath = Path.GetDirectoryName(assetPath) + Path.DirectorySeparatorChar + mat.name + ".json";
            var sr = new StreamReader(Application.dataPath.Replace("Assets", "") + jsonFilePath);
            var fileContents = sr.ReadToEnd();
            sr.Close();

            TerrainMaterialGenerator.Chunk newChunk = JsonUtility.FromJson<TerrainMaterialGenerator.Chunk>(fileContents);

            Vector4 scaleVector = new Vector4();
            TerrainMaterialGenerator.Layer currentLayer;
            for (int i = 0; i < newChunk.layers.Count; i++)
            {
                currentLayer = newChunk.layers[i];
                string texturePath = Path.Combine(Path.GetDirectoryName(@assetPath), @currentLayer.file);
                texturePath = Path.GetFullPath(texturePath);
                texturePath = texturePath.Substring(texturePath.IndexOf($"Assets{Path.DirectorySeparatorChar}"));

                Texture2D layerTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture2D));
                mat.SetTexture("Layer_" + i, layerTexture);
                scaleVector[i] = currentLayer.scale;
            }

            mat.SetVector("Scale", scaleVector);
        }
    }
}
