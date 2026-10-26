namespace Gameplay
{
    /// <summary>
    /// Behaviour/visual for a falling note: a quick single tap, an emphasized bigger tap, a hold that
    /// must be pressed and kept held for its full <see cref="NoteData.duration"/>, or a rarer star tap.
    /// </summary>
    public enum NoteType
    {
        Normal,
        Big,
        Hold,
        Star,
    }

    /// <summary>
    /// A single falling note: when it should be hit, which lane it falls in, its type, and —
    /// for <see cref="NoteType.Hold"/> notes only — how long it must be held for.
    /// </summary>
    public struct NoteData
    {
        public float time;
        public int lane;
        public NoteType type;
        public float duration;
    }
}
