using System;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

namespace HijackPoker.UI
{
    /// <summary>
    /// Keyboard shortcuts for desktop/WebGL: Space (next step), R (reset), A (auto-play).
    /// Skips input when an InputField is focused.
    /// </summary>
    public class InputHandler : MonoBehaviour
    {
        public event Action OnNextStep;
        public event Action OnReset;
        public event Action OnAutoPlayToggle;

#if UNITY_STANDALONE || UNITY_WEBGL || UNITY_EDITOR
        private void Update()
        {
            // Skip if an input field is focused
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected != null && selected.GetComponent<TMP_InputField>() != null)
                return;

            if (Input.GetKeyDown(KeyCode.Space))
                OnNextStep?.Invoke();
            else if (Input.GetKeyDown(KeyCode.R))
                OnReset?.Invoke();
            else if (Input.GetKeyDown(KeyCode.A))
                OnAutoPlayToggle?.Invoke();
        }
#endif
    }
}
