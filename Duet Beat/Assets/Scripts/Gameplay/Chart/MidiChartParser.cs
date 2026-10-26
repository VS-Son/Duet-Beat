using System.Collections.Generic;
using MidiPlayerTK;

namespace Gameplay
{
    /// <summary>
    /// Turns a song's raw .mid bytes into a six-lane falling-note <see cref="Chart"/>, using the
    /// MidiPlayer Toolkit (MPTK) <see cref="MidiLoad"/> parser (Assets/MidiPlayer) for the actual MIDI
    /// reading — tempo-map-aware absolute timing, per-track/channel data, no hand-rolled SMF parsing needed.
    /// </summary>
    public static class MidiChartParser
    {
        private const int BigVelocityThreshold = 100; // 0-127; loud/strong notes render as NoteType.Big.
        private const int StarVelocityThreshold = 120; // 0-127; the loudest, rarest notes become NoteType.Star.
        private const float HoldDurationThreshold = 0.35f; // seconds; notes held this long become NoteType.Hold.

        /// <summary>
        /// Parses raw .mid bytes into a chart. Returns null if MPTK can't load the data or it contains
        /// no note-on events — callers should fall back to <see cref="Chart.CreateFallback"/>.
        /// </summary>
        public static Chart Parse(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            var midiLoad = new MidiLoad
            {
                MPTK_KeepNoteOff = false,      // one NoteOn event already carries its own Duration.
                MPTK_EnableChangeTempo = true, // honor mid-song tempo changes for RealTime.
            };

            bool loaded;
            try
            {
                loaded = midiLoad.MPTK_Load(data);
            }
            catch (System.Exception)
            {
                return null;
            }

            if (!loaded || midiLoad.MPTK_MidiEvents == null)
                return null;

            // Group note-on data (real-time, type, hold duration) by source track, so we can spread
            // multiple instrument tracks across the six lanes the same way the fallback chart does.
            // Note type comes straight from the MIDI event: a long-held note (Duration, tempo-map
            // corrected by MPTK) becomes a Hold; otherwise Velocity decides Star (loudest) vs Big vs Normal.
            var byTrack = new Dictionary<long, List<NoteData>>();
            foreach (MPTKEvent evt in midiLoad.MPTK_MidiEvents)
            {
                if (evt.Command != MPTKCommand.NoteOn)
                    continue;

                float durationSeconds = evt.Duration / 1000f;
                NoteType type;
                if (durationSeconds >= HoldDurationThreshold)
                    type = NoteType.Hold;
                else if (evt.Velocity >= StarVelocityThreshold)
                    type = NoteType.Star;
                else if (evt.Velocity >= BigVelocityThreshold)
                    type = NoteType.Big;
                else
                    type = NoteType.Normal;

                if (!byTrack.TryGetValue(evt.Track, out List<NoteData> list))
                {
                    list = new List<NoteData>();
                    byTrack[evt.Track] = list;
                }

                list.Add(new NoteData
                {
                    time = evt.RealTime / 1000f,
                    type = type,
                    duration = type == NoteType.Hold ? durationSeconds : 0f,
                });
            }

            var notes = new List<NoteData>();

            if (byTrack.Count >= 2)
            {
                var trackKeys = new List<long>(byTrack.Keys);
                trackKeys.Sort();

                for (int trackIndex = 0; trackIndex < trackKeys.Count; trackIndex++)
                {
                    int lane = trackIndex % Chart.LaneCount;
                    foreach (NoteData n in byTrack[trackKeys[trackIndex]])
                    {
                        NoteData assigned = n;
                        assigned.lane = lane;
                        notes.Add(assigned);
                    }
                }
            }
            else if (byTrack.Count == 1)
            {
                List<NoteData> raw = null;
                foreach (List<NoteData> value in byTrack.Values)
                    raw = value; // only one entry when Count == 1.

                raw.Sort((a, b) => a.time.CompareTo(b.time));
                for (int i = 0; i < raw.Count; i++)
                {
                    NoteData assigned = raw[i];
                    assigned.lane = i % Chart.LaneCount;
                    notes.Add(assigned);
                }
            }

            if (notes.Count == 0)
                return null;

            notes.Sort((a, b) => a.time.CompareTo(b.time));

            float duration = (float)midiLoad.MPTK_Duration.TotalSeconds;
            if (duration <= 0f)
                duration = notes[notes.Count - 1].time + 2f;

            return new Chart { Notes = notes, Duration = duration };
        }
    }
}
