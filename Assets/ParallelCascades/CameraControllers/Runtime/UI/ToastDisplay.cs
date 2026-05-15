using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace ParallelCascades.CameraControllers.Runtime.UI
{
    public class ToastDisplay : MonoBehaviour
    {
        [SerializeField] private float m_displayDuration = 1.5f;
        [SerializeField] private float m_fadeTime = 0.5f;
        [SerializeField] private UIDocument m_toastDocument;

        private VisualElement m_labelBackground;
        private Label m_displayLabel;

        private Coroutine m_fadeCoroutine;

        private void OnEnable()
        {
            m_labelBackground = m_toastDocument.rootVisualElement.Q<VisualElement>("label-background");
            m_displayLabel = m_toastDocument.rootVisualElement.Q<Label>("label");
            m_labelBackground.style.opacity = 0;
        }

        public void DisplayText(string text)
        {
            if (m_fadeCoroutine != null)
            {
                StopCoroutine(m_fadeCoroutine);
            }

            m_displayLabel.text = text;
            m_labelBackground.style.opacity = 1;
            m_fadeCoroutine = StartCoroutine(FadeOut());
        }

        private IEnumerator FadeOut()
        {
            yield return new WaitForSeconds(m_displayDuration);

            float elapsed = 0f;
            while (elapsed < m_fadeTime)
            {
                elapsed += Time.deltaTime;
                m_labelBackground.style.opacity = 1f - (elapsed / m_fadeTime);
                yield return null;
            }

            m_labelBackground.style.opacity = 0f;
            m_fadeCoroutine = null;
        }
    }
}