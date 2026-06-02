using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Core.Interaction;
using System.Collections;
using UnityEngine;

namespace Fusion.XR.Shared.Locomotion
{
    /**
     * Allow to fade in / fade out a black overlay in front of the user
     * Used in locomotion, loading, ...
     */
    public class Fader : MonoBehaviour, ICameraFader
    {
        [Header("Fader description")]
        [Tooltip("The actual renderer to show/hide/fade")]
        public Renderer target;
        public Color fadeColor = Color.black;
        public float startFadeLevel = 0;
        public string colorNameMaterialProperty = "_Color";

        [Header("Blink default durations")]
        public float blinkDurationIn = 0.1f;
        public float blinkDurationSpentIn = 0.1f;
        public float blinkDurationOut = 0.1f;

        const string FADER_SHADER_NAME = "Unlit/Fader";
        const string SHADER_COLLECTION_NAME = "FaderShaderCollection";

        private void Awake()
        {
            if (target == null)
            {
                CreateFader();
            }
        }

        void CreateFader()
        {
            var faderTargetGameObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
            faderTargetGameObject.transform.parent = transform;
            faderTargetGameObject.transform.localScale = 0.5f * Vector3.one;
            faderTargetGameObject.transform.localPosition = new Vector3(0, 0, 0.02f);
            faderTargetGameObject.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            target = faderTargetGameObject.GetComponent<Renderer>();
            target.material = new Material(Shader.Find(FADER_SHADER_NAME));
            target.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            faderTargetGameObject.SetActive(false);
        }

        // Start is called before the first frame update
        void Start()
        {
            Camera camera = GetComponent<Camera>();
            target.transform.localPosition = new Vector3(0, 0, camera.nearClipPlane + 0.01f);
            SetFade(startFadeLevel);

        }

        [ContextMenu("Blink")]
        private void LaunchBlink()
        {

            StartCoroutine(Blink());
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            ValidationUtils.SceneEditionValidate(gameObject, () => {
                var shader = Shader.Find(FADER_SHADER_NAME);
                var graphicsSettings = new UnityEditor.SerializedObject(UnityEngine.Rendering.GraphicsSettings.GetGraphicsSettings());
                UnityEditor.SerializedProperty alwaysIncludedShaders = graphicsSettings.FindProperty("m_AlwaysIncludedShaders");
                bool found = false;
                for (int i = 0; i < alwaysIncludedShaders.arraySize; i++)
                {
                    var s = alwaysIncludedShaders.GetArrayElementAtIndex(i);
                    if (s?.objectReferenceValue == shader)
                    {
                        found = true;

                        break;
                    }
                }

                if (found == false)
                {
                    UnityEditor.SerializedProperty preloadedShaderCollections = graphicsSettings.FindProperty("m_PreloadedShaders");
                    for (int i = 0; i < preloadedShaderCollections.arraySize; i++)
                    {
                        var c = preloadedShaderCollections.GetArrayElementAtIndex(i);
                        if (c?.objectReferenceValue is ShaderVariantCollection collection && collection.name == SHADER_COLLECTION_NAME)
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (found == false)
                {
                    Debug.LogWarning($"[Fader] To be sure that the fader shader is included in builds,  either add {FADER_SHADER_NAME} to Always included shaders, or add {SHADER_COLLECTION_NAME} to the preloaded shaders in graphics settings");
                }
            });
#endif
        }

        public IEnumerator Blink(float durationIn = ICameraFader.USE_DEFAULT_DURATION, float durationSpentIn = ICameraFader.USE_DEFAULT_DURATION, float durationOut = ICameraFader.USE_DEFAULT_DURATION)
        {
            if (durationIn == ICameraFader.USE_DEFAULT_DURATION) durationIn = blinkDurationIn;
            if (durationSpentIn == ICameraFader.USE_DEFAULT_DURATION) durationSpentIn = blinkDurationSpentIn;
            if (durationOut == ICameraFader.USE_DEFAULT_DURATION) durationOut = blinkDurationOut;
            yield return FadeIn(durationIn);
            yield return WaitBlinkDuration(durationSpentIn);
            yield return FadeOut(durationOut);
        }

        public IEnumerator WaitBlinkDuration(float durationSpentIn = ICameraFader.USE_DEFAULT_DURATION)
        {
            if (durationSpentIn == ICameraFader.USE_DEFAULT_DURATION) durationSpentIn = blinkDurationSpentIn;
            yield return new WaitForSeconds(durationSpentIn);
        }

        public void SetFade(float level)
        {
            if (target)
            {
                Color color = fadeColor;
                color.a = level;
                target.material.SetColor(colorNameMaterialProperty, color);
                if (level == 0)
                {
                    target.gameObject.SetActive(false);
                }
                else if (!target.gameObject.activeSelf)
                {
                    target.gameObject.SetActive(true);
                }
            }
        }

        float fadeRequestId = 0;
        public IEnumerator Fade(float duration, float sourceAlpha = 1, float targetAlpha = 0)
        {
            float durationMS = 1000f * duration;
            fadeRequestId = Time.realtimeSinceStartup;
            float currentRequestId = fadeRequestId;
            float elapsed = 0;
            int step = 10;
            float stepS = ((float)step) / 1000f;
            SetFade(sourceAlpha);
            while (elapsed < durationMS && currentRequestId == fadeRequestId)
            {
                float level = Mathf.Lerp(sourceAlpha, targetAlpha, elapsed / durationMS);
                SetFade(level);
                yield return new WaitForSeconds(stepS);
                elapsed += step;
            }
            SetFade(targetAlpha);
        }

        public IEnumerator FadeOut(float duration = ICameraFader.USE_DEFAULT_DURATION)
        {
            if (duration == ICameraFader.USE_DEFAULT_DURATION) duration = blinkDurationOut;
            yield return Fade(duration, 1, 0);
        }

        public IEnumerator FadeIn(float duration = ICameraFader.USE_DEFAULT_DURATION)
        {
            if (duration == ICameraFader.USE_DEFAULT_DURATION) duration = blinkDurationIn;
            yield return Fade(duration, 0, 1);
        }

        public void AnimateFadeOut(float duration = ICameraFader.USE_DEFAULT_DURATION)
        {
            if (duration == ICameraFader.USE_DEFAULT_DURATION) duration = blinkDurationOut;
            if (isActiveAndEnabled) StartCoroutine(FadeOut(duration));
        }
        public void AnimateFadeIn(float duration = ICameraFader.USE_DEFAULT_DURATION)
        {
            if (duration == ICameraFader.USE_DEFAULT_DURATION) duration = blinkDurationIn;
            if (isActiveAndEnabled) StartCoroutine(FadeIn(duration));
        }
    }
}

