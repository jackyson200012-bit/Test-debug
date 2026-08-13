namespace JayFos.Environment
{
    using System.Collections;
    using UnityEngine;

    public class LightningAudio : MonoBehaviour
    {
        [Header("Audio")]
        public AudioClip thunderClip;

        private AudioSource audioSource;
        private Vector3 strikePosition;
        private float maxDistance;
        private LightningManager manager;

        public void SetManager(LightningManager mgr)
        {
            manager = mgr;
        }

        private void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialize = true;
            audioSource.spatialBlend = 1f;
            audioSource.playOnAwake = false;
        }

        public void Initialize(Vector3 strikePosition, float strikeRadius)
        {
            this.strikePosition = strikePosition;
            maxDistance = strikeRadius;

            Camera cam = Camera.main;
            float distance = cam != null
                ? Vector3.Distance(cam.transform.position, strikePosition)
                : 100f;
            audioSource.volume = Mathf.Clamp01(1f / distance * 3f);
            audioSource.minDistance = 10f;
            audioSource.maxDistance = maxDistance;

            if (thunderClip != null)
                audioSource.clip = thunderClip;
        }

        public void Play()
        {
            if (audioSource == null)
                return;

            if (audioSource.clip == null)
                audioSource.clip = GetOrCreateThunderClip();

            if (audioSource.clip != null)
            {
                audioSource.Play();
                StartCoroutine(AutoReturn());
            }
        }

        private static AudioClip cachedThunderClip;

        /// <summary>
        /// One-time procedural thunder rumble (brown-noise burst with exponential decay).
        /// Generated once and reused by every pooled audio object — zero per-strike allocation.
        /// </summary>
        private static AudioClip GetOrCreateThunderClip()
        {
            if (cachedThunderClip != null)
                return cachedThunderClip;

            int sampleRate = 44100;
            float duration = 1.6f;
            int samples = Mathf.RoundToInt(duration * sampleRate);
            float[] data = new float[samples];
            float last = 0f;

            for (int i = 0; i < samples; i++)
            {
                float white = UnityEngine.Random.value * 2f - 1f;
                last = (last + 0.05f * white) / 1.05f;

                float t = (float)i / samples;
                float envelope = Mathf.Pow(1f - t, 2f) * 0.9f;
                // Initial crack then decaying roll
                if (i > samples * 0.05f && i < samples * 0.25f)
                    envelope += UnityEngine.Random.value * 0.25f;

                data[i] = Mathf.Clamp(last * envelope, -1f, 1f);
            }

            cachedThunderClip = AudioClip.Create("ProceduralThunder", samples, 1, sampleRate, false);
            cachedThunderClip.SetData(data, 0);
            return cachedThunderClip;
        }

        private IEnumerator AutoReturn()
        {
            yield return new WaitForSeconds(audioSource.clip.length);
            manager.ReturnAudioToPool(this);
        }
    }
}
