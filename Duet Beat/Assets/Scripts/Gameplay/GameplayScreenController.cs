using System;
using System.Collections;
using System.Collections.Generic;
using DuetBeat.SongSelect;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Gameplay
{
    /// <summary>
    /// Six-lane falling-notes gameplay screen (3 lanes for the left character, 3 for the right,
    /// matching the existing duet left/right split). Builds its own UI at runtime (no scene/prefab
    /// wiring required) and is shown/hidden over whatever screen requested it — currently the song list.
    /// </summary>
    public class GameplayScreenController : MonoBehaviour
    {
        private const int LanesPerSide = Chart.LaneCount / 2;
        private const float ReferenceWidth = 1080f;
        private const float ReferenceHeight = 1920f;
        private const float NoteTravelTime = 1.8f;
        private const float HitWindow = 0.15f;
        private const float SpawnY = 860f;
        private const float HitLineY = -650f;
        private const float NoteSize = 90f;

        // Shatter burst shown when a note is successfully hit.
        private const int ShatterPieceCount = 8;
        private const float ShatterPieceSize = 18f;
        private const float ShatterSpeedMin = 250f;
        private const float ShatterSpeedMax = 550f;
        private const float ShatterGravity = 900f;
        private const float ShatterLife = 0.4f;

        private static GameplayScreenController _instance;

        public static GameplayScreenController Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("GameplayScreen (Auto)");
                    _instance = go.AddComponent<GameplayScreenController>();
                    _instance.BuildUi();
                }
                return _instance;
            }
        }

        private Canvas _canvas;
        private readonly RectTransform[] _lanes = new RectTransform[Chart.LaneCount];
        private readonly bool[] _laneHeld = new bool[Chart.LaneCount];
        private TextMeshProUGUI _titleText;
        private RectTransform _progressTrack;
        private Image _progressFill;
        private AudioSource _audioSource;
        private GameObject _songSelectRoot;

        private readonly List<NoteView> _pool = new List<NoteView>();
        private readonly List<NoteView> _active = new List<NoteView>();
        private readonly List<ProgressStarMarker> _progressStarMarkers = new List<ProgressStarMarker>();

        /// <summary>
        /// A star marker pinned on the progress bar at a Star note's exact chart position (time/Duration),
        /// shown dim/uncollected from song start and lit up once that note is actually hit in gameplay —
        /// so its position always matches the MIDI chart, not whatever song-time happened to be at hit.
        /// </summary>
        private class ProgressStarMarker
        {
            public GameObject GameObject;
            public Image Image;
            public float Time;
            public bool Collected;
        }
        private readonly List<ShatterPiece> _shatterPool = new List<ShatterPiece>();
        private readonly List<ShatterPiece> _activeShatter = new List<ShatterPiece>();

        private Chart _chart;
        private int _nextSpawnIndex;
        private float _manualClock;
        private bool _isPlaying;

        private float SongTime =>
            (_audioSource != null && _audioSource.clip != null && _audioSource.isPlaying)
                ? _audioSource.time
                : _manualClock;

        public void StartSong(SongEntry entry, GameObject songSelectRoot)
        {
            if (entry == null)
                return;

            _songSelectRoot = songSelectRoot;
            if (_songSelectRoot != null)
                _songSelectRoot.SetActive(false);

            ResetState();
            _canvas.gameObject.SetActive(true);
            if (_titleText != null)
                _titleText.text = $"{entry.name} - {entry.artistName}";

            StartCoroutine(LoadAndPlayRoutine(entry));
        }

        public void TryHit(int lane)
        {
            if (!_isPlaying)
                return;

            if (lane >= 0 && lane < _laneHeld.Length)
                _laneHeld[lane] = true;

            float songTime = SongTime;
            NoteView best = null;
            float bestDiff = float.MaxValue;

            foreach (NoteView note in _active)
            {
                if (note.Lane != lane || note.Hit || note.Holding)
                    continue;

                float diff = Mathf.Abs(note.HitTime - songTime);
                if (diff <= HitWindow && diff < bestDiff)
                {
                    best = note;
                    bestDiff = diff;
                }
            }

            if (best == null)
                return;

            if (best.Type == NoteType.Hold)
            {
                // Caught — Update() now tracks whether the lane stays held for the full duration.
                best.Holding = true;
                best.Image.color = new Color(0.7f, 1f, 1f);
            }
            else
            {
                Color noteColor = best.Image.color;
                best.Hit = true;
                best.Image.color = new Color(0.4f, 1f, 0.4f);
                SpawnShatterEffect(best, noteColor);
                StartCoroutine(DespawnAfterFlash(best));

                if (best.Type == NoteType.Star)
                    MarkStarCollected(best);
            }
        }

        public void OnLaneReleased(int lane)
        {
            if (lane >= 0 && lane < _laneHeld.Length)
                _laneHeld[lane] = false;
        }

        public void ReturnToSongSelect()
        {
            ResetState();
            _canvas.gameObject.SetActive(false);
            if (_songSelectRoot != null)
                _songSelectRoot.SetActive(true);
        }

        private void Update()
        {
            UpdateShatterPieces();

            if (!_isPlaying || _chart == null)
                return;

            if (_audioSource == null || _audioSource.clip == null || !_audioSource.isPlaying)
                _manualClock += Time.deltaTime;

            float songTime = SongTime;

            if (_progressFill != null && _chart.Duration > 0f)
                _progressFill.fillAmount = Mathf.Clamp01(songTime / _chart.Duration);

            while (_nextSpawnIndex < _chart.Notes.Count &&
                   _chart.Notes[_nextSpawnIndex].time - songTime <= NoteTravelTime)
            {
                SpawnNote(_chart.Notes[_nextSpawnIndex]);
                _nextSpawnIndex++;
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                NoteView note = _active[i];

                if (note.Hit)
                    continue; // already resolved; DespawnAfterFlash removes it shortly — leave it in place.

                // Falling motion is identical for every note — the head (bottom edge for a Hold bar,
                // center for Normal/Big) always tracks HitTime the same way. A Hold note's height
                // (fixed at spawn) makes its tail trail behind, so the whole bar keeps scrolling
                // through the hit line for its full duration instead of stopping/shrinking there.
                float t = 1f - (note.HitTime - songTime) / NoteTravelTime;
                Vector2 pos = note.Rect.anchoredPosition;
                pos.y = Mathf.LerpUnclamped(SpawnY, HitLineY, t);
                note.Rect.anchoredPosition = pos;

                if (note.Type == NoteType.Hold && note.Holding)
                {
                    if (!_laneHeld[note.Lane])
                    {
                        // Released before the tail passed the hit line — broken hold, vanishes now.
                        _active.RemoveAt(i);
                        DespawnNote(note);
                    }
                    else if (songTime >= note.HitTime + note.Duration)
                    {
                        // Held all the way through the tail — success.
                        Color noteColor = note.Image.color;
                        note.Hit = true;
                        note.Image.color = new Color(0.4f, 1f, 0.4f);
                        SpawnShatterEffect(note, noteColor);
                        StartCoroutine(DespawnAfterFlash(note));
                    }
                    continue;
                }

                if (songTime - note.HitTime > HitWindow)
                {
                    _active.RemoveAt(i);
                    DespawnNote(note);
                }
            }

            if (songTime >= _chart.Duration + 1f)
                ReturnToSongSelect();
        }

        private IEnumerator LoadAndPlayRoutine(SongEntry entry)
        {
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.loop = false;
                _audioSource.spatialBlend = 0f; // 2D — must not be attenuated/panned by listener distance.
                _audioSource.volume = 1f;
                _audioSource.mute = false;
            }

            bool chartReady = false;
            Chart loadedChart = null;
            StartCoroutine(MidiChartLoader.LoadChart(entry.fileData, entry.difficulty, c =>
            {
                loadedChart = c;
                chartReady = true;
            }));

            if (!string.IsNullOrEmpty(entry.fileAudio) && entry.fileAudio.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                AudioType audioType = GuessAudioType(entry.fileAudio);

                using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(entry.fileAudio, audioType))
                {
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                        if (clip != null && clip.length > 0f)
                        {
                            _audioSource.clip = clip;
                            Debug.Log($"[Gameplay] Loaded song audio '{entry.fileAudio}' ({clip.length:F1}s).");
                        }
                        else
                        {
                            Debug.LogWarning($"[Gameplay] Song audio '{entry.fileAudio}' downloaded but decoded to an empty clip — playing without audio.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[Gameplay] Failed to load song audio '{entry.fileAudio}': {request.error}");
                    }
                }
            }
            else
            {
                Debug.Log($"[Gameplay] Song audio '{entry.fileAudio}' is a local file not bundled in this test build — playing without audio.");
            }

            yield return new WaitUntil(() => chartReady);

            _chart = loadedChart;
            _nextSpawnIndex = 0;
            _manualClock = 0f;
            _isPlaying = true;
            BuildProgressStarMarkers();

            if (_audioSource.clip != null)
            {
                _audioSource.Play();
                if (!_audioSource.isPlaying)
                    Debug.LogWarning("[Gameplay] AudioSource.Play() was called but isPlaying is false right after — check the Editor's audio isn't muted and Main Camera still has an AudioListener.");
            }
            else
            {
                Debug.Log("[Gameplay] Starting song without audio — falling notes will run on an internal clock instead.");
            }
        }

        /// <summary>
        /// Every fileAudio URL in the song data is effectively an mp3, but some have query strings
        /// (?token=...) after the extension, so a plain EndsWith(".mp3") check can miss them — strip
        /// the query/fragment first, and default to MPEG rather than UNKNOWN since UNKNOWN often fails
        /// to decode at all instead of just guessing wrong.
        /// </summary>
        private static AudioType GuessAudioType(string url)
        {
            int queryIndex = url.IndexOfAny(new[] { '?', '#' });
            string path = queryIndex >= 0 ? url.Substring(0, queryIndex) : url;

            if (path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                return AudioType.OGGVORBIS;
            if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                return AudioType.WAV;

            return AudioType.MPEG;
        }

        private IEnumerator DespawnAfterFlash(NoteView note)
        {
            yield return new WaitForSeconds(0.08f);
            _active.Remove(note);
            DespawnNote(note);
        }

        private void ResetState()
        {
            StopAllCoroutines();

            if (_audioSource != null)
            {
                _audioSource.Stop();
                _audioSource.clip = null;
            }

            foreach (NoteView note in _active)
                DespawnNote(note);
            _active.Clear();

            foreach (ShatterPiece piece in _activeShatter)
                DespawnShatterPiece(piece);
            _activeShatter.Clear();

            for (int i = 0; i < _laneHeld.Length; i++)
                _laneHeld[i] = false;

            if (_progressFill != null)
                _progressFill.fillAmount = 0f;

            foreach (ProgressStarMarker marker in _progressStarMarkers)
            {
                if (marker.GameObject != null)
                    Destroy(marker.GameObject);
            }
            _progressStarMarkers.Clear();

            _chart = null;
            _nextSpawnIndex = 0;
            _manualClock = 0f;
            _isPlaying = false;
        }

        private void SpawnNote(NoteData data)
        {
            NoteView note = GetNote();
            int lane = Mathf.Clamp(data.lane, 0, _lanes.Length - 1);
            RectTransform parent = _lanes[lane];
            note.transform.SetParent(parent, false);

            Vector2 size;
            Vector2 pivot;
            Color color;
            Sprite sprite = null;

            switch (data.type)
            {
                case NoteType.Big:
                    size = new Vector2(NoteSize * 1.6f, NoteSize * 1.6f);
                    pivot = new Vector2(0.5f, 0.5f);
                    color = new Color(1f, 0.7f, 0.2f); // gold — hit harder/stands out.
                    break;

                case NoteType.Hold:
                    float travelPixels = SpawnY - HitLineY;
                    float holdHeight = Mathf.Max(NoteSize * 0.6f, (data.duration / NoteTravelTime) * travelPixels);
                    size = new Vector2(NoteSize * 0.7f, holdHeight);
                    pivot = new Vector2(0.5f, 0f); // bottom-pivoted: bottom edge = head, tracks HitTime.
                    color = new Color(0.3f, 0.75f, 1f); // cyan bar.
                    break;

                case NoteType.Star:
                    size = new Vector2(NoteSize * 1.5f, NoteSize * 1.5f);
                    pivot = new Vector2(0.5f, 0.5f);
                    color = new Color(1f, 0.92f, 0.35f); // bright yellow.
                    sprite = GetStarSprite();
                    break;

                default:
                    size = new Vector2(NoteSize, NoteSize);
                    pivot = new Vector2(0.5f, 0.5f);
                    color = Color.white;
                    break;
            }

            note.ResetState(lane, data, color, size, pivot, sprite);

            Vector2 pos = note.Rect.anchoredPosition;
            pos.y = SpawnY;
            note.Rect.anchoredPosition = pos;

            _active.Add(note);
        }

        /// <summary>
        /// Bursts a handful of small shard pieces outward from a hit note's position, in the note's own
        /// color, which tumble and fade under gravity. Pieces are parented to the same lane rect as the
        /// note so they share its coordinate space. Driven by <see cref="UpdateShatterPieces"/>.
        /// </summary>
        private void SpawnShatterEffect(NoteView note, Color color)
        {
            if (!(note.Rect.parent is RectTransform lane))
                return;

            Vector2 origin = note.Rect.anchoredPosition;

            for (int i = 0; i < ShatterPieceCount; i++)
            {
                ShatterPiece piece = GetShatterPiece();
                piece.transform.SetParent(lane, false);

                float angle = (360f / ShatterPieceCount) * i + UnityEngine.Random.Range(-15f, 15f);
                float speed = UnityEngine.Random.Range(ShatterSpeedMin, ShatterSpeedMax);
                Vector2 velocity = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * speed;
                float size = ShatterPieceSize * UnityEngine.Random.Range(0.6f, 1.2f);

                piece.Spawn(origin, velocity, UnityEngine.Random.Range(-540f, 540f), UnityEngine.Random.Range(0f, 360f),
                    new Vector2(size, size), color, ShatterLife);

                _activeShatter.Add(piece);
            }
        }

        private void UpdateShatterPieces()
        {
            for (int i = _activeShatter.Count - 1; i >= 0; i--)
            {
                ShatterPiece piece = _activeShatter[i];
                piece.Life -= Time.deltaTime;
                if (piece.Life <= 0f)
                {
                    _activeShatter.RemoveAt(i);
                    DespawnShatterPiece(piece);
                    continue;
                }

                piece.Velocity += Vector2.down * ShatterGravity * Time.deltaTime;
                piece.Rect.anchoredPosition += piece.Velocity * Time.deltaTime;
                piece.Rect.Rotate(0f, 0f, piece.AngularVelocity * Time.deltaTime);

                Color c = piece.Image.color;
                c.a = piece.Life / piece.MaxLife;
                piece.Image.color = c;
            }
        }

        private ShatterPiece GetShatterPiece()
        {
            if (_shatterPool.Count > 0)
            {
                ShatterPiece piece = _shatterPool[_shatterPool.Count - 1];
                _shatterPool.RemoveAt(_shatterPool.Count - 1);
                return piece;
            }
            return CreateShatterPieceInstance();
        }

        private void DespawnShatterPiece(ShatterPiece piece)
        {
            piece.gameObject.SetActive(false);
            piece.transform.SetParent(transform, false);
            _shatterPool.Add(piece);
        }

        private ShatterPiece CreateShatterPieceInstance()
        {
            var go = new GameObject("ShatterPiece", typeof(RectTransform), typeof(Image), typeof(ShatterPiece));
            go.transform.SetParent(transform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            // Purely decorative — must not intercept taps meant for the lane's hit zone.
            go.GetComponent<Image>().raycastTarget = false;

            return go.GetComponent<ShatterPiece>();
        }

        /// <summary>
        /// Pins one dim star icon on the progress bar for every Star note in the chart, positioned at
        /// <c>note.time / chart.Duration</c> — the same fraction the fill bar itself uses — so every
        /// marker's position matches the MIDI exactly and is known up front, before any note is hit.
        /// Called once per song load; cleared on <see cref="ResetState"/>.
        /// </summary>
        private void BuildProgressStarMarkers()
        {
            if (_progressTrack == null || _chart == null || _chart.Duration <= 0f)
                return;

            foreach (NoteData note in _chart.Notes)
            {
                if (note.type != NoteType.Star)
                    continue;

                float fraction = Mathf.Clamp01(note.time / _chart.Duration);

                var go = new GameObject("StarMarker", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_progressTrack, false);

                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(fraction, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(26f, 26f);
                rect.anchoredPosition = Vector2.zero;

                Image image = go.GetComponent<Image>();
                image.sprite = GetStarSprite();
                image.color = new Color(1f, 1f, 1f, 0.35f); // dim/uncollected until the note is actually hit.
                image.raycastTarget = false;

                _progressStarMarkers.Add(new ProgressStarMarker
                {
                    GameObject = go,
                    Image = image,
                    Time = note.time,
                    Collected = false,
                });
            }
        }

        /// <summary>
        /// Lights up the pre-placed progress-bar marker for the Star note that was just hit, so the
        /// moment the fill sweeps past that point it visually lines up with an already-lit star instead
        /// of a freshly-spawned one at wherever song-time happened to be.
        /// </summary>
        private void MarkStarCollected(NoteView note)
        {
            ProgressStarMarker marker = null;
            float bestDiff = float.MaxValue;

            foreach (ProgressStarMarker candidate in _progressStarMarkers)
            {
                if (candidate.Collected)
                    continue;

                float diff = Mathf.Abs(candidate.Time - note.HitTime);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    marker = candidate;
                }
            }

            if (marker == null)
                return;

            marker.Collected = true;
            marker.Image.color = new Color(1f, 0.92f, 0.35f, 1f);
            marker.Image.rectTransform.sizeDelta = new Vector2(32f, 32f); // small pop for hit feedback.
        }

        private NoteView GetNote()
        {
            if (_pool.Count > 0)
            {
                NoteView note = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
                return note;
            }
            return CreateNoteInstance();
        }

        private void DespawnNote(NoteView note)
        {
            note.gameObject.SetActive(false);
            note.transform.SetParent(transform, false);
            _pool.Add(note);
        }

        private static Sprite _solidSprite;

        /// <summary>
        /// A 1x1 solid-white sprite, generated once and cached, for UI elements that need a real
        /// Sprite to behave correctly (e.g. <see cref="Image.Type.Filled"/>) but have no art asset.
        /// </summary>
        private static Sprite GetSolidSprite()
        {
            if (_solidSprite != null)
                return _solidSprite;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            _solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            return _solidSprite;
        }

        private static Sprite _starSprite;

        /// <summary>
        /// Draws a simple 5-point star into a runtime Texture2D and wraps it as a Sprite the first time
        /// it's needed, then reuses that same sprite for every Star note — no art asset required.
        /// </summary>
        private static Sprite GetStarSprite()
        {
            if (_starSprite != null)
                return _starSprite;

            const int size = 64;
            const int points = 5;
            var center = new Vector2(size / 2f, size / 2f);
            float outerRadius = size * 0.48f;
            float innerRadius = outerRadius * 0.42f;

            var poly = new Vector2[points * 2];
            for (int i = 0; i < poly.Length; i++)
            {
                float angle = -Mathf.PI / 2f + i * Mathf.PI / points;
                float r = (i % 2 == 0) ? outerRadius : innerRadius;
                poly[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = IsPointInPolygon(new Vector2(x + 0.5f, y + 0.5f), poly);
                    pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            _starSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
            return _starSprite;
        }

        private static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private NoteView CreateNoteInstance()
        {
            var go = new GameObject("Note", typeof(RectTransform), typeof(Image), typeof(NoteView));
            go.transform.SetParent(transform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(NoteSize, NoteSize);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            // Notes are purely visual — raycastTarget must stay off, otherwise a tap landing on a
            // falling note would be consumed by it instead of reaching the lane's LaneHitZone.
            go.GetComponent<Image>().raycastTarget = false;

            return go.GetComponent<NoteView>();
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("GameplayCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;

            Image background = CreateImage("Background", canvasGo.transform, new Color(0.05f, 0.05f, 0.08f, 0.95f));
            StretchFull(background.rectTransform);

            // 6 lanes total: lanes [0..LanesPerSide) belong to the left character's side of the
            // screen, [LanesPerSide..LaneCount) to the right character's side — same left/right split
            // as the existing duet character system, just subdivided into 3 lanes per side.
            for (int lane = 0; lane < Chart.LaneCount; lane++)
            {
                bool leftSide = lane < LanesPerSide;
                int indexInSide = leftSide ? lane : lane - LanesPerSide;
                float sideMin = leftSide ? 0f : 0.5f;
                float laneWidth = 0.5f / LanesPerSide;
                float xMin = sideMin + indexInSide * laneWidth;
                float xMax = xMin + laneWidth;

                RectTransform laneRect = CreateLaneRect($"Lane{lane}", canvasGo.transform, xMin, xMax);
                _lanes[lane] = laneRect;

                CreateHitLineVisual(laneRect);
                CreateHitZone(laneRect, lane);

                // Thin divider between every lane; a more visible one at the left/right side boundary.
                if (lane > 0)
                {
                    bool sideBoundary = lane == LanesPerSide;
                    Image divider = CreateImage("Divider", canvasGo.transform,
                        new Color(1f, 1f, 1f, sideBoundary ? 0.25f : 0.08f));
                    divider.rectTransform.anchorMin = new Vector2(xMin, 0f);
                    divider.rectTransform.anchorMax = new Vector2(xMin, 1f);
                    divider.rectTransform.sizeDelta = new Vector2(sideBoundary ? 5f : 2f, 0f);
                    divider.rectTransform.anchoredPosition = Vector2.zero;
                }
            }

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(canvasGo.transform, false);
            _titleText = titleGo.AddComponent<TextMeshProUGUI>();
            _titleText.alignment = TextAlignmentOptions.Center;
            _titleText.fontSize = 56;
            _titleText.color = Color.white;
            _titleText.raycastTarget = false; // sits over the lanes; must not swallow taps meant for them.
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0f, 140f);
            titleRect.anchoredPosition = new Vector2(0f, -20f);

            CreateProgressBar(canvasGo.transform);
            CreateBackButton(canvasGo.transform);

            canvasGo.SetActive(false);
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false; // decorative only; only LaneHitZone/BackButton should catch taps.
            return image;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static RectTransform CreateLaneRect(string name, Transform parent, float xMin, float xMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(xMin, 0f);
            rect.anchorMax = new Vector2(xMax, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void CreateHitLineVisual(RectTransform lane)
        {
            Image line = CreateImage("HitLine", lane, new Color(1f, 1f, 1f, 0.5f));
            line.rectTransform.anchorMin = new Vector2(0.1f, 0.5f);
            line.rectTransform.anchorMax = new Vector2(0.9f, 0.5f);
            line.rectTransform.sizeDelta = new Vector2(0f, 8f);
            line.rectTransform.anchoredPosition = new Vector2(0f, HitLineY);
        }

        private void CreateHitZone(RectTransform lane, int laneIndex)
        {
            var go = new GameObject("HitZone", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(lane, false);
            StretchFull(go.GetComponent<RectTransform>());
            Image hitZoneImage = go.GetComponent<Image>();
            hitZoneImage.color = new Color(0f, 0f, 0f, 0.001f);
            hitZoneImage.raycastTarget = true; // the actual tap surface for this lane.

            LaneHitZone zone = go.AddComponent<LaneHitZone>();
            zone.lane = laneIndex;
            zone.controller = this;
        }

        private void CreateProgressBar(Transform parent)
        {
            var trackGo = new GameObject("ProgressTrack", typeof(RectTransform), typeof(Image));
            trackGo.transform.SetParent(parent, false);
            _progressTrack = trackGo.GetComponent<RectTransform>();
            _progressTrack.anchorMin = new Vector2(0f, 1f);
            _progressTrack.anchorMax = new Vector2(1f, 1f);
            _progressTrack.pivot = new Vector2(0.5f, 1f);
            _progressTrack.sizeDelta = new Vector2(-80f, 16f); // -80 => 40px margin on each side.
            _progressTrack.anchoredPosition = new Vector2(0f, -170f); // just below the title.
            Image track = trackGo.GetComponent<Image>();
            track.color = new Color(1f, 1f, 1f, 0.15f);
            track.raycastTarget = false;

            var fillGo = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(_progressTrack, false);
            StretchFull(fillGo.GetComponent<RectTransform>());
            _progressFill = fillGo.GetComponent<Image>();
            // Image.Type.Filled only clips correctly when a sprite is assigned — without one, fillAmount
            // changes don't reliably show up. A 1x1 solid sprite makes it behave like a plain color fill.
            _progressFill.sprite = GetSolidSprite();
            _progressFill.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            _progressFill.raycastTarget = false;
            _progressFill.type = Image.Type.Filled;
            _progressFill.fillMethod = Image.FillMethod.Horizontal;
            _progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _progressFill.fillAmount = 0f;
        }

        private void CreateBackButton(Transform parent)
        {
            var go = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(160f, 90f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = "Back";
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 40;
            text.color = Color.white;
            StretchFull(textGo.GetComponent<RectTransform>());

            go.GetComponent<Button>().onClick.AddListener(ReturnToSongSelect);
        }
    }
}
