using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using WowUnity;

public class LayerInfo
{
    public uint density;
    public List<GameObject> doodads;
    public List<float> doodadWeights;
}

public class FoliageSpawner : MonoBehaviour
{
    public const int SPAWNED_PER_FRAME = 50;
    private static readonly HashSet<FoliageSpawner> spawners = new();
    private static int totalSpawnCount = 0;

    public static int TotalSpawnCount() {  return totalSpawnCount; }

    public static void RespawnAll()
    {
        var player = GameObject.FindGameObjectsWithTag("Player")[0];
        var spawnDistance = RuntimeSettings.GetSettings().foliageSpawnDistance;
        var density = RuntimeSettings.GetSettings().foliageDensityFactor;

        foreach (var s in spawners)
        {
            s.spawnDistance = spawnDistance;
            s.spawnExecuted = false;
            if (Vector3.Distance(s.distanceTest.transform.position, player.transform.position) < spawnDistance)
                s.SpawnFoliage(density);
        }
    }

    public static Texture2D RotateTexture180(Texture2D originalTexture)
    {
        var rotatedTexture = new Texture2D(originalTexture.width, originalTexture.height);

        // Get the original pixels from the texture
        var originalPixels = originalTexture.GetPixels();
        var rotatedPixels = new Color[originalPixels.Length];

        int width = originalTexture.width;
        var height = originalTexture.height;

        // Loop through each pixel and set it in the new position
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // Calculate the index for the original and rotated positions
                var originalIndex = y * width + x;
                var rotatedIndex = (height - 1 - y) * width + (width - 1 - x);

                // Set the rotated pixel
                rotatedPixels[rotatedIndex] = originalPixels[originalIndex];
            }
        }

        // Apply the rotated pixels to the new texture
        rotatedTexture.SetPixels(rotatedPixels);
        rotatedTexture.Apply();

        return rotatedTexture;
    }

    public Texture2D chunkTex;
    public int layerCount;
    public List<uint> layerDensities;
    public List<GameObject> layer0Prefabs;
    public List<float> layer0Weights;
    public List<GameObject> layer1Prefabs;
    public List<float> layer1Weights;
    public List<GameObject> layer2Prefabs;
    public List<float> layer2Weights;
    public List<GameObject> layer3Prefabs;
    public List<float> layer3Weights;
    public Bounds chunkBounds;

    private List<GameObject> spawnedObjects;
    private bool spawnExecuted = false;
    private int spawningIter = 0;
    private List<GameObject> layerContainers;
    private readonly Dictionary<int, float>[] layerPixels = new Dictionary<int, float>[4];
    private float spawnDistance;
    private GameObject distanceTest;

    void Start()
    {
        spawners.Add(this);
        spawnDistance = RuntimeSettings.GetSettings().foliageSpawnDistance;

        layerContainers = new();
        for (var i = 0; i < 4; i++)
        {
            var go = new GameObject() { name = $"layer{i}" };
            go.transform.parent = transform;
            go.transform.localPosition = new Vector3(
                chunkBounds.center.x - chunkBounds.extents.x,
                chunkBounds.center.y + chunkBounds.extents.y,
                chunkBounds.center.z - chunkBounds.extents.z
            );
            layerContainers.Add(go);

            distanceTest = new GameObject() { name = $"distance" };
            distanceTest.transform.parent = transform;
            distanceTest.transform.localPosition = new Vector3(
                chunkBounds.center.x,
                chunkBounds.center.y,
                chunkBounds.center.z
            );
        }
        spawnedObjects = new();

        var pixels = RotateTexture180(chunkTex).GetPixels();
        var pixelsBase = new Dictionary<int, float>();
        var pixelsR = new Dictionary<int, float>();
        var pixelsG = new Dictionary<int, float>();
        var pixelsB = new Dictionary<int, float>();
        for (var i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            if (pixel.r == 0 && pixel.g == 0 && pixel.b == 0)
                pixelsBase.Add(i, 1);

            if (pixel.r > 0)
                pixelsR.Add(i, pixel.r);

            if (pixel.g > 0)
                pixelsG.Add(i, pixel.g);

            if (pixel.b > 0)
                pixelsB.Add(i, pixel.b);
        }
        layerPixels[0] = pixelsBase;
        layerPixels[1] = pixelsR;
        layerPixels[2] = pixelsG;
        layerPixels[3] = pixelsB;
    }

    void Update()
    {
        var player = GameObject.FindGameObjectsWithTag("Player")[0];
        if (!spawnExecuted && Vector3.Distance(distanceTest.transform.position, player.transform.position) < spawnDistance)
            SpawnFoliage(RuntimeSettings.GetSettings().foliageDensityFactor);
    }

    private void OnDestroy()
    {
        spawners.Remove(this);
    }

    private List<(List<GameObject>, List<float>)> GetPrefabsWeights()
    {
        return new List<(List<GameObject>, List<float>)>
        {
            (layer0Prefabs, layer0Weights),
            (layer1Prefabs, layer1Weights),
            (layer2Prefabs, layer2Weights),
            (layer3Prefabs, layer3Weights),
        };
    }

    public void SetupSpawner(Texture2D chunkTex, List<LayerInfo> layersInfo, Bounds chunkBounds)
    {
        this.chunkBounds = chunkBounds;
        this.chunkTex = chunkTex;

        layerCount = layersInfo.Count;
        layerDensities = new();

        for (var lidx = 0; lidx < layerCount; lidx++)
        {
            var layerInfo = layersInfo[lidx];
            layerDensities.Add(layerInfo.density);
            switch (lidx)
            {
                case 0:
                    layer0Prefabs = layerInfo.doodads;
                    layer0Weights = layerInfo.doodadWeights;
                    break;
                case 1:
                    layer1Prefabs = layerInfo.doodads;
                    layer1Weights = layerInfo.doodadWeights;
                    break;
                case 2:
                    layer2Prefabs = layerInfo.doodads;
                    layer2Weights = layerInfo.doodadWeights;
                    break;
                case 3:
                    layer3Prefabs = layerInfo.doodads;
                    layer3Weights = layerInfo.doodadWeights;
                    break;
            }
        }
    }

    void SpawnFoliage(float factor)
    {
        spawnExecuted = true;
        StartCoroutine(SpawnFoliageOverTime(factor, ++spawningIter));
    }

    IEnumerator SpawnFoliageOverTime(float factor, int myIter)
    {
        var perFrameCount = 0;

        foreach (var s in this.spawnedObjects)
        {
            Destroy(s);
            Interlocked.Decrement(ref totalSpawnCount);
            perFrameCount++;
            if (perFrameCount % SPAWNED_PER_FRAME == SPAWNED_PER_FRAME - 1)
                yield return null;
        }

        if (layerCount == 0)
            yield break;

        perFrameCount = 0;
        var spawnedObjects = new List<GameObject>();

        var prefabsWeights = GetPrefabsWeights();

        for (var idx = 0; idx < layerCount; idx++)
        {
            var layer = layerContainers[idx];
            var (doodads, weights) = prefabsWeights[idx];

            for (var doodadIndex = 0; doodadIndex < doodads.Count; doodadIndex++)
            {
                var doodad = doodads[doodadIndex];
                var density = layerDensities[idx] * weights[doodadIndex];
                var prob = (density * 100 * factor) / (64 * 64);

                foreach (var (pixelIdx, pixelVal) in layerPixels[idx])
                {
                    if (spawningIter != myIter)
                    {
                        foreach (var s in spawnedObjects)
                        {
                            Destroy(s);
                            Interlocked.Decrement(ref totalSpawnCount);
                            perFrameCount++;
                            if (perFrameCount % SPAWNED_PER_FRAME == SPAWNED_PER_FRAME - 1)
                                yield return null;
                        }
                        yield break;
                    }

                    if (Random.value > prob * pixelVal)
                        continue;

                    var x = pixelIdx % 64;
                    var z = pixelIdx / 64;

                    var posX = (x / 64f) * (chunkBounds.size.x - 64f / chunkBounds.size.x) + Random.Range(0, 64f / chunkBounds.size.x);
                    var posZ = (z / 64f) * (chunkBounds.size.z - 64f / chunkBounds.size.z) + Random.Range(0, 64f / chunkBounds.size.z);

                    var pos = new Vector3(posX, -chunkBounds.size.y * 2, posZ);

                    var rayStart = new Vector3(
                        pos.x,
                        pos.y + 200,
                        pos.z
                    ) + layer.transform.position;

                    if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit _, 5000, RuntimeSettings.GetSettings().foliageRayPreventLayerMask))
                        continue;

                    if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 5000, 1 << RuntimeSettings.GetSettings().foliageRayLayer))
                        continue;

                    var foliage = Instantiate(doodad, hit.point, Quaternion.identity);
                    foliage.transform.RotateAround(hit.point, Vector3.up, Random.Range(0, 180));
                    foliage.transform.parent = layer.transform;
                    var scale = Random.Range(0, 0.5f);
                    foliage.transform.localScale = Vector3.one - new Vector3(scale, scale, scale);
                    foliage.isStatic = false;
                    var renderers = new List<Renderer>();

                    foreach (Transform transform in foliage.GetComponentInChildren<Transform>())
                    {
                        transform.gameObject.isStatic = false;
                        foreach (Transform subtransform in transform.GetComponentInChildren<Transform>())
                        {
                            subtransform.gameObject.isStatic = false;
                            renderers.AddRange(subtransform.gameObject.GetComponentsInChildren<Renderer>());
                        }
                    }

                    var settings = RuntimeSettings.GetSettings();
                    if (settings.foliageSetupLODs)
                    {
                        var lodGroup = foliage.AddComponent<LODGroup>();
                        var lods = lodGroup.GetLODs();
                        lods[0].renderers = renderers.ToArray();
                        lods[1].renderers = lods[0].renderers;
                        lods[2].renderers = lods[0].renderers;
                        lods[2].screenRelativeTransitionHeight = settings.foliageCullWidth / 100;
                        lodGroup.SetLODs(lods);
                    }

                    spawnedObjects.Add(foliage);
                    Interlocked.Increment(ref totalSpawnCount);
                    perFrameCount++;

                    if (perFrameCount % SPAWNED_PER_FRAME == SPAWNED_PER_FRAME - 1)
                        yield return null;
                }
            }
        }

        this.spawnedObjects = spawnedObjects;
    }
}
