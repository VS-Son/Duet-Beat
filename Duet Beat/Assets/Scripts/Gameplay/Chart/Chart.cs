using System.Collections.Generic;
using UnityEngine;

namespace Gameplay
{
    /// <summary>
    /// A playable note chart: an ordered list of notes plus the song duration they span.
    /// </summary>
    public class Chart
    {
        /// <summary>
        /// Total number of falling-note lanes: 3 for the left character, 3 for the right character.
        /// </summary>
        public const int LaneCount = 6;

        public List<NoteData> Notes = new List<NoteData>();
        public float Duration;

        /// <summary>
        /// Generates a simple, evenly-spaced, round-robin-lane chart mixing all three note types.
        /// Used whenever a real MIDI chart can't be obtained (no bundled/downloadable fileData) so the
        /// gameplay screen still has notes to show instead of dead-ending.
        /// </summary>
        public static Chart CreateFallback(int difficulty, float duration)
        {
            float bpm = 100f + Mathf.Clamp(difficulty, 0, 3) * 20f;
            float interval = 60f / bpm;

            var notes = new List<NoteData>();
            int lane = 0;
            int i = 0;
            for (float t = 2f; t < duration; t += interval)
            {
                NoteType type = NoteType.Normal;
                float noteDuration = 0f;

                if (i % 8 == 7)
                {
                    type = NoteType.Hold;
                    noteDuration = interval * 3f;
                }
                else if (i % 6 == 5)
                {
                    type = NoteType.Star;
                }
                else if (i % 4 == 3)
                {
                    type = NoteType.Big;
                }

                notes.Add(new NoteData { time = t, lane = lane, type = type, duration = noteDuration });
                lane = (lane + 1) % LaneCount;
                i++;
            }

            return new Chart { Notes = notes, Duration = duration };
        }
    }
}
