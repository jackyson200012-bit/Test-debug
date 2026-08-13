namespace JayFos.Environment
{
    using JayFos.Cloud;
    using JayFos.Biomes;
    using UnityEngine;

    public class SnowSystem : MonoBehaviour
    {
        [Header("References")]
        public WeatherSystem weatherSystem;
        [System.NonSerialized]
        public BiomeMap biomeMap;

        [Header("Snow Parameters")]
        [Tooltip("Maximum number of snow particles.")]
        [Range(100, 2000)]
        public int snowParticleCount = 1000;
        [Tooltip("Height of the snow volume in world units.")]
        [Range(5f, 100f)]
        public float snowHeight = 30f;
        [Tooltip("Horizontal radius of the snow volume.")]
        [Range(5f, 100f)]
        public float snowRadius = 20f;
        [Tooltip("Base fall speed of snowflakes.")]
        [Range(1f, 10f)]
        public float snowSpeed = 5f;
        [Tooltip("Minimum particle size.")]
        [Range(0.01f, 0.1f)]
        public float snowflakeSize = 0.03f;
        [Tooltip("Material with snowflake texture (white, soft, low alpha).")]
        public Material snowMaterial;

        [Header("Temperature")]
        [Tooltip("Temperature ceiling (0-1 scale). Snow activates when the effective temperature (global or biome-derived) is below this value. Higher = more snow conditions.")]
        [Range(0f, 1f)]
        public float snowThreshold = 0.5f;

        private ParticleSystem mainPS;
        private ParticleSystemRenderer psRenderer;
        private ParticleSystem.EmissionModule emission;
        private ParticleSystem.VelocityOverLifetimeModule velocity;
        private ParticleSystem.MainModule main;

        private Transform cameraRef;

        private void Awake()
        {
            var cam = Camera.main;
            if (cam != null)
                cameraRef = cam.transform;

            CreateSnowParticles();
        }

        private void LateUpdate()
        {
            if (mainPS == null || weatherSystem == null)
                return;

            float intensity = weatherSystem.CurrentSnowIntensity * 0.7f;

            if (intensity > 0.007f)
            {
                if (!mainPS.isPlaying)
                    mainPS.Play();

                emission.rateOverTime = snowParticleCount * intensity;

                float wind = weatherSystem.CurrentWindMultiplier;
                velocity.x = wind * 0.5f;
                velocity.z = wind * 0.3f;
            }
            else
            {
                if (mainPS.isPlaying)
                    mainPS.Stop();
            }

            if (cameraRef != null)
            {
                Vector3 pos = cameraRef.position;
                pos.y += snowHeight * 0.5f;
                transform.position = pos;
            }
        }

        private void CreateSnowParticles()
        {
            GameObject snowGO = new GameObject("SnowParticles");
            snowGO.transform.SetParent(transform, false);

            mainPS = snowGO.AddComponent<ParticleSystem>();

            main = mainPS.main;
            main.maxParticles = snowParticleCount;
            main.simulationSpace = ParticleSystemSimulationSpace.Custom;
            if (cameraRef != null)
                main.customSimulationSpace = cameraRef;
            main.duration = 1f;
            main.startDelay = 0f;
            main.startLifetime = snowHeight / snowSpeed;
            main.startSpeed = snowSpeed;
            main.startSize = snowflakeSize;
            main.startRotation = 0f;
            main.gravityModifier = 0f;
            main.playOnAwake = false;

            emission = mainPS.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = mainPS.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(snowRadius * 2f, 0.1f, snowRadius * 2f);

            velocity = mainPS.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(0.5f, 0.5f);
            velocity.y = -snowSpeed;
            velocity.z = new ParticleSystem.MinMaxCurve(0.3f, 0.3f);

            psRenderer = snowGO.GetComponent<ParticleSystemRenderer>();
            if (snowMaterial == null)
            {
                snowMaterial = CreateDefaultSnowMaterial();
            }
            psRenderer.material = snowMaterial;
            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            psRenderer.velocityScale = snowflakeSize / snowSpeed;
            psRenderer.sortingFudge = 0.1f;

            var colorLifetime = mainPS.colorOverLifetime;
            colorLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.1f), new GradientAlphaKey(0.7f, 0.9f), new GradientAlphaKey(0f, 1f) }
            );
            colorLifetime.color = gradient;

            mainPS.Stop();
        }

        private Material CreateDefaultSnowMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            Material mat = new Material(shader);
            mat.name = "DefaultSnowMaterial";
            mat.color = new Color(1f, 1f, 1f, 0.65f);
            return mat;
        }
    }
}
