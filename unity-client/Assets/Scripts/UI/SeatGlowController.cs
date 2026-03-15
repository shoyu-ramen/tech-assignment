using UnityEngine;
using UnityEngine.UI;
using HijackPoker.Animation;
using HijackPoker.Models;

namespace HijackPoker.UI
{
    /// <summary>
    /// Manages the active-turn breathing glow and winner pulsing gold glow
    /// for a single seat.
    /// </summary>
    public class SeatGlowController
    {
        private readonly Image _activeGlow;
        private TweenHandle _activeGlowTween;
        private TweenHandle _winnerGlowTween;
        private bool _wasActive;
        private bool _wasWinner;

        public bool WasWinner => _wasWinner;

        public SeatGlowController(Image activeGlow)
        {
            _activeGlow = activeGlow;
        }

        public void UpdateActiveGlow(PlayerState player, GameState game, bool animate,
            AnimationController animController)
        {
            bool isActive = game.Move == player.Seat && game.Move > 0;

            bool needsRestart = isActive
                && (_activeGlowTween == null || _activeGlowTween.IsComplete);

            if (isActive && (!_wasActive || needsRestart))
            {
                _activeGlowTween?.Cancel();
                _activeGlowTween = animController?.Play(Tweener.PulseGlow(
                    a => _activeGlow.color = new Color(0, 0.83f, 1f, a),
                    0.08f, 0.28f, 1.0f));

                if (_activeGlowTween == null)
                    _activeGlow.color = new Color(0, 0.83f, 1f, 0.12f);
            }
            else if (!isActive && _wasActive)
            {
                _activeGlowTween?.Cancel();
                _activeGlowTween = null;
                _activeGlow.color = new Color(0, 0, 0, 0);
            }
            else if (!isActive)
            {
                if (!_wasWinner && !(player.IsWinner && game.HandStep >= 13))
                    _activeGlow.color = new Color(0, 0, 0, 0);
            }

            _wasActive = isActive;
        }

        public void UpdateWinnerGlow(PlayerState player, GameState game, bool animate,
            AnimationController animController)
        {
            bool isWinner = player.IsWinner && game.HandStep >= 13;

            bool needsRestart = isWinner
                && (_winnerGlowTween == null || _winnerGlowTween.IsComplete);

            if (isWinner && (!_wasWinner || needsRestart))
            {
                _activeGlowTween?.Cancel();
                _winnerGlowTween?.Cancel();
                _winnerGlowTween = animController?.Play(Tweener.PulseGlow(
                    a => _activeGlow.color = new Color(1f, 0.84f, 0f, a),
                    0.05f, 0.25f, 0.8f));

                if (_winnerGlowTween == null)
                    _activeGlow.color = new Color(1f, 0.84f, 0f, 0.15f);
            }
            else if (!isWinner && _wasWinner)
            {
                _winnerGlowTween?.Cancel();
                _winnerGlowTween = null;
            }

            _wasWinner = isWinner;
        }

        public void ResetContinuousTweens()
        {
            _activeGlowTween = null;
            _winnerGlowTween = null;
            _wasActive = false;
            _wasWinner = false;
        }

        public void Reset()
        {
            _activeGlowTween?.Cancel();
            _activeGlowTween = null;
            _winnerGlowTween?.Cancel();
            _winnerGlowTween = null;
            _wasActive = false;
            _wasWinner = false;
        }
    }
}
