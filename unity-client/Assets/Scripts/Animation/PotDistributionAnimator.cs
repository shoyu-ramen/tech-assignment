using System;
using System.Collections.Generic;
using UnityEngine;
using HijackPoker.Managers;
using HijackPoker.Models;
using HijackPoker.UI;
using HijackPoker.Utils;

namespace HijackPoker.Animation
{
    /// <summary>
    /// Animates pot-to-winner fly animations after a hand is paid out.
    /// Uses runtime RectTransform positions instead of precomputed anchors.
    /// </summary>
    public static class PotDistributionAnimator
    {
        public static void PlayPotDistribution(
            AnimationController anim,
            Transform canvas,
            SeatView[] seats,
            TableResponse oldState,
            TableResponse newState)
        {
            if (newState?.Players == null) return;

            // Get pot position from the HUD's pot RectTransform at runtime
            var hudView = UnityEngine.Object.FindAnyObjectByType<HudView>();
            Vector2 potPos = hudView != null && hudView.PotTransform != null
                ? LayoutConfig.WorldToCanvasPos(hudView.PotTransform)
                : Vector2.zero;

            // Collect winners
            var winners = new List<PlayerState>();
            foreach (var p in newState.Players)
                if (p.IsWinner && p.Winnings > 0) winners.Add(p);
            if (winners.Count == 0) return;

            bool hasSidePots = oldState?.Game?.SidePots != null
                && oldState.Game.SidePots.Count > 0;

            // Build flight list: (label, targetSeat, delay)
            var flights = new List<(string label, int seat, float delay)>();

            if (!hasSidePots)
            {
                for (int i = 0; i < winners.Count; i++)
                {
                    var w = winners[i];
                    flights.Add((
                        $"+{MoneyFormatter.Format(w.Winnings)}",
                        w.Seat, i * 0.2f));
                }
            }
            else
            {
                var winnerSeatSet = new HashSet<int>();
                if (newState.Game?.Winners != null)
                    foreach (var w in newState.Game.Winners)
                        winnerSeatSet.Add(w.Seat);

                float sidePotTotal = 0;
                foreach (var sp in oldState.Game.SidePots)
                    sidePotTotal += sp.Amount;
                float mainPot = Mathf.Max(0, oldState.Game.Pot - sidePotTotal);

                int idx = 0;

                if (mainPot > 0.01f && winners.Count > 0)
                {
                    flights.Add((
                        $"Pot: {MoneyFormatter.Format(mainPot)}",
                        winners[0].Seat, idx * 0.2f));
                    idx++;
                }

                for (int i = 0; i < oldState.Game.SidePots.Count; i++)
                {
                    var sp = oldState.Game.SidePots[i];
                    if (sp.Amount <= 0.01f || sp.EligibleSeats == null) continue;

                    int spWinner = -1;
                    foreach (var seat in sp.EligibleSeats)
                    {
                        if (winnerSeatSet.Contains(seat))
                        {
                            spWinner = seat;
                            break;
                        }
                    }
                    if (spWinner < 1) spWinner = winners[0].Seat;

                    flights.Add((
                        $"Side Pot {i + 1}: {MoneyFormatter.Format(sp.Amount)}",
                        spWinner, idx * 0.2f));
                    idx++;
                }
            }

            if (flights.Count == 0) return;

            var lastFlightPerSeat = new Dictionary<int, int>();
            for (int i = 0; i < flights.Count; i++)
                lastFlightPerSeat[flights[i].seat] = i;

            for (int i = 0; i < flights.Count; i++)
            {
                var (label, seat, delay) = flights[i];
                // Get seat position from its actual RectTransform
                var seatPos = GetSeatCanvasPos(seats, seat);
                bool triggerStack = lastFlightPerSeat[seat] == i;
                CreateFlyingPot(anim, canvas, seats, label, potPos, seatPos, delay,
                    triggerStack ? seat : -1);
            }
        }

        private static Vector2 GetSeatCanvasPos(SeatView[] seats, int seatNum)
        {
            if (seatNum >= 1 && seatNum <= LayoutConfig.MaxSeats && seats[seatNum] != null)
                return LayoutConfig.WorldToCanvasPos(seats[seatNum].RectTransform);
            return Vector2.zero;
        }

        private static void CreateFlyingPot(
            AnimationController anim,
            Transform canvas,
            SeatView[] seats,
            string text, Vector2 from, Vector2 to,
            float delay, int stackSeatNum)
        {
            var flyText = UIFactory.CreateText("FlyPot", canvas, text,
                14f, UIFactory.AccentGold, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            var flyRt = flyText.GetComponent<RectTransform>();
            flyRt.anchorMin = new Vector2(0.5f, 0.5f);
            flyRt.anchorMax = new Vector2(0.5f, 0.5f);
            flyRt.pivot = new Vector2(0.5f, 0.5f);
            flyRt.anchoredPosition = from;
            flyRt.sizeDelta = new Vector2(160, 24);

            int capturedSeat = stackSeatNum;

            Action onArrival = () =>
            {
                if (flyText != null) UnityEngine.Object.Destroy(flyText.gameObject);
                AudioManager.Instance?.Play(SoundType.ChipClink);
                if (capturedSeat >= 1 && capturedSeat <= LayoutConfig.MaxSeats)
                    seats[capturedSeat].AnimateDeferredStack();
            };

            Action doFly = () =>
            {
                var handle = anim.Play(
                    Tweener.TweenPosition(flyRt, from, to, 0.6f, EaseType.EaseInOutQuad));

                handle.SnapToFinal = () =>
                {
                    if (flyRt != null) flyRt.anchoredPosition = to;
                    onArrival();
                };
                handle.OnComplete(onArrival);
            };

            if (delay > 0f)
            {
                var delayHandle = anim.Play(Tweener.Delay(delay));
                delayHandle.SnapToFinal = onArrival;
                delayHandle.OnComplete(doFly);
            }
            else
            {
                doFly();
            }
        }
    }
}
