using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace WowUnity.Foliage
{
    public class LayerInfo
    {
        public uint density;
        public List<GameObject> doodads;
        public List<float> doodadWeights;
    }

    public readonly struct CalculatedObject
    {
        public CalculatedObject(int layerIndex, GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            LayerIndex = layerIndex;
            Prefab = prefab;
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public int LayerIndex { get; }
        public GameObject Prefab { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 Scale { get; }
    }

    public class FoliageSpawner : MonoBehaviour
    {
        public const int SPAWNED_PER_FRAME = 50;
        private static readonly HashSet<FoliageSpawner> spawners = new();
        private static int totalSpawnCount = 0;
        private static ConcurrentDictionary<int, ConcurrentStack<GameObject>> foliagePool = new();

        public static int TotalSpawnCount() { return totalSpawnCount; }

        public static void RespawnAll()
        {
            var foliagePool = FoliageSpawner.foliagePool;
            FoliageSpawner.foliagePool = new();

            foreach (var (prefabId, stack) in foliagePool)
            {
                foreach (var instance in stack)
                {
                    RemoveFoliage((prefabId, instance), true);
                }
            }

            foreach (var s in spawners)
            {
                if (s.spawnedObjects != null)
                {
                    foreach (var instance in s.spawnedObjects)
                    {
                        RemoveFoliage(instance, true);
                    }
                }

                s.calculatedObjects = null;
                s.spawnedObjects = null;
                s.spawnExecuted = false;
            }
        }

        public static GameObject GetFoliage(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, Transform parent)
        {
            var pool = foliagePool.GetOrAdd(prefab.GetInstanceID(), (_) => { return new ConcurrentStack<GameObject>(); });

            if (pool.TryPop(out GameObject foliage))
            {
                foliage.transform.SetPositionAndRotation(position, rotation);
                foliage.transform.parent = parent;
                foliage.transform.localScale = scale;
                foliage.SetActive(true);
                return foliage;
            }

            var settings = RuntimeSettings.GetSettings();

            foliage = Instantiate(prefab, position, rotation);
            foliage.transform.parent = parent;
            foliage.transform.localScale = scale;

            foliage.isStatic = false;
            var renderers = new List<Renderer>();

            foreach (Transform transform in foliage.GetComponentInChildren<Transform>())
            {
                transform.gameObject.isStatic = false;
                foreach (Transform subtransform in transform.GetComponentInChildren<Transform>())
                {
                    subtransform.gameObject.isStatic = false;
                    foreach (var renderer in subtransform.gameObject.GetComponentsInChildren<Renderer>())
                    {
                        renderer.shadowCastingMode = settings.foliageCastsShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                        renderers.Add(renderer);
                    }
                }
            }

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

            Interlocked.Increment(ref totalSpawnCount);
            return foliage;
        }

        public static void RemoveFoliage((int, GameObject) item, bool delete = false)
        {
            if (delete)
            {
                Destroy(item.Item2);
                Interlocked.Decrement(ref totalSpawnCount);
            }
            else
            {
                if (!foliagePool.TryGetValue(item.Item1, out ConcurrentStack<GameObject> pool))
                    return;

                item.Item2.SetActive(false);
                pool.Push(item.Item2);
            }
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

        private List<(int, GameObject)> spawnedObjects;
        private List<CalculatedObject> calculatedObjects;
        private bool spawnExecuted = false;
        private int spawningIter = 0;
        private List<GameObject> layerContainers;
        private readonly Dictionary<int, float>[] layerPixels = new Dictionary<int, float>[4];
        private GameObject distanceTest;
        private RuntimeSettings settings;

        void Start()
        {
            spawners.Add(this);
            settings = RuntimeSettings.GetSettings();

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
            }

            distanceTest = new GameObject() { name = $"distance" };
            distanceTest.transform.parent = transform;
            distanceTest.transform.localPosition = new Vector3(
                chunkBounds.center.x,
                chunkBounds.center.y,
                chunkBounds.center.z
            );

            spawnedObjects = new();

            var pixels = Utils.RotateTexture180(chunkTex).GetPixels();
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
            var distance = Vector3.Distance(distanceTest.transform.position, player.transform.position);

            if (distance < settings.foliageSpawnDistance)
                SpawnFoliage(settings.foliageDensityFactor);
            else if (distance > settings.foliagePoolDistance)
                DespawnFoliage();
        }

        private void OnDestroy()
        {
            spawners.Remove(this);
        }

        private (List<GameObject>, List<float>)[] GetPrefabsAndWeights()
        {
            return new (List<GameObject>, List<float>)[4]
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
            if (spawnExecuted || layerCount == 0)
                return;

            spawnExecuted = true;
            if (calculatedObjects == null)
                StartCoroutine(SpawnNewFoliageJob(factor, ++spawningIter));
            else
                StartCoroutine(SpawnCalculatedFoliageJob(++spawningIter));
        }

        IEnumerator SpawnCalculatedFoliageJob(int myIter)
        {
            yield return StartCoroutine(DespawnFoliageJob(this.spawnedObjects));

            var perFrameCount = 0;
            var spawnedObjects = new List<(int, GameObject)>();

            foreach (var obj in calculatedObjects)
            {
                if (spawningIter != myIter)
                {
                    StartCoroutine(DespawnFoliageJob(spawnedObjects, true));
                    yield break;
                }

                var layer = layerContainers[obj.LayerIndex];
                spawnedObjects.Add((obj.Prefab.GetInstanceID(), GetFoliage(obj.Prefab, obj.Position, obj.Rotation, obj.Scale, layer.transform)));

                perFrameCount++;
                if (perFrameCount % SPAWNED_PER_FRAME == SPAWNED_PER_FRAME - 1)
                    yield return null;
            }

            this.spawnedObjects = spawnedObjects;
        }

        IEnumerator SpawnNewFoliageJob(float factor, int myIter)
        {
            yield return StartCoroutine(DespawnFoliageJob(this.spawnedObjects));

            var perFrameCount = 0;
            var prefabsWeights = GetPrefabsAndWeights();
            var calculatedObjects = new List<CalculatedObject>();
            var spawnedObjects = new List<(int, GameObject)>();

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
                            StartCoroutine(DespawnFoliageJob(spawnedObjects, true));
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

                        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit _, 5000, settings.foliageRayPreventLayerMask))
                            continue;

                        if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 5000, 1 << settings.foliageRayLayer))
                            continue;

                        var scaleF = Random.Range(0.5f, 1f);
                        var scale = new Vector3(scaleF, scaleF, scaleF);
                        var foliage = GetFoliage(doodad, hit.point, Quaternion.identity, scale, layer.transform);
                        foliage.transform.RotateAround(hit.point, Vector3.up, Random.Range(0, 180));
                        spawnedObjects.Add((doodad.GetInstanceID(), foliage));
                        calculatedObjects.Add(new CalculatedObject(idx, doodad, hit.point, foliage.transform.rotation, scale));

                        perFrameCount++;
                        if (perFrameCount % SPAWNED_PER_FRAME == SPAWNED_PER_FRAME - 1)
                            yield return null;
                    }
                }
            }

            this.spawnedObjects = spawnedObjects;
            this.calculatedObjects = calculatedObjects;
        }

        void DespawnFoliage()
        {
            spawnExecuted = false;
            if (this.spawnedObjects == null)
                return;

            var spawnedObjects = this.spawnedObjects;
            this.spawnedObjects = null;
            StartCoroutine(DespawnFoliageJob(spawnedObjects));
        }

        IEnumerator DespawnFoliageJob(List<(int, GameObject)> spawnedObjects, bool delete = false)
        {
            if (spawnedObjects == null)
                yield break;

            var perFrameCount = 0;
            foreach (var s in spawnedObjects)
            {
                RemoveFoliage(s, delete);
                perFrameCount++;
                if (perFrameCount % SPAWNED_PER_FRAME == SPAWNED_PER_FRAME - 1)
                    yield return null;
            }
        }
    }
}