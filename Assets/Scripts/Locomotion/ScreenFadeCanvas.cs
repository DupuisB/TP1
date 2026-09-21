using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LOG8704.Locomotion
{
    /// <summary>
    /// Manages full-screen camera fades (fade-to-black / fade-in) for VR comfort transitions like Blink Teleportation.
    /// Compatible with both OpenXR HMDs (Meta Quest 3) and Desktop XR Device Simulator.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenFadeCanvas : MonoBehaviour
    {
        private static ScreenFadeCanvas s_Instance;
        public static ScreenFadeCanvas Instance => s_Instance;

        [Header("Visual Elements")]
        [SerializeField] private CanvasGroup m_CanvasGroup;
        [SerializeField] private Image m_FadeImage;

        [Header("Default Timings")]
        [Tooltip("Duration in seconds for the blackout fade out.")]
        [SerializeField] private float m_DefaultFadeOutDuration = 0.08f;

        [Tooltip("Duration in seconds for the blackout fade in.")]
        [SerializeField] private float m_DefaultFadeInDuration = 0.08f;

        private Coroutine m_CurrentFadeCoroutine;

        public bool isFading => m_CurrentFadeCoroutine != null;
        public float currentAlpha => m_CanvasGroup != null ? m_CanvasGroup.alpha : 0f;

        private void Awake()
        {
            if (s_Instance == null)
            {
                s_Instance = this;
            }

            if (m_CanvasGroup == null)
            {
                m_CanvasGroup = GetComponent<CanvasGroup>();
            }

            if (m_FadeImage == null)
            {
                m_FadeImage = GetComponentInChildren<Image>();
            }

            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = 0f;
                m_CanvasGroup.blocksRaycasts = false;
                m_CanvasGroup.interactable = false;
            }
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }

        /// <summary>
        /// Executes a rapid Blink transition: fade to black, invoke callback, fade back in.
        /// </summary>
        public void BlinkFade(Action onBlackout, float fadeDuration = -1f)
        {
            float duration = fadeDuration > 0f ? fadeDuration : m_DefaultFadeOutDuration;

            if (m_CurrentFadeCoroutine != null)
            {
                StopCoroutine(m_CurrentFadeCoroutine);
            }

            m_CurrentFadeCoroutine = StartCoroutine(BlinkRoutine(onBlackout, duration));
        }

        private IEnumerator BlinkRoutine(Action onBlackout, float halfDuration)
        {
            // 1. Rapid fade out (transparent -> black)
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (m_CanvasGroup != null)
                    m_CanvasGroup.alpha = Mathf.Clamp01(elapsed / halfDuration);
                yield return null;
            }

            if (m_CanvasGroup != null)
                m_CanvasGroup.alpha = 1f;

            // 2. Perform the instantaneous teleport/action while screen is pitch black
            try
            {
                onBlackout?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ScreenFadeCanvas] Error during onBlackout callback: {ex}");
            }

            // Brief frame pause at peak blackout to let TeleportationProvider update position
            yield return null;
            yield return null;

            // 3. Rapid fade in (black -> transparent)
            float inDuration = m_DefaultFadeInDuration > 0f ? m_DefaultFadeInDuration : halfDuration;
            elapsed = 0f;
            while (elapsed < inDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (m_CanvasGroup != null)
                    m_CanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / inDuration);
                yield return null;
            }

            if (m_CanvasGroup != null)
                m_CanvasGroup.alpha = 0f;

            m_CurrentFadeCoroutine = null;
        }

        /// <summary>
        /// Helper to create and attach a ScreenFadeCanvas directly to a Camera.
        /// </summary>
        public static ScreenFadeCanvas CreateOnCamera(Camera targetCamera)
        {
            if (targetCamera == null)
            {
                Debug.LogError("[ScreenFadeCanvas] Cannot create on null camera.");
                return null;
            }

            // Check if one already exists under camera
            var existing = targetCamera.GetComponentInChildren<ScreenFadeCanvas>(true);
            if (existing != null)
                return existing;

            var fadeObj = new GameObject("ScreenFadeCanvas");
            fadeObj.transform.SetParent(targetCamera.transform, false);
            fadeObj.transform.localPosition = new Vector3(0f, 0f, 0.15f);
            fadeObj.transform.localRotation = Quaternion.identity;
            fadeObj.transform.localScale = Vector3.one * 0.001f;

            var canvas = fadeObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = targetCamera;
            canvas.sortingOrder = 32767; // Draw on top

            var canvasScaler = fadeObj.AddComponent<CanvasScaler>();
            canvasScaler.dynamicPixelsPerUnit = 10f;

            var canvasGroup = fadeObj.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            var imageObj = new GameObject("FadeImage");
            imageObj.transform.SetParent(fadeObj.transform, false);
            var rect = imageObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = new Vector2(4000f, 4000f);
            rect.anchoredPosition = Vector2.zero;

            var img = imageObj.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;

            // Ensure fade draws over all 3D geometry regardless of depth
            var defaultShader = Shader.Find("UI/Default");
            if (defaultShader != null)
            {
                var overlayMat = new Material(defaultShader);
                overlayMat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
                img.material = overlayMat;
            }

            var comp = fadeObj.AddComponent<ScreenFadeCanvas>();
            comp.m_CanvasGroup = canvasGroup;
            comp.m_FadeImage = img;

            return comp;
        }
    }
}
