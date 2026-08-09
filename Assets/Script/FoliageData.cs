using System.Collections.Generic;
using UnityEngine;

namespace JayFos.Foliage
{
    public struct FoliageData
    {
        public List<Vector3> placementPoints;
        public int[] typeIndices;
        public float[] densityWeights;

        public int totalCount;
        public Dictionary<int, int> typeCounts;

        public static readonly FoliageData Empty = new FoliageData();

        public bool IsEmpty => placementPoints == null || placementPoints.Count == 0;

        public int GetTotalCount()
        {
            if (totalCount != 0) return totalCount;
            return placementPoints != null ? placementPoints.Count : 0;
        }
    }
}
