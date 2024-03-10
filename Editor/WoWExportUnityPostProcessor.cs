using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;
using WowUnity;

public class WoWExportUnityPostprocessor : AssetPostprocessor
{
    public override int GetPostprocessOrder()
    {
        return 9001; // must be after unitys post processor so it doesn't overwrite our own stuff
    }

    public override uint GetVersion()
    {
        return 1;
    }

    static private bool ValidAsset(string path)
    {
        if (!path.Contains(".obj"))
            return false;
        if (path.Contains(".phys.obj"))
            return false;

        if (!File.Exists(Path.GetDirectoryName(path) + "/" + Path.GetFileNameWithoutExtension(path) + ".json")) {
            return false;
        }

        return true;
    }

    public void OnPreprocessTexture()
    {
        bool match = Regex.IsMatch(assetPath, @"tex_\d{2}_\d{2}_\d{1,3}(?=\.png)");

        if (!match)
        {
            return;
        }

        TextureImporter textureImporter = (TextureImporter)assetImporter;
        textureImporter.textureType = TextureImporterType.Default;
        textureImporter.wrapMode = TextureWrapMode.Clamp;
        textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
        textureImporter.filterMode = FilterMode.Bilinear;
        textureImporter.mipmapEnabled = false;
        textureImporter.sRGBTexture = false;
    }

    public void OnPreprocessModel()
    {
        if (!ValidAsset(assetPath))
        {
            return;
        }

        Debug.Log($"{assetPath}: processing wow model");

        ModelImporter modelImporter = assetImporter as ModelImporter;

        modelImporter.bakeAxisConversion = true;
        modelImporter.generateSecondaryUV = true;
        modelImporter.secondaryUVMarginMethod = ModelImporterSecondaryUVMarginMethod.Calculate;
        modelImporter.secondaryUVMinLightmapResolution = 16;
        modelImporter.secondaryUVMinObjectScale = 1;

        modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;
    }

    public void OnPostprocessModel(GameObject gameObject)
    {
        if (!ValidAsset(assetPath))
        {
            return;
        }

        GameObject physicsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath.Replace(".obj", ".phys.obj"));
        MeshRenderer[] childRenderers = gameObject.GetComponentsInChildren<MeshRenderer>();

        if (physicsPrefab == null || physicsPrefab.GetComponentInChildren<MeshFilter>() == null)
        {
            foreach (MeshRenderer child in childRenderers)
            {
                child.gameObject.AddComponent<MeshCollider>();
            }
        }
        else
        {
            GameObject collider = new GameObject();
            collider.transform.SetParent(gameObject.transform);
            collider.name = "Collision";
            MeshFilter collisionMesh = physicsPrefab.GetComponentInChildren<MeshFilter>();
            MeshCollider parentCollider = collider.AddComponent<MeshCollider>();
            parentCollider.sharedMesh = collisionMesh.sharedMesh;
        }
    }

    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        Debug.Log(string.Format("postproc all: imported {0} assets", importedAssets.Length));

        foreach (string path in importedAssets)
        {
            if (ValidAsset(path))
            {
                AssetConversionManager.QueueMetadata(path);
            }

            //ADT/WMO Item Collection Queue
            if (Path.GetFileName(path).Contains("_ModelPlacementInformation.csv"))
            {
                ItemCollectionUtility.QueuePlacementData(path);
            }

            //ADT Liquid Volume Queue
            if (Regex.IsMatch(path, @"liquid_\d{2}_\d{2}(?=\.json)"))
            {
                LiquidUtility.QueueLiquidData(path);
            }
        }

        EditorApplication.update += AssetConversionManager.ProcessAssets;
    }
}
