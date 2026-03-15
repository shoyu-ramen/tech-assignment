using System.Collections.Generic;
using UnityEngine;
using TMPro;
using HijackPoker.Managers;
using HijackPoker.Models;
using HijackPoker.UI;

namespace HijackPoker.Animation
{
    /// <summary>
    /// Animates end-of-hand sequence: card sweep to center, shuffle visual,
    /// and "Hand #N" flash overlay.
    /// Uses runtime RectTransform positions instead of precomputed anchors.
    /// </summary>
    public static class ShuffleAnimator
    {
        private const float SweepDuration = 0.3f;
        private const float SweepStagger = 0.03f;
        private const float ShuffleDuration = 0.4f;

        // Card offsets relative to seat center (mirrors SeatView.BuildUI)
        private static Vector2 Card1Offset => new Vector2(-UI.LayoutConfig.SeatCardSpacing, -24f);
        private static Vector2 Card2Offset => new Vector2(UI.LayoutConfig.SeatCardSpacing, -24f);

        public static void PlayShuffle(
            AnimationController anim,
            Transform canvas,
            SeatView[] seats,
            TableResponse oldState,
            int newHandNumber)
        {
            Vector2 centerPos = GetDeckCanvasPos();
            var flyingCards = new List<GameObject>();
            float delay = 0f;

            // Sweep hole cards from old state
            if (oldState?.Players != null)
            {
                foreach (var player in oldState.Players)
                {
                    if (player == null || !player.HasCards || player.IsFolded) continue;
                    Vector2 seatPos = GetSeatCanvasPos(seats, player.Seat);

                    var fly1 = SweepCard(anim, canvas, seatPos + Card1Offset,
                        centerPos, delay, UI.LayoutConfig.SeatCardScale);
                    flyingCards.Add(fly1);
                    delay += SweepStagger;

                    var fly2 = SweepCard(anim, canvas, seatPos + Card2Offset,
                        centerPos, delay, UI.LayoutConfig.SeatCardScale);
                    flyingCards.Add(fly2);
                    delay += SweepStagger;
                }
            }

            // Sweep community cards
            if (oldState?.Game?.CommunityCards != null && oldState.Game.CommunityCards.Count > 0)
            {
                Vector2 commBase = GetCommunityCardsCanvasPos();
                float totalWidth = 5 * CardView.CardSize.x + 4 * 6f;
                float startX = -totalWidth / 2f + CardView.CardSize.x / 2f;

                for (int i = 0; i < oldState.Game.CommunityCards.Count && i < 5; i++)
                {
                    Vector2 cardPos = commBase +
                        new Vector2(startX + i * (CardView.CardSize.x + 6f), 0);
                    var fly = SweepCard(anim, canvas, cardPos, centerPos, delay, 1f);
                    flyingCards.Add(fly);
                    delay += SweepStagger;
                }
            }

            if (flyingCards.Count == 0)
            {
                PlayHandFlash(anim, canvas, newHandNumber, 0f);
                return;
            }

            float sweepEnd = delay + SweepDuration;
            ScheduleShuffleVisual(anim, canvas, centerPos, sweepEnd, flyingCards);
            PlayHandFlash(anim, canvas, newHandNumber,
                sweepEnd + ShuffleDuration + 0.1f);
        }

        private static Vector2 GetDeckCanvasPos()
        {
            var commCards = Object.FindAnyObjectByType<CommunityCardsView>();
            if (commCards != null)
                return LayoutConfig.WorldToCanvasPos(commCards.GetComponent<RectTransform>());
            return Vector2.zero;
        }

        private static Vector2 GetSeatCanvasPos(SeatView[] seats, int seatNum)
        {
            if (seatNum >= 1 && seatNum <= LayoutConfig.MaxSeats && seats[seatNum] != null)
                return LayoutConfig.WorldToCanvasPos(seats[seatNum].RectTransform);
            return Vector2.zero;
        }

        private static Vector2 GetCommunityCardsCanvasPos()
        {
            var commCards = Object.FindAnyObjectByType<CommunityCardsView>();
            if (commCards != null)
                return LayoutConfig.WorldToCanvasPos(commCards.GetComponent<RectTransform>());
            return Vector2.zero;
        }

        private static GameObject SweepCard(AnimationController anim, Transform canvas,
            Vector2 from, Vector2 to, float delay, float cardScale)
        {
            var go = CreateCardBack(canvas, from, cardScale);
            var rt = go.GetComponent<RectTransform>();

            System.Action snap = () => { if (go != null) Object.Destroy(go); };

            System.Action doSweep = () =>
            {
                if (go == null) return;

                var posHandle = anim.Play(
                    Tweener.TweenPosition(rt, from, to, SweepDuration,
                        EaseType.EaseInOutQuad));
                posHandle.SnapToFinal = snap;

                anim.Play(Tweener.TweenFloat(cardScale, cardScale * 0.6f, SweepDuration,
                    s => { if (rt != null) rt.localScale = Vector3.one * s; }))
                    .SnapToFinal = snap;
            };

            if (delay > 0f)
            {
                var dh = anim.Play(Tweener.Delay(delay));
                dh.SnapToFinal = snap;
                dh.OnComplete(doSweep);
            }
            else
            {
                doSweep();
            }

            return go;
        }

        private static void ScheduleShuffleVisual(AnimationController anim, Transform canvas,
            Vector2 center, float delay, List<GameObject> sweepCards)
        {
            System.Action cleanupSweep = () =>
            {
                foreach (var g in sweepCards)
                    if (g != null) Object.Destroy(g);
            };

            System.Action doShuffle = () =>
            {
                cleanupSweep();
                AudioManager.Instance?.Play(SoundType.Shuffle);

                var left = CreateCardBack(canvas, center + new Vector2(-16, 0), 1f);
                var right = CreateCardBack(canvas, center + new Vector2(16, 0), 1f);
                var leftRt = left.GetComponent<RectTransform>();
                var rightRt = right.GetComponent<RectTransform>();

                System.Action cleanupStacks = () =>
                {
                    if (left != null) Object.Destroy(left);
                    if (right != null) Object.Destroy(right);
                };

                float half = ShuffleDuration * 0.5f;

                anim.Play(Tweener.TweenPosition(leftRt,
                    center + new Vector2(-16, 0), center, half, EaseType.EaseInOutQuad))
                    .SnapToFinal = cleanupStacks;

                var mergeRight = anim.Play(Tweener.TweenPosition(rightRt,
                    center + new Vector2(16, 0), center, half, EaseType.EaseInOutQuad));
                mergeRight.SnapToFinal = cleanupStacks;

                mergeRight.OnComplete(() =>
                {
                    if (leftRt == null) { cleanupStacks(); return; }

                    var fade = anim.Play(Tweener.TweenFloat(1f, 0f, half,
                        a =>
                        {
                            if (leftRt != null)
                                leftRt.localScale = Vector3.one * (1f + (1f - a) * 0.15f);
                            if (rightRt != null)
                                rightRt.localScale = Vector3.one * (1f + (1f - a) * 0.15f);
                        }));
                    fade.SnapToFinal = cleanupStacks;
                    fade.OnComplete(cleanupStacks);
                });
            };

            if (delay > 0f)
            {
                var dh = anim.Play(Tweener.Delay(delay));
                dh.SnapToFinal = cleanupSweep;
                dh.OnComplete(doShuffle);
            }
            else
            {
                doShuffle();
            }
        }

        private static void PlayHandFlash(AnimationController anim, Transform canvas,
            int handNumber, float delay)
        {
            System.Action doFlash = () =>
            {
                var text = UIFactory.CreateText("HandFlash", canvas,
                    $"Hand #{handNumber}", 36f, UIFactory.AccentCyan,
                    TextAlignmentOptions.Center, FontStyles.Bold);
                var rt = text.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(400, 60);

                Color cyan = UIFactory.AccentCyan;
                text.color = new Color(cyan.r, cyan.g, cyan.b, 0f);

                System.Action cleanup = () =>
                {
                    if (text != null) Object.Destroy(text.gameObject);
                };

                var fadeIn = anim.Play(Tweener.TweenFloat(0f, 1f, 0.2f,
                    a => { if (text != null) text.color = new Color(cyan.r, cyan.g, cyan.b, a); }));
                fadeIn.SnapToFinal = cleanup;

                fadeIn.OnComplete(() =>
                {
                    var hold = anim.Play(Tweener.Delay(0.4f));
                    hold.SnapToFinal = cleanup;

                    hold.OnComplete(() =>
                    {
                        var fadeOut = anim.Play(Tweener.TweenFloat(1f, 0f, 0.2f,
                            a => { if (text != null) text.color = new Color(cyan.r, cyan.g, cyan.b, a); }));
                        fadeOut.SnapToFinal = cleanup;
                        fadeOut.OnComplete(cleanup);
                    });
                });
            };

            if (delay > 0f)
            {
                var dh = anim.Play(Tweener.Delay(delay));
                dh.SnapToFinal = () => { };
                dh.OnComplete(doFlash);
            }
            else
            {
                doFlash();
            }
        }

        private static GameObject CreateCardBack(Transform canvas, Vector2 position, float scale)
        {
            var go = new GameObject("ShuffleCard", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = CardView.CardSize;
            rt.localScale = Vector3.one * scale;

            var bg = UIFactory.CreateImage("Back", go.transform, UIFactory.CardBack);
            UIFactory.StretchFill(bg.GetComponent<RectTransform>());

            var inner = UIFactory.CreateImage("Inner", bg.transform, UIFactory.CardBackLight);
            var innerRt = inner.GetComponent<RectTransform>();
            innerRt.anchorMin = Vector2.zero;
            innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(3, 3);
            innerRt.offsetMax = new Vector2(-3, -3);

            return go;
        }
    }
}
