using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MiniChess.Bot.Engine;
using UnityEngine;

namespace MiniChess.Client.UI
{
    /// <summary>
    /// 개발용 봇 벤치마크 패널(개발 빌드와 에디터에서만 생긴다). 화면 오른쪽 위 "Bot Bench" 버튼으로 열어
    /// 레벨별 한 턴 판단 시간(BotBenchmark)을 이 기기에서 잰다. 씬에 두지 않고 시작할 때 스스로 만든다.
    /// 결과는 화면, 로그("[BotBench]", Android 는 adb logcat), 파일(persistentDataPath/bot_benchmark.txt)에 남긴다.
    /// 측정은 별도 스레드에서 하며, 측정 중에는 기기가 느려질 수 있다(경기와 동시에 돌리지 않는 것이 정확하다).
    /// 화면 글자는 영어다(IMGUI 기본 폰트에 한글이 없는 기기가 있다).
    /// </summary>
    public class BotBenchmarkPanel : MonoBehaviour
    {
        private const int TurnsPerLevel = 5;
        private const string LogTag = "[BotBench]";

        private readonly object _lock = new object();
        private readonly List<string> _lines = new List<string>();
        private readonly List<BenchmarkTurn> _turns = new List<BenchmarkTurn>();
        private volatile CancellationTokenSource _running;
        private bool _open;
        private Vector2 _scroll;
        private string _filePath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (!Debug.isDebugBuild && !Application.isEditor)
                return;

            var go = new GameObject("BotBenchmarkPanel");
            DontDestroyOnLoad(go);
            go.AddComponent<BotBenchmarkPanel>();
        }

        private void Awake()
        {
            _filePath = Path.Combine(Application.persistentDataPath, "bot_benchmark.txt");
        }

        private void OnDestroy()
        {
            _running?.Cancel();
        }

        private void Begin(int[] levels)
        {
            if (_running != null)
                return;

            lock (_lock)
            {
                _lines.Clear();
                _turns.Clear();
            }

            AddLine($"device: {SystemInfo.deviceModel}, cpu: {SystemInfo.processorType} x{SystemInfo.processorCount}, ram: {SystemInfo.systemMemorySize}MB");
            AddLine($"platform: {Application.platform}, backend: {Backend}, levels: {string.Join(",", levels)}, turns/level: {TurnsPerLevel}");

            _running = new CancellationTokenSource();
            CancellationToken cancellation = _running.Token;
            Task.Run(() =>
            {
                try
                {
                    BotBenchmark.Run(levels, TurnsPerLevel, BotBenchmark.DefaultSeed, turn =>
                    {
                        lock (_lock)
                            _turns.Add(turn);
                        AddLine(turn.ToString());
                    }, cancellation);

                    List<BenchmarkTurn> turns;
                    lock (_lock)
                        turns = _turns.ToList();
                    AddLine(cancellation.IsCancellationRequested ? "-- cancelled --" : "-- summary --");
                    foreach (string line in BotBenchmark.Summary(turns))
                        AddLine(line);
                }
                catch (Exception e)
                {
                    AddLine("error: " + e);
                }
                finally
                {
                    SaveFile();
                    _running = null;
                }
            });
        }

        private static string Backend
        {
            get
            {
#if ENABLE_IL2CPP
                return "IL2CPP";
#else
                return "Mono";
#endif
            }
        }

        private void AddLine(string line)
        {
            lock (_lock)
                _lines.Add(line);
            Debug.Log($"{LogTag} {line}");
        }

        private void SaveFile()
        {
            try
            {
                string text;
                lock (_lock)
                    text = string.Join("\n", _lines);
                File.WriteAllText(_filePath, text, Encoding.UTF8);
                Debug.Log($"{LogTag} saved: {_filePath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} save failed: {e.Message}");
            }
        }

        private void OnGUI()
        {
            // 기기 해상도와 무관하게 비슷한 크기로 보이도록 짧은 변 720 기준으로 키운다.
            float scale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            if (GUI.Button(new Rect(width - 110, 10, 100, 34), _running != null ? "Bench..." : "Bot Bench"))
                _open = !_open;
            if (!_open)
                return;

            var area = new Rect(10, 50, width - 20, height - 60);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 8, area.y + 8, area.width - 16, area.height - 16));

            GUILayout.BeginHorizontal();
            GUI.enabled = _running == null;
            if (GUILayout.Button("L1-10", GUILayout.Height(36)))
                Begin(Enumerable.Range(1, 10).ToArray());
            if (GUILayout.Button("L7-10", GUILayout.Height(36)))
                Begin(new[] { 7, 8, 9, 10 });
            if (GUILayout.Button("L10", GUILayout.Height(36)))
                Begin(new[] { 10 });
            GUI.enabled = _running != null;
            if (GUILayout.Button("Cancel", GUILayout.Height(36)))
                _running?.Cancel();
            GUI.enabled = true;
            if (GUILayout.Button("Close", GUILayout.Height(36)))
                _open = false;
            GUILayout.EndHorizontal();

            GUILayout.Label(_running != null ? "Running... (device may lag)" : $"file: {_filePath}");

            _scroll = GUILayout.BeginScrollView(_scroll);
            lock (_lock)
            {
                foreach (string line in _lines)
                    GUILayout.Label(line);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
