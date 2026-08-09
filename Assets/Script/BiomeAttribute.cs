using System;

namespace JayFos.Biomes
{
    [Serializable]
    public struct BiomeAttribute
    {
        public string key;
        public float value;

        public BiomeAttribute(string key, float value)
        {
            this.key = key;
            this.value = value;
        }

        public override string ToString()
        {
            return $"{key}: {value:F3}";
        }
    }
}
