using UnityEngine;

namespace WowUnity
{
    [CreateAssetMenu(fileName = "FoliageSettings", menuName = "wow.unity/FoliageSettings", order = 1)]
    public class FoliageSettings : ScriptableObject
    {
        public int rayLayer = 0;
        public int rayPreventLayerMask = 0;
        public int spawnLayer = -1;
        public float spawnDistance = 70f;
        public float poolDistance = 71f;
        public float densityFactor = 0.6f;
        public bool castsShadows = true;
    }
}