namespace JayFos.Environment
{
    using System.Collections.Generic;
    using JayFos.Cloud;
    using UnityEngine;

    public class LightningManager : MonoBehaviour
    {
        [Header("References")]
        public WeatherSystem weatherSystem;
        public EnvironmentSettings environmentSettings;

        [Header("Audio")]
        [Tooltip("Optional thunder rumble clip. If unassigned, a procedural thunder clip is generated at runtime.")]
        public AudioClip thunderClip;

        [Header("Spawn Settings")]
        [Tooltip("Min seconds between lightning strikes during Storm state.")]
        [Range(0.5f, 5f)]
        public float minStrikeInterval = 1.5f;
        [Tooltip("Max seconds between lightning strikes during Storm state.")]
        [Range(1f, 10f)]
        public float maxStrikeInterval = 4f;
        [Tooltip("Intensity of each lightning flash (affects light source).")]
        [Range(1f, 10f)]
        public float flashIntensity = 4f;
        [Tooltip("Duration of each flash in seconds.")]
        [Range(0.05f, 0.5f)]
        public float flashDuration = 0.15f;

        [Header("Strike Volume")]
        [Tooltip("Horizontal radius around camera where strikes can occur.")]
        [Range(50f, 500f)]
        public float strikeRadius = 200f;
        [Tooltip("Height above terrain where bolts originate.")]
        public float boltOriginHeight = 50f;

        private float nextStrikeTime = 0f;
        private float currentInterval = 2.5f;

        private Camera cam;
        private Transform camTransform;

        private List<LightningBolt> activeBolts = new List<LightningBolt>();
        private List<LightningBolt> boltPool = new List<LightningBolt>();
        private List<LightningFlash> flashPool = new List<LightningFlash>();
        private List<LightningAudio> audioPool = new List<LightningAudio>();
        private int poolIndex = 0;

        public System.Action<Vector3> OnLightningStrike;

        private void Awake()
        {
            cam = Camera.main;
            if (cam != null)
                camTransform = cam.transform;

            CreateBoltPool(3);
            CreateFlashPool(3);
            CreateAudioPool(3);
        }

        private void Update()
        {
            if (weatherSystem == null || environmentSettings == null)
                return;

            if (weatherSystem.CurrentState != WeatherState.Storm)
                return;

            if (Time.time >= nextStrikeTime)
            {
                currentInterval = Random.Range(minStrikeInterval, maxStrikeInterval);
                nextStrikeTime = Time.time + currentInterval;
                TriggerStrike();
            }
        }

        private void TriggerStrike()
        {
            Vector3 strikePos = GetStrikePosition();

            LightningBolt bolt = GetBoltFromPool();
            if (bolt != null)
            {
                bolt.transform.position = strikePos;
                bolt.gameObject.SetActive(true);
            }

            LightningFlash flash = GetFlashFromPool();
            if (flash != null)
            {
                flash.transform.position = strikePos;
                flash.gameObject.SetActive(true);
                flash.Initialize(flashIntensity, flashDuration);
                flash.Play();
            }

            LightningAudio audio = GetAudioFromPool();
            if (audio != null)
            {
                audio.transform.position = strikePos;
                audio.gameObject.SetActive(true);
                audio.Initialize(strikePos, strikeRadius);
                audio.Play();
            }

            OnLightningStrike?.Invoke(strikePos);
        }

        private Vector3 GetStrikePosition()
        {
            if (camTransform == null)
                return Vector3.zero;

            Vector3 strikePos = camTransform.position;
            float x = Random.Range(-1f, 1f) * strikeRadius;
            float z = Random.Range(-1f, 1f) * strikeRadius;
            strikePos.x += x;
            strikePos.z += z;
            strikePos.y = boltOriginHeight;

            return strikePos;
        }

        private void CreateBoltPool(int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject("LightningBolt");
                go.transform.SetParent(transform);
                go.transform.localPosition = Vector3.zero;
                go.SetActive(false);

                LightningBolt bolt = go.AddComponent<LightningBolt>();
                bolt.SetManager(this);

                boltPool.Add(bolt);
            }
        }

        private void CreateFlashPool(int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject("LightningFlash");
                go.transform.SetParent(transform);
                go.transform.localPosition = Vector3.zero;
                go.SetActive(false);

                LightningFlash flash = go.AddComponent<LightningFlash>();
                flash.SetManager(this);

                flashPool.Add(flash);
            }
        }

        private void CreateAudioPool(int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject("LightningAudio");
                go.transform.SetParent(transform);
                go.transform.localPosition = Vector3.zero;
                go.SetActive(false);

                LightningAudio audio = go.AddComponent<LightningAudio>();
                audio.SetManager(this);
                audio.thunderClip = thunderClip;

                audioPool.Add(audio);
            }
        }

        private LightningBolt GetBoltFromPool()
        {
            for (int i = 0; i < boltPool.Count; i++)
            {
                int index = (poolIndex + i) % boltPool.Count;
                if (!boltPool[index].gameObject.activeSelf)
                {
                    poolIndex = index;
                    return boltPool[index];
                }
            }

            return null;
        }

        private LightningFlash GetFlashFromPool()
        {
            for (int i = 0; i < flashPool.Count; i++)
            {
                int index = (poolIndex + i) % flashPool.Count;
                if (!flashPool[index].gameObject.activeSelf)
                {
                    poolIndex = index;
                    return flashPool[index];
                }
            }

            return null;
        }

        private LightningAudio GetAudioFromPool()
        {
            for (int i = 0; i < audioPool.Count; i++)
            {
                int index = (poolIndex + i) % audioPool.Count;
                if (!audioPool[index].gameObject.activeSelf)
                {
                    poolIndex = index;
                    return audioPool[index];
                }
            }

            return null;
        }

        public void OnBoltSpawned(LightningBolt bolt)
        {
            if (!activeBolts.Contains(bolt))
                activeBolts.Add(bolt);
        }

        public void OnBoltRetracted(LightningBolt bolt)
        {
            if (activeBolts.Contains(bolt))
                activeBolts.Remove(bolt);
        }

        public void ReturnFlashToPool(LightningFlash flash)
        {
            flash.gameObject.SetActive(false);
        }

        public void ReturnAudioToPool(LightningAudio audio)
        {
            audio.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (activeBolts.Count == 0)
                return;

            LightningBolt[] snapshot = activeBolts.ToArray();
            activeBolts.Clear();
            for (int i = 0; i < snapshot.Length; i++)
            {
                LightningBolt bolt = snapshot[i];
                if (bolt != null && bolt.gameObject != null)
                    bolt.gameObject.SetActive(false);
            }
        }
    }
}
