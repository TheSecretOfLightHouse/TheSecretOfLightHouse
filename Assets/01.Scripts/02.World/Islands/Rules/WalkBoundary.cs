using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Islands.Rules
{
    public sealed class WalkBoundary : IWalkBoundary
    {
        private const float MinMoveSqr = 1e-8f;
        private const float RingScale = 2f;
        private const float Diagonal = 0.70710678f;

        private static readonly Vector2[] RingDirections =
        {
            new Vector2(1f, 0f),
            new Vector2(Diagonal, Diagonal),
            new Vector2(0f, 1f),
            new Vector2(-Diagonal, Diagonal),
            new Vector2(-1f, 0f),
            new Vector2(-Diagonal, -Diagonal),
            new Vector2(0f, -1f),
            new Vector2(Diagonal, -Diagonal)
        };

        private readonly List<WalkMask> _masks = new List<WalkMask>();
        private readonly float _lookahead;

        public WalkBoundary(float lookaheadMeters)
        {
            if (lookaheadMeters <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(lookaheadMeters));
            }

            _lookahead = lookaheadMeters;
        }

        public int MaskCount => _masks.Count;

        public void Add(WalkMask mask)
        {
            _masks.Add(mask ?? throw new ArgumentNullException(nameof(mask)));
        }

        public void Clear()
        {
            _masks.Clear();
        }

        public bool IsWalkable(Vector3 position)
        {
            return IsWalkable(new Vector2(position.x, position.z));
        }

        public Vector3 FilterMove(Vector3 position, Vector3 direction)
        {
            var move = new Vector2(direction.x, direction.z);
            if (move.sqrMagnitude < MinMoveSqr)
            {
                return direction;
            }

            var origin = new Vector2(position.x, position.z);
            if (IsWalkable(origin + move.normalized * _lookahead))
            {
                return direction;
            }

            Vector2 inward = GetInward(origin);
            if (inward.sqrMagnitude < MinMoveSqr)
            {
                return direction;
            }

            inward.Normalize();
            float along = Vector2.Dot(move, inward);
            if (along >= 0f)
            {
                return direction;
            }

            move -= inward * along;
            return new Vector3(move.x, direction.y, move.y);
        }

        private bool IsWalkable(Vector2 point)
        {
            for (int i = 0; i < _masks.Count; i++)
            {
                if (_masks[i].IsWalkable(point))
                {
                    return true;
                }
            }

            return false;
        }

        private Vector2 GetInward(Vector2 origin)
        {
            Vector2 sum = Vector2.zero;
            float radius = _lookahead * RingScale;
            for (int i = 0; i < RingDirections.Length; i++)
            {
                if (IsWalkable(origin + RingDirections[i] * radius))
                {
                    sum += RingDirections[i];
                }
            }

            return sum;
        }
    }
}
