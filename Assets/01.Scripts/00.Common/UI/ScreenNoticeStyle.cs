namespace Lighthouse.Common.UI
{
    // Tone of one notice line. Colours live on the ScreenNotice prefab so the look can change without code.
    public enum ScreenNoticeStyle : byte
    {
        Normal = 0,
        // Progress worth celebrating: a finished floor, the last part.
        Gold = 1,
        // Bad news for the listener.
        Warning = 2
    }
}
