using UnityEngine;

namespace JayFos.Cloud
{
    public class RainSystem : MonoBehaviour
    {
        [SerializeField] private Material rainMaterial;
        [Range(100, 5000)]
        [SerializeField] private int particleCount = 2000;
        [SerializeField] private float rainHeight = 30f;
        [SerializeField] private float rainRadius = 20f;
        [SerializeField] private float rainSpeed = 15f;
        [SerializeField] private float rainWidth = 0.02f;
        [SerializeField] private float rainLength = 1.5f;

        private ParticleSystem rainParticles;
        private WeatherSystem weatherSystem;
        private Transform cameraTransform;

        public void Initialize(WeatherSystem weather)
        {
            weatherSystem = weather;
            cameraTransform = Camera.main?.transform;
            CreateRainParticles();
        }

        private void Update()
        {
            if (rainParticles == null || weatherSystem == null)
                return;

            float intensity = weatherSystem.CurrentRainIntensity;

            if (intensity > 0.01f)
            {
                if (!rainParticles.isPlaying)
                    rainParticles.Play();

                var emission = rainParticles.emission;
                emission.rateOverTime = particleCount * intensity;

                var velocity = rainParticles.velocityOverLifetime;
                velocity.enabled = true;
                velocity.x = weatherSystem.CurrentWindMultiplier * 0.5f;
                velocity.z = weatherSystem.CurrentWindMultiplier * 0.3f;
            }
            else
            {
                if (rainParticles.isPlaying)
                    rainParticles.Stop();
            }

            if (cameraTransform != null)
            {
                Vector3 pos = cameraTransform.position;
                pos.y += rainHeight * 0.5f;
                transform.position = pos;
            }
        }

        private void CreateRainParticles()
        {
            GameObject rainGO = new GameObject("RainParticles");
            rainGO.transform.SetParent(transform, false);

            rainParticles = rainGO.AddComponent<ParticleSystem>();

            var main = rainParticles.main;
            main.maxParticles = particleCount;
            main.startLifetime = rainHeight / rainSpeed;
            main.startSpeed = rainSpeed;
            main.startSize = new ParticleSystem.MinMaxCurve(rainWidth, rainWidth * 2f);
            main.startRotation = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.playOnAwake = false;

            var emission = rainParticles.emission;
            emission.rateOverTime = 0;

            var shape = rainParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(rainRadius * 2f, 0.1f, rainRadius * 2f);

            var renderer = rainGO.GetComponent<ParticleSystemRenderer>();
            renderer.material = rainMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = rainLength / rainSpeed;
            renderer.lengthScale = rainLength;

            var colorLifetime = rainParticles.colorOverLifetime;
            colorLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.1f), new GradientAlphaKey(0.6f, 0.9f), new GradientAlphaKey(0f, 1f) }
            );
            colorLifetime.color = gradient;

            rainParticles.Stop();
        }
    }
}
