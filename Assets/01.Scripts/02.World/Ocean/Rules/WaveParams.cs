using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    [Serializable]
    public struct WaveParams
    {
        public float Amplitude;
        public float Wavelength;
        public Vector2 Direction;
        public float Steepness;
        public float Speed;
    }
}
