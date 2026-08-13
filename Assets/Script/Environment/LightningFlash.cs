namespace JayFos.Environment
{
    using System.Collections;
    using UnityEngine;

    public class LightningFlash : MonoBehaviour
    {
        private Light flashLight;
        private float intensity;
        private float duration;
        private LightningManager manager;

        public void SetManager(LightningManager mgr)
        {
            manager = mgr;
        }

        public void Initialize(float flashIntensity, float flashDuration)
        {
            intensity = flashIntensity;
            duration = flashDuration;

            if (flashLight == null)
            {
                flashLight = gameObject.AddComponent<Light>();
                flashLight.type = LightType.Point;
                flashLight.color = new Color(0.9f, 0.95f, 1f);
            }

            flashLight.intensity = intensity;
            flashLight.range = 60f;

            StartCoroutine(FlashFade());
        }

        private IEnumerator FlashFade()
        {
            float elapsed = 0f;
            float fadeTime = duration * 0.6f;

            while (elapsed < fadeTime)
            {
                flashLight.intensity = Mathf.Lerp(intensity, 0f, elapsed / fadeTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            flashLight.intensity = 0f;
            manager.ReturnFlashToPool(this);
        }

        public void Play()
        {
        }
    }
}
