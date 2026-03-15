using System.Collections.Generic;
using UnityEngine;
using HijackPoker.Managers;
using HijackPoker.Models;
using HijackPoker.UI;

namespace HijackPoker.Animation
{
    /// <summary>
    /// Animates clockwise card dealing from a center deck to player seats.
    /// Two rounds (one card per player per round), with arc trajectory.
    /// Uses runtime RectTransform positions instead of precomputed anchors.
    /// </summary>
    public static class DealAnimator
    {
        private const float CardFlyDuration = 0.1f;
        private const float CardGap = 0.02f;
        private const float ArcHeight = 30f;
        private const float InitialDelay = 0.08f;

        public static void PlayDeal(
            AnimationController anim,
            Transform canvas,
            int dealerSeat,
            SeatView[] seats,
            List<PlayerState> players)
        {
            if (players == null || players.Count == 0) return;

            var activeSeats = new HashSet<int>();
            foreach (var p in players)
                if (p != null && p.HasCards && !p.IsFolded)
                    activeSeats.Add(p.Seat);
            if (activeSeats.Count == 0) return;

            var dealOrder = BuildDealOrder(dealerSeat, activeSeats);

            // Deck position: center of the community cards area
            Vector2 deckPos = GetDeckCanvasPos();
            var deckGo = CreateDeckVisual(canvas, deckPos);

            // Suppress real cards (they were just rendered face-down)
            foreach (int s in dealOrder)
            {
                seats[s].Card1.RectTransform.localScale = Vector3.zero;
                seats[s].Card2.RectTransform.localScale = Vector3.zero;
            }

            int totalFlights = dealOrder.Count * 2;
            int flightIdx = 0;
            float delay = InitialDelay;

            // Round 1: first card to each player
            for (int i = 0; i < dealOrder.Count; i++)
            {
                int seat = dealOrder[i];
                Vector2 target = GetCardCanvasPos(seats[seat], 0);
                FlyCard(anim, canvas, deckPos, target, delay, seats[seat].Card1,
                    flightIdx == totalFlights - 1, deckGo);
                delay += CardFlyDuration + CardGap;
                flightIdx++;
            }

            // Round 2: second card to each player
            for (int i = 0; i < dealOrder.Count; i++)
            {
                int seat = dealOrder[i];
                Vector2 target = GetCardCanvasPos(seats[seat], 1);
                FlyCard(anim, canvas, deckPos, target, delay, seats[seat].Card2,
                    flightIdx == totalFlights - 1, deckGo);
                delay += CardFlyDuration + CardGap;
                flightIdx++;
            }
        }

        private static List<int> BuildDealOrder(int dealerSeat, HashSet<int> activeSeats)
        {
            var order = new List<int>();
            for (int i = 1; i <= LayoutConfig.MaxSeats; i++)
            {
                int seat = ((dealerSeat - 1 + i) % LayoutConfig.MaxSeats) + 1;
                if (activeSeats.Contains(seat))
                    order.Add(seat);
            }
            return order;
        }

        private static Vector2 GetDeckCanvasPos()
        {
            // Try to find community cards view as the deck origin
            var commCards = Object.FindAnyObjectByType<CommunityCardsView>();
            if (commCards != null)
                return LayoutConfig.WorldToCanvasPos(commCards.GetComponent<RectTransform>());
            return Vector2.zero;
        }

        private static Vector2 GetCardCanvasPos(SeatView seat, int cardIndex)
        {
            Vector2 seatPos = LayoutConfig.WorldToCanvasPos(seat.RectTransform);
            CardView card = cardIndex == 0 ? seat.Card1 : seat.Card2;
            return seatPos + card.RectTransform.anchoredPosition;
        }

        private static void FlyCard(AnimationController anim, Transform canvas,
            Vector2 from, Vector2 to, float delay, CardView targetCard,
            bool isLastCard, GameObject deckGo)
        {
            var flyGo = CreateFlyingCardBack(canvas, from);
            flyGo.SetActive(false);
            var flyRt = flyGo.GetComponent<RectTransform>();

            CardView capturedCard = targetCard;
            GameObject capturedDeck = deckGo;
            bool capturedLast = isLastCard;

            System.Action snap = () =>
            {
                if (flyGo != null) Object.Destroy(flyGo);
                if (capturedCard != null)
                    capturedCard.RectTransform.localScale = Vector3.one * UI.LayoutConfig.SeatCardScale;
                if (capturedLast && capturedDeck != null)
                    Object.Destroy(capturedDeck);
            };

            System.Action onArrival = () =>
            {
                if (flyGo != null) Object.Destroy(flyGo);
                if (capturedCard != null)
                    capturedCard.RectTransform.localScale = Vector3.one * UI.LayoutConfig.SeatCardScale;
                if (capturedLast)
                    FadeDeck(anim, capturedDeck);
            };

            System.Action doFly = () =>
            {
                if (flyGo == null) return;
                flyGo.SetActive(true);
                AudioManager.Instance?.Play(SoundType.CardDeal);

                var handle = anim.Play(Tweener.TweenFloat(0f, 1f, CardFlyDuration, t =>
                {
                    if (flyRt == null) return;
                    Vector2 linear = Vector2.Lerp(from, to, t);
                    Vector2 dir = (to - from).normalized;
                    Vector2 perp = new Vector2(-dir.y, dir.x);
                    float arc = Mathf.Sin(t * Mathf.PI) * ArcHeight;
                    flyRt.anchoredPosition = linear + perp * arc;
                }, EaseType.EaseInOutQuad));

                handle.SnapToFinal = snap;
                handle.OnComplete(onArrival);
            };

            if (delay > 0f)
            {
                var dh = anim.Play(Tweener.Delay(delay));
                dh.SnapToFinal = snap;
                dh.OnComplete(doFly);
            }
            else
            {
                doFly();
            }
        }

        private static void FadeDeck(AnimationController anim, GameObject deckGo)
        {
            if (deckGo == null) return;
            var cg = deckGo.GetComponent<CanvasGroup>();
            if (cg == null) { Object.Destroy(deckGo); return; }

            var go = deckGo;
            var handle = anim.Play(Tweener.TweenFloat(1f, 0f, 0.2f,
                a => { if (cg != null) cg.alpha = a; }));
            handle.SnapToFinal = () => { if (go != null) Object.Destroy(go); };
            handle.OnComplete(() => { if (go != null) Object.Destroy(go); });
        }

        // ── Visual element factories ──────────────────────────────────

        private static GameObject CreateDeckVisual(Transform canvas, Vector2 position)
        {
            var go = new GameObject("DealDeck", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(56, 76);
            go.AddComponent<CanvasGroup>();

            for (int i = 0; i < 3; i++)
            {
                float offset = (2 - i) * 1.5f;
                var card = UIFactory.CreateImage($"DeckCard{i}", go.transform,
                    UIFactory.CardBack, CardView.CardSize);
                card.GetComponent<RectTransform>().anchoredPosition =
                    new Vector2(offset, -offset);

                var inner = UIFactory.CreateImage("Inner", card.transform,
                    UIFactory.CardBackLight);
                var innerRt = inner.GetComponent<RectTransform>();
                innerRt.anchorMin = Vector2.zero;
                innerRt.anchorMax = Vector2.one;
                innerRt.offsetMin = new Vector2(3, 3);
                innerRt.offsetMax = new Vector2(-3, -3);
            }

            return go;
        }

        private static GameObject CreateFlyingCardBack(Transform canvas, Vector2 position)
        {
            var go = new GameObject("FlyCard", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = CardView.CardSize;
            rt.localScale = Vector3.one * UI.LayoutConfig.SeatCardScale;

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
