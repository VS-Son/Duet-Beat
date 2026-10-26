using System;
using System.Collections;
using DuetBeat.SongSelect;
using UnityEngine;
using UnityEngine.Networking;

namespace Gameplay
{
    /// <summary>
    /// Resolves a <see cref="SongEntry.fileData"/> reference into a playable <see cref="Chart"/>.
    /// Downloads and parses real MIDI for CDN-hosted songs; falls back to a generated chart for
    /// local-only entries (no .mid bundled in this build) or on any parse/download failure, so the
    /// caller always ends up with something to play.
    /// </summary>
    public static class MidiChartLoader
    {
        public static IEnumerator LoadChart(string fileData, int difficulty, Action<Chart> onLoaded)
        {
            Chart chart = null;

            if (!string.IsNullOrEmpty(fileData) && fileData.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                using (UnityWebRequest request = UnityWebRequest.Get(fileData))
                {
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        try
                        {
                            chart = MidiChartParser.Parse(request.downloadHandler.data);
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[Gameplay] Failed to parse MIDI chart from '{fileData}': {e.Message}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[Gameplay] Failed to download chart '{fileData}': {request.error}");
                    }
                }
            }
            else
            {
                Debug.Log($"[Gameplay] Chart '{fileData}' is a local file not bundled in this test build — using a generated chart.");
            }

            if (chart == null || chart.Notes.Count == 0)
                chart = Chart.CreateFallback(difficulty, 60f);

            onLoaded?.Invoke(chart);
        }
    }
}
