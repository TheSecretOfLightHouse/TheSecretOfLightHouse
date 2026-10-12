using System;
using UnityEngine;

namespace Lighthouse.World.Weather.Rules
{
    [Serializable]
    public struct WaveSlot
    {
        public float Wavelength;
        public Vector2 Direction;
        public float Speed;
    }
}
