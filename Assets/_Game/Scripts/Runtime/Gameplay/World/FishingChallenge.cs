using System;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    internal enum FishingSize { Small, Medium, Large }
    internal enum FishingResult { InProgress, Caught, Escaped }

    /// <summary>Positions and progress on the normalized fishing track.</summary>
    internal sealed class FishingChallenge
    {
        internal const float ZoneHeight = .32f;
        private const float ZoneSpeed = .92f;
        private const float RoundSeconds = 24f;
        private readonly System.Random _random;
        private readonly float _fishSpeed;
        private readonly float _fishAgility;
        private readonly float _pause;
        private float _target = .5f;
        private float _velocity;
        private float _untilNextMove = .38f;
        private float _elapsed;

        internal FishingSize Size { get; }
        internal float Fish { get; private set; } = .5f;
        internal float Zone { get; private set; } = .5f;
        internal float Progress { get; private set; } = .32f;
        internal bool InZone => Mathf.Abs(Fish - Zone) <= ZoneHeight * .5f;
        internal FishingResult Result { get; private set; }

        internal FishingChallenge(FishingSize size, System.Random random)
        {
            Size = size;
            _random = random;
            (_fishSpeed, _fishAgility, _pause) = size switch
            {
                FishingSize.Small => (.25f, .92f, .44f),
                FishingSize.Medium => (.47f, 2.1f, .24f),
                _ => (.74f, 4.1f, .10f)
            };
        }

        internal FishingResult Advance(float deltaTime, float direction)
        {
            // Substeps keep a slow frame from skipping the fish across the capture zone.
            var remaining = Mathf.Min(deltaTime, .1f);
            while (remaining > 0f && Result == FishingResult.InProgress)
            {
                var step = Mathf.Min(remaining, 1f / 60f);
                remaining -= step;
                Tick(step, direction);
            }
            return Result;
        }

        private void Tick(float step, float direction)
        {
            _elapsed += step;
            Zone = Mathf.Clamp(Zone + direction * ZoneSpeed * step, ZoneHeight * .5f, 1f - ZoneHeight * .5f);
            if ((_untilNextMove -= step) <= 0f)
            {
                var side = Fish < .5f ? 1f : -1f;
                _target = Mathf.Clamp(Fish + side * Mathf.Lerp(.25f, .75f, (float)_random.NextDouble()), .055f, .945f);
                _untilNextMove = Mathf.Abs(Fish - _target) / _fishSpeed + _pause;
            }

            var offset = _target - Fish;
            var desiredSpeed = Mathf.Clamp(offset * 7f, -_fishSpeed, _fishSpeed);
            _velocity = Mathf.MoveTowards(_velocity, desiredSpeed, _fishAgility * step);
            Fish = Mathf.Clamp(Fish + _velocity * step, .045f, .955f);
            Progress = Mathf.Clamp01(Progress + step * (InZone ? .265f : -.22f));
            if (Progress >= 1f) Result = FishingResult.Caught;
            else if (Progress <= 0f || _elapsed >= RoundSeconds) Result = FishingResult.Escaped;
        }
    }
}
