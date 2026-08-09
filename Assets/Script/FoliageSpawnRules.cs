using UnityEngine;
using System;

namespace JayFos.Foliage
{
    [CreateAssetMenu(fileName = "FoliageSpawnRules", menuName = "World/Foliage Spawn Rules")]
    public class FoliageSpawnRules : ScriptableObject
    {
        [Header("Spawn Rules")]
        public FoliageSpawnRule[] rules;

        public FoliageSpawnRule[] GetRules()
        {
            if (rules != null && rules.Length > 0)
                return rules;

            return new FoliageSpawnRule[0];
        }
    }
}
