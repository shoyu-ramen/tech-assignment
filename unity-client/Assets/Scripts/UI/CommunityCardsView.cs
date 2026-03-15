using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HijackPoker.Animation;
using HijackPoker.Managers;
using HijackPoker.Models;

namespace HijackPoker.UI
{
    /// <summary>
    /// Renders the 5 community card slots at center table.
    /// Cards appear incrementally: 3 at flop, 4 at turn, 5 at river.
    /// Animates new card reveals with staggered pop (flop) or slide-down (turn/river).
    /// </summary>
    public class CommunityCardsView : MonoBehaviour
    {
        private CardView[] _cards;
        private RectTransform _rt;
        private int _prevCardCount = -1;

        public AnimationController AnimController { get; set; }

        public static CommunityCardsView Create(Transform parent)
        {
            var go = new GameObject("CommunityCards", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            var cardSz = CardView.CardSize;
            float gap = LayoutConfig.CommunityCardGap;
            float totalWidth = 5 * cardSz.x + 4 * gap;
            float totalHeight = cardSz.y + 10;
            rt.sizeDelta = new Vector2(totalWidth + 10, totalHeight);

            // Participate in parent layout group
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = totalWidth + 10;
            le.preferredHeight = totalHeight;

            // HorizontalLayoutGroup handles card spacing
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = gap;
            hlg.padding = new RectOffset(5, 5, 5, 5);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var view = go.AddComponent<CommunityCardsView>();
            view._rt = rt;
            view._cards = new CardView[5];

            for (int i = 0; i < 5; i++)
            {
                var card = CardView.Create($"Community_{i}", go.transform);
                view._cards[i] = card;
            }

            return view;
        }

        public void UpdateFromState(TableResponse state)
        {
            if (state?.Game == null)
            {
                ClearAll();
                return;
            }

            var communityCards = state.Game.CommunityCards;
            int newCount = communityCards?.Count ?? 0;
            int oldCount = _prevCardCount;
            bool animate = _prevCardCount >= 0 && AnimController != null;

            // Cards removed (reset / new hand) — snap instantly
            if (newCount <= oldCount || !animate)
            {
                for (int i = 0; i < 5; i++)
                {
                    if (i < newCount && communityCards != null)
                        _cards[i].SetFaceUp(communityCards[i]);
                    else
                        _cards[i].SetEmpty();
                }
                _prevCardCount = newCount;
                return;
            }

            // Keep existing cards
            for (int i = 0; i < oldCount; i++)
            {
                _cards[i].SetFaceUp(communityCards[i]);
            }

            // Clear unused slots
            for (int i = newCount; i < 5; i++)
            {
                _cards[i].SetEmpty();
            }

            // Animate new cards
            int newCardCount = newCount - oldCount;
            bool isFlop = oldCount == 0 && newCount == 3;

            for (int i = 0; i < newCardCount; i++)
            {
                int idx = oldCount + i;
                AnimateCardReveal(idx, communityCards[idx], isFlop, i);
                if (i == 0)
                    AudioManager.Instance?.Play(SoundType.CommunityCardReveal);
            }

            _prevCardCount = newCount;
        }

        private void AnimateCardReveal(int index, string cardString, bool isFlop, int sequenceIndex)
        {
            var card = _cards[index];
            var rt = card.RectTransform;
            Vector2 finalPos = rt.anchoredPosition;

            if (isFlop)
            {
                // Flop: staggered scale pop with 0.12s delay between cards
                card.SetFaceUp(cardString);
                rt.localScale = Vector3.zero;

                float delay = sequenceIndex * 0.12f;
                if (delay > 0)
                {
                    var delayHandle = AnimController.Play(Tweener.Delay(delay));
                    // Override SnapToFinal so cancel restores the card
                    delayHandle.SnapToFinal = () =>
                    {
                        if (rt != null) rt.localScale = Vector3.one;
                    };
                    delayHandle.OnComplete(() =>
                    {
                        var popHandle = AnimController.Play(
                            Tweener.ScalePop(rt, 0.2f, 1.15f));
                        popHandle.SnapToFinal = () =>
                        {
                            if (rt != null) rt.localScale = Vector3.one;
                        };
                    });
                }
                else
                {
                    AnimController.Play(Tweener.ScalePop(rt, 0.2f, 1.15f));
                }
            }
            else
            {
                // Turn / River: slide down from above + scale up
                card.SetFaceUp(cardString);
                rt.anchoredPosition = finalPos + new Vector2(0, 30);
                rt.localScale = Vector3.one * 0.5f;

                AnimController.Play(Tweener.TweenFloat(30f, 0f, 0.3f,
                    y => { if (rt != null) rt.anchoredPosition = finalPos + new Vector2(0, y); }));

                AnimController.Play(Tweener.TweenFloat(0.5f, 1f, 0.3f,
                    s => { if (rt != null) rt.localScale = Vector3.one * s; },
                    EaseType.EaseOutBack));
            }
        }

        public void ClearAll()
        {
            for (int i = 0; i < 5; i++)
                _cards[i].SetEmpty();
            _prevCardCount = 0;
        }
    }
}
