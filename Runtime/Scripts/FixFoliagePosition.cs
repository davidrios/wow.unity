using UnityEngine;
using WowUnity;

public class FixFoliagePosition : MonoBehaviour
{
    void Start()
    {
        foreach (Transform layer in gameObject.transform)
        {
            foreach (Transform child in layer)
            {
                var pos = child.position;
                var rayStart = new Vector3(
                    pos.x,
                    pos.y + 200,
                    pos.z
                );

                int terrainLayer = 1 << RuntimeSettings.GetSettings().foliageRayLayer;

                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit _, 5000, 0xfffffff & (~terrainLayer)))
                    child.gameObject.SetActive(false);

                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 5000, terrainLayer))
                    child.localPosition += new Vector3(0, 200 - hit.distance, 0);
            }
        }
    }
}
