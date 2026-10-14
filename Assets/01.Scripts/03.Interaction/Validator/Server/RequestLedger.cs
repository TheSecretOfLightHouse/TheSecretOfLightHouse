using System.Collections.Generic;
using Lighthouse.Interaction.Requests.Net;

namespace Lighthouse.Interaction.Validator.Server
{
    // Per-actor request numbering for one match (jihun-system-design §5.2, 2026-10-14 reduced contract).
    // There is no reconnect, so one actor has one connection for the whole match and Mirror's reliable channel
    // never duplicates; only the next number and the last final result are kept.
    public sealed class RequestLedger
    {
        public const uint FirstSequence = 1;

        private readonly Dictionary<uint, Entry> _entries = new Dictionary<uint, Entry>();

        public enum Verdict
        {
            Accept,
            Replay,
            Gap,
            Stale,
            UnknownActor
        }

        private struct Entry
        {
            public uint NextSequence;
            public bool HasLast;
            public InteractionResult Last;
        }

        public void Begin(uint actorId)
        {
            _entries[actorId] = new Entry { NextSequence = FirstSequence };
        }

        public void End(uint actorId)
        {
            _entries.Remove(actorId);
        }

        public void Clear()
        {
            _entries.Clear();
        }

        public bool TryGetNextSequence(uint actorId, out uint nextSequence)
        {
            if (_entries.TryGetValue(actorId, out Entry entry))
            {
                nextSequence = entry.NextSequence;
                return true;
            }

            nextSequence = 0;
            return false;
        }

        // The last number replays its stored result even if the payload differs, so a request never runs twice.
        public Verdict Check(uint actorId, uint sequence, out InteractionResult replay)
        {
            replay = default;

            if (!_entries.TryGetValue(actorId, out Entry entry))
            {
                return Verdict.UnknownActor;
            }

            if (sequence == entry.NextSequence)
            {
                return Verdict.Accept;
            }

            if (entry.HasLast && sequence == entry.Last.Sequence)
            {
                replay = entry.Last;
                return Verdict.Replay;
            }

            return sequence > entry.NextSequence ? Verdict.Gap : Verdict.Stale;
        }

        // Records a final result for an accepted number, success or failure alike, and advances to the next number.
        public void Record(uint actorId, InteractionResult result)
        {
            _entries[actorId] = new Entry
            {
                NextSequence = result.Sequence + 1,
                HasLast = true,
                Last = result
            };
        }
    }
}
