using System;
using System.Collections;
using UnityEngine;

namespace HijackPoker.Animation
{
    public enum EaseType
    {
        Linear,
        SmoothStep,
        EaseOutBack,
        EaseInOutQuad,
    }

    public class TweenHandle
    {
        internal Coroutine Coroutine;

        /// <summary>
        /// Action to run when Cancel() is called — should set the target to its final state.
        /// Publicly settable so views can customize snap behavior for compound animations.
        /// </summary>
        public Action SnapToFinal { get; set; }

        private bool _isComplete;
        private Action _onComplete;

        public bool IsComplete => _isComplete;

        public TweenHandle OnComplete(Action callback)
        {
            _onComplete = callback;
            return this;
        }

        public void Cancel()
        {
            if (_isComplete) return;
            _isComplete = true;
            if (Coroutine != null)
                Tweener.StopTween(Coroutine);
            SnapToFinal?.Invoke();
        }

        internal void MarkComplete()
        {
            if (_isComplete) return;
            _isComplete = true;
            _onComplete?.Invoke();
        }
    }

    /// <summary>
    /// Hosts coroutines for the static Tweener class.
    /// </summary>
    public class TweenRunner : MonoBehaviour { }

    /// <summary>
    /// Static tween utility. Creates coroutine-based animations via a shared TweenRunner.
    /// All methods return cancellable TweenHandles.
    /// </summary>
    public static class Tweener
    {
        /// <summary>
        /// Global speed multiplier for all tween durations. Higher = faster animations.
        /// </summary>
        public static float SpeedMultiplier = 1f;

        private static TweenRunner _runner;

        [UnityEngine.RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _runner = null; SpeedMultiplier = 1f; }

        private static TweenRunner GetRunner()
        {
            if (_runner == null)
            {
                var go = new GameObject("[TweenRunner]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideInHierarchy;
                _runner = go.AddComponent<TweenRunner>();
            }
            return _runner;
        }

        internal static void StopTween(Coroutine coroutine)
        {
            if (_runner != null && coroutine != null)
                _runner.StopCoroutine(coroutine);
        }

        // ── Tween Factories ────────────────────────────────────────────

        public static TweenHandle TweenFloat(float from, float to, float duration,
            Action<float> onUpdate, EaseType ease = EaseType.SmoothStep)
        {
            var handle = new TweenHandle { SnapToFinal = () => onUpdate(to) };
            handle.Coroutine = GetRunner().StartCoroutine(
                FloatRoutine(from, to, duration, onUpdate, ease, handle));
            return handle;
        }

        public static TweenHandle TweenColor(Color from, Color to, float duration,
            Action<Color> onUpdate, EaseType ease = EaseType.SmoothStep)
        {
            var handle = new TweenHandle { SnapToFinal = () => onUpdate(to) };
            handle.Coroutine = GetRunner().StartCoroutine(
                ColorRoutine(from, to, duration, onUpdate, ease, handle));
            return handle;
        }

        /// <summary>
        /// Continuously pulses a float between min and max (e.g. for glow alpha).
        /// Runs forever until cancelled.
        /// </summary>
        public static TweenHandle PulseGlow(Action<float> onUpdate,
            float min, float max, float cycleDuration)
        {
            var handle = new TweenHandle { SnapToFinal = () => onUpdate(0f) };
            handle.Coroutine = GetRunner().StartCoroutine(
                PulseGlowRoutine(onUpdate, min, max, cycleDuration, handle));
            return handle;
        }

        /// <summary>
        /// Scale pop: 0 -> overshoot -> 1 over duration.
        /// </summary>
        public static TweenHandle ScalePop(Transform target, float duration,
            float overshoot = 1.2f)
        {
            var handle = new TweenHandle
            {
                SnapToFinal = () => { if (target != null) target.localScale = Vector3.one; }
            };
            handle.Coroutine = GetRunner().StartCoroutine(
                ScalePopRoutine(target, duration, overshoot, handle));
            return handle;
        }

        /// <summary>
        /// Card flip: scaleX 1 -> 0, invoke midFlipAction (swap content), scaleX 0 -> 1.
        /// </summary>
        public static TweenHandle FlipCard(RectTransform rt, Action midFlipAction,
            float duration = 0.3f)
        {
            var handle = new TweenHandle
            {
                SnapToFinal = () =>
                {
                    midFlipAction();
                    if (rt != null) rt.localScale = Vector3.one;
                }
            };
            handle.Coroutine = GetRunner().StartCoroutine(
                FlipCardRoutine(rt, midFlipAction, duration, handle));
            return handle;
        }

        /// <summary>
        /// Tweens a RectTransform's anchoredPosition between two points.
        /// </summary>
        public static TweenHandle TweenPosition(RectTransform rt, Vector2 from, Vector2 to,
            float duration, EaseType ease = EaseType.SmoothStep)
        {
            var handle = new TweenHandle
            {
                SnapToFinal = () => { if (rt != null) rt.anchoredPosition = to; }
            };
            handle.Coroutine = GetRunner().StartCoroutine(
                PositionRoutine(rt, from, to, duration, ease, handle));
            return handle;
        }

        /// <summary>
        /// Simple delay. SnapToFinal is a no-op by default — override for sequences.
        /// </summary>
        public static TweenHandle Delay(float duration)
        {
            var handle = new TweenHandle { SnapToFinal = () => { } };
            handle.Coroutine = GetRunner().StartCoroutine(
                DelayRoutine(duration, handle));
            return handle;
        }

        // ── Coroutines ─────────────────────────────────────────────────

        private static IEnumerator FloatRoutine(float from, float to, float duration,
            Action<float> onUpdate, EaseType ease, TweenHandle handle)
        {
            duration = AdjustDuration(duration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                onUpdate(Mathf.LerpUnclamped(from, to, ApplyEase(t, ease)));
                yield return null;
            }
            onUpdate(to);
            handle.MarkComplete();
        }

        private static IEnumerator ColorRoutine(Color from, Color to, float duration,
            Action<Color> onUpdate, EaseType ease, TweenHandle handle)
        {
            duration = AdjustDuration(duration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                onUpdate(Color.LerpUnclamped(from, to, ApplyEase(t, ease)));
                yield return null;
            }
            onUpdate(to);
            handle.MarkComplete();
        }

        private static IEnumerator PulseGlowRoutine(Action<float> onUpdate,
            float min, float max, float cycleDuration, TweenHandle handle)
        {
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime / cycleDuration;
                t %= 1f; // prevent precision loss from unbounded growth
                float alpha = Mathf.Lerp(min, max, (Mathf.Sin(t * Mathf.PI * 2f) + 1f) / 2f);
                onUpdate(alpha);
                yield return null;
            }
        }

        private static IEnumerator ScalePopRoutine(Transform target, float duration,
            float overshoot, TweenHandle handle)
        {
            if (target == null) { handle.MarkComplete(); yield break; }

            duration = AdjustDuration(duration);
            target.localScale = Vector3.zero;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (target == null) { handle.MarkComplete(); yield break; }
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float s;
                if (t < 0.6f)
                    s = Mathf.Lerp(0f, overshoot, t / 0.6f);
                else
                    s = Mathf.Lerp(overshoot, 1f, (t - 0.6f) / 0.4f);

                target.localScale = Vector3.one * s;
                yield return null;
            }

            if (target != null) target.localScale = Vector3.one;
            handle.MarkComplete();
        }

        private static IEnumerator FlipCardRoutine(RectTransform rt, Action midFlipAction,
            float duration, TweenHandle handle)
        {
            if (rt == null) { handle.MarkComplete(); yield break; }

            duration = AdjustDuration(duration);
            float half = duration / 2f;
            float elapsed = 0f;

            // Squeeze scaleX to 0 (with SmoothStep easing)
            while (elapsed < half)
            {
                if (rt == null) { handle.MarkComplete(); yield break; }
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                float eased = ApplyEase(t, EaseType.SmoothStep);
                rt.localScale = new Vector3(1f - eased, 1f, 1f);
                yield return null;
            }

            // Swap content at midpoint
            midFlipAction();

            // Expand scaleX back to 1 (with SmoothStep easing)
            elapsed = 0f;
            while (elapsed < half)
            {
                if (rt == null) { handle.MarkComplete(); yield break; }
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                float eased = ApplyEase(t, EaseType.SmoothStep);
                rt.localScale = new Vector3(eased, 1f, 1f);
                yield return null;
            }

            if (rt != null) rt.localScale = Vector3.one;
            handle.MarkComplete();
        }

        private static IEnumerator PositionRoutine(RectTransform rt, Vector2 from, Vector2 to,
            float duration, EaseType ease, TweenHandle handle)
        {
            if (rt == null) { handle.MarkComplete(); yield break; }

            duration = AdjustDuration(duration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (rt == null) { handle.MarkComplete(); yield break; }
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, ApplyEase(t, ease));
                yield return null;
            }

            if (rt != null) rt.anchoredPosition = to;
            handle.MarkComplete();
        }

        private static IEnumerator DelayRoutine(float duration, TweenHandle handle)
        {
            yield return new WaitForSeconds(AdjustDuration(duration));
            handle.MarkComplete();
        }

        private static float AdjustDuration(float duration)
        {
            return SpeedMultiplier > 0f ? duration / SpeedMultiplier : duration;
        }

        // ── Easing ─────────────────────────────────────────────────────

        private static float ApplyEase(float t, EaseType ease)
        {
            switch (ease)
            {
                case EaseType.Linear:
                    return t;
                case EaseType.SmoothStep:
                    return t * t * (3f - 2f * t);
                case EaseType.EaseOutBack:
                    const float c = 1.70158f;
                    float t1 = t - 1f;
                    return 1f + (c + 1f) * t1 * t1 * t1 + c * t1 * t1;
                case EaseType.EaseInOutQuad:
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                default:
                    return t;
            }
        }
    }
}
