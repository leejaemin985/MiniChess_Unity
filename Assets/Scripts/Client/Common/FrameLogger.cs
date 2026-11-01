using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace MiniChess.Client.Common
{
    /// <summary>
    /// 프레임 성능 기록기(에디터, 개발 빌드에서만 자동으로 켜짐). 플레이를 시작하고 warmup 초 뒤부터 기록한다(1초 넘는 로딩 프레임은 따로 셈).
    ///   - interval 초마다 구간 통계를 콘솔에 한 줄 남긴다: 평균 FPS/ms, 최소·최대 ms, 1% 저점 FPS, 33ms(30fps) 넘은 프레임 수,
    ///     드로우(Batches), SetPass, 삼각형, 정점, 프레임당 GC 할당, 사용 메모리.
    ///   - 플레이를 끝내면(또는 앱 종료) 전체 요약을 남기고, 구간 기록을 CSV 로 저장한다.
    ///     에디터: 프로젝트/Logs/FrameLog_날짜_시각.csv, 빌드: Application.persistentDataPath.
    ///   - F9: 기록 일시정지/재개(로딩 등 빼고 싶은 구간). Mark("배치 시작") 으로 CSV 에 구간 이름을 남길 수 있다.
    /// 드로우/삼각형 수는 Unity 렌더 통계(ProfilerRecorder)라 에디터에서는 씬 뷰 등 다른 창 렌더링이 섞일 수 있다(Game 뷰만 띄우고 재면 정확).
    /// </summary>
    public class FrameLogger : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float interval = 5f;
        [SerializeField] private KeyCode pauseKey = KeyCode.F9;
        [Tooltip("이 시간(ms)을 넘은 프레임을 끊김으로 센다.")]
        [SerializeField, Min(1f)] private float spikeMs = 33.3f;
        [Tooltip("플레이 시작 후 이 시간(초)과 첫 프레임들은 기록하지 않는다(플레이 진입/맵 로딩이 통계를 흐리지 않게).")]
        [SerializeField, Min(0f)] private float warmup = 3f;
        [Tooltip("이보다 긴 프레임(ms)은 로딩으로 보고 통계에서 빼되 따로 센다.")]
        [SerializeField, Min(100f)] private float loadingMs = 1000f;

        private static FrameLogger _instance;

        private readonly List<float> _frameMs = new List<float>();
        private readonly List<float> _allMs = new List<float>();
        private readonly StringBuilder _csv = new StringBuilder();
        private float _elapsed;
        /// <summary>기록 시작부터 누적한 시간(초). realtimeSinceStartup 은 플레이 종료 때 다시 시작되므로 직접 센다.</summary>
        private float _time;
        private int _loadingFrames;
        private bool _paused;
        private string _mark = "";
        private long _maxTriangles;
        private long _maxBatches;

        private ProfilerRecorder _batches;
        private ProfilerRecorder _setPass;
        private ProfilerRecorder _triangles;
        private ProfilerRecorder _vertices;
        private ProfilerRecorder _gcAlloc;
        private ProfilerRecorder _usedMemory;
        private long _sumBatches, _sumSetPass, _sumTriangles, _sumVertices, _sumGc;
        private int _samples;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (_instance != null)
                return;

            var go = new GameObject("[FrameLogger]");
            DontDestroyOnLoad(go);
            go.AddComponent<FrameLogger>();
        }
#endif

        /// <summary>지금부터의 구간에 이름을 붙인다(CSV mark 열). 기록기가 없으면 무시.</summary>
        public static void Mark(string label)
        {
            if (_instance != null)
                _instance._mark = label ?? "";
        }

        private void Awake()
        {
            _instance = this;
        }

        private void OnEnable()
        {
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _vertices = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
            _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _usedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");

            _time = 0f;
            _csv.Clear();
            _csv.AppendLine("time_s,mark,frames,avg_fps,avg_ms,min_ms,max_ms,low1_fps,spikes,batches,setpass,triangles,vertices,gc_kb_per_frame,used_mb,screen");
            Debug.Log($"[FrameLogger] 기록 시작({interval:0.#}초마다 콘솔에 한 줄, {pauseKey} = 일시정지/재개)");
        }

        private void OnDisable()
        {
            Flush();
            WriteSummary();

            _batches.Dispose();
            _setPass.Dispose();
            _triangles.Dispose();
            _vertices.Dispose();
            _gcAlloc.Dispose();
            _usedMemory.Dispose();
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            if (Input.GetKeyDown(pauseKey))
            {
                _paused = !_paused;
                if (_paused)
                    Flush();
                Debug.Log($"[FrameLogger] {(_paused ? "일시정지" : "재개")}");
            }

            if (_paused)
                return;

            _time += Time.unscaledDeltaTime;
            if (_time < warmup)
                return;

            float ms = Time.unscaledDeltaTime * 1000f;
            if (ms > loadingMs)
            {
                _loadingFrames++;
                return;
            }

            _frameMs.Add(ms);
            _allMs.Add(ms);

            _sumBatches += _batches.LastValue;
            _sumSetPass += _setPass.LastValue;
            _sumTriangles += _triangles.LastValue;
            _sumVertices += _vertices.LastValue;
            _sumGc += _gcAlloc.LastValue;
            _maxTriangles = Math.Max(_maxTriangles, _triangles.LastValue);
            _maxBatches = Math.Max(_maxBatches, _batches.LastValue);
            _samples++;

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed >= interval)
                Flush();
        }

        /// <summary>지금까지 모인 구간을 콘솔/CSV 로 내보내고 비운다.</summary>
        private void Flush()
        {
            if (_frameMs.Count == 0)
                return;

            Stats stats = Measure(_frameMs);
            int n = Math.Max(1, _samples);
            long batches = _sumBatches / n, setPass = _sumSetPass / n, triangles = _sumTriangles / n, vertices = _sumVertices / n;
            float gcKb = _sumGc / (float)n / 1024f;
            float usedMb = _usedMemory.LastValue / (1024f * 1024f);
            float time = _time;

            Debug.Log($"[FrameLogger] {time,6:0.0}s {_mark} | {stats.AvgFps,5:0.0}fps ({stats.AvgMs:0.0}ms, 최소 {stats.MinMs:0.0} 최대 {stats.MaxMs:0.0}, 1%저점 {stats.Low1Fps:0.0}fps, 끊김 {stats.Spikes}) " +
                      $"| 드로우 {batches} SetPass {setPass} 삼각형 {triangles / 1000f:0.0}k 정점 {vertices / 1000f:0.0}k | GC {gcKb:0.0}KB/f 메모리 {usedMb:0}MB");

            _csv.AppendLine(string.Join(",",
                time.ToString("0.0"), _mark.Replace(",", " "), _frameMs.Count, stats.AvgFps.ToString("0.0"), stats.AvgMs.ToString("0.00"),
                stats.MinMs.ToString("0.00"), stats.MaxMs.ToString("0.00"), stats.Low1Fps.ToString("0.0"), stats.Spikes,
                batches, setPass, triangles, vertices, gcKb.ToString("0.0"), usedMb.ToString("0"), $"{Screen.width}x{Screen.height}"));

            _frameMs.Clear();
            _sumBatches = _sumSetPass = _sumTriangles = _sumVertices = _sumGc = 0;
            _samples = 0;
            _elapsed = 0f;
        }

        private void WriteSummary()
        {
            if (_allMs.Count == 0)
                return;

            Stats total = Measure(_allMs);
            string path = SaveCsv();
            Debug.Log($"[FrameLogger] 요약: {_allMs.Count}프레임, 평균 {total.AvgFps:0.0}fps ({total.AvgMs:0.0}ms), 1%저점 {total.Low1Fps:0.0}fps, " +
                      $"최대 {total.MaxMs:0.0}ms, 끊김({spikeMs:0}ms 초과) {total.Spikes}회, 로딩({loadingMs:0}ms 초과, 통계 제외) {_loadingFrames}회, " +
                      $"최대 삼각형 {_maxTriangles / 1000f:0.0}k, 최대 드로우 {_maxBatches}" +
                      (path != null ? $"\nCSV: {path}" : ""));
        }

        private string SaveCsv()
        {
            try
            {
#if UNITY_EDITOR
                string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs");
#else
                string folder = Application.persistentDataPath;
#endif
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, $"FrameLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                File.WriteAllText(path, _csv.ToString(), new UTF8Encoding(true));
                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FrameLogger] CSV 저장 실패: {e.Message}");
                return null;
            }
        }

        private struct Stats
        {
            public float AvgMs, MinMs, MaxMs, AvgFps, Low1Fps;
            public int Spikes;
        }

        private Stats Measure(List<float> frames)
        {
            var sorted = new List<float>(frames);
            sorted.Sort();
            float sum = 0f;
            int spikes = 0;
            foreach (float ms in frames)
            {
                sum += ms;
                if (ms > spikeMs)
                    spikes++;
            }

            // 1% 저점: 가장 느린 1% 프레임의 평균을 FPS 로
            int worstCount = Math.Max(1, sorted.Count / 100);
            float worstSum = 0f;
            for (int i = sorted.Count - worstCount; i < sorted.Count; i++)
                worstSum += sorted[i];

            float avg = sum / frames.Count;
            return new Stats
            {
                AvgMs = avg,
                MinMs = sorted[0],
                MaxMs = sorted[sorted.Count - 1],
                AvgFps = 1000f / Mathf.Max(0.001f, avg),
                Low1Fps = 1000f / Mathf.Max(0.001f, worstSum / worstCount),
                Spikes = spikes,
            };
        }
    }
}
