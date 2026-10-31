using UnityEngine;

namespace Lighthouse.UI.SharedMap.Rules
{
    public struct SharedMapOperation
    {
        public uint Author;
        public uint Stroke;
        public uint Sample;
        public uint Writer;
        public Vector2 From;
        public Vector2 To;
        public byte Color;
        public byte BrushSize;
        public bool IsErase;
    }
}
