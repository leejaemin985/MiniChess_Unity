using System;
using System.Collections;
using System.Collections.Generic;
using MiniChess.Client.Common;
using UnityEngine;
using UnityEngine.SceneManagement;
using CoreBoard = MiniChess.Core.State.Board;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 맵 씬을 불러와 경기에 쓸 판 정보를 꺼낸다(GameSession 에서 맵 준비를 떼어 낸 것).
    ///   1. Load: 맵 씬을 지금 씬 위에 덧붙여(Additive) 불러오고, 그 씬의 MapSceneSettings 를 찾는다. 이미 불러온 맵이 있으면 먼저 내린다.
    ///      맵 씬을 활성 씬으로 해 그 맵의 하늘/주변광을 쓴다(햇빛/볼륨/배경 안개도 맵 씬에 있다). 불러오는 쪽 씬은 환경 설정을 갖지 않는다.
    ///   2. Prepare: 코어 보드와 맵 씬이 맞는지 검사하고(MapSceneSettings.Validate), 맵의 칸 크기로 좌표 변환(BoardCoordinates)을 만든다.
    /// 맵 씬은 Build Settings 의 씬 목록에 들어 있어야 불러올 수 있다.
    /// 규칙/판정은 코어가 하므로, 검사에 실패한 맵으로는 경기를 시작하지 않는다(보이는 판과 판정이 어긋나므로).
    /// </summary>
    public class MapLoader : MonoBehaviour
    {
        [Tooltip("따로 정하지 않았을 때 불러올 맵 씬 이름.")]
        [SerializeField] private string defaultMapScene = "RuinsMap";

        private Scene _scene;

        /// <summary>불러온 맵 씬의 등록표. 불러오지 않았으면 null.</summary>
        public MapSceneSettings Current { get; private set; }

        public string DefaultMapScene => defaultMapScene;

        /// <summary>맵 씬을 불러오는 중인지.</summary>
        public bool IsLoading { get; private set; }

        /// <summary>
        /// 맵 씬을 불러온다(코루틴). 끝나면 onLoaded 에 등록표를 넘긴다(불러오지 못했으면 null).
        /// 같은 씬이 이미 올라와 있으면 다시 불러오지 않는다.
        /// </summary>
        public IEnumerator Load(string sceneName, Action<MapSceneSettings> onLoaded)
        {
            if (string.IsNullOrEmpty(sceneName))
                sceneName = defaultMapScene;

            if (Current != null && _scene.IsValid() && _scene.isLoaded && _scene.name == sceneName)
            {
                onLoaded?.Invoke(Current);
                yield break;
            }

            IsLoading = true;
            yield return UnloadRoutine();

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[MapLoader] 맵 씬 '{sceneName}' 을 불러올 수 없음(Build Settings 의 씬 목록에 있는지 확인)");
                IsLoading = false;
                onLoaded?.Invoke(null);
                yield break;
            }

            AsyncOperation loading = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            yield return loading;

            _scene = SceneManager.GetSceneByName(sceneName);
            Current = FindSettings(_scene);
            IsLoading = false;

            // 하늘/주변광/안개(RenderSettings)는 활성 씬의 것을 쓰므로 맵 씬을 활성으로 한다(맵마다 분위기가 다르고, 불러오는 씬은 영향을 주지 않게).
            if (_scene.IsValid())
                SceneManager.SetActiveScene(_scene);

            if (Current == null)
                Debug.LogError($"[MapLoader] 맵 씬 '{sceneName}' 에 MapSceneSettings 가 없음");

            onLoaded?.Invoke(Current);
        }

        /// <summary>
        /// 불러온 맵이 코어 보드와 맞는지 검사하고, 맞으면 맵의 칸 크기로 좌표 변환을 만든다.
        /// 맞지 않으면 어긋난 점을 로그로 남기고 false.
        /// </summary>
        public bool TryPrepare(CoreBoard board, out BoardCoordinates coordinates)
        {
            coordinates = null;
            if (Current == null)
            {
                Debug.LogError("[MapLoader] 불러온 맵이 없음");
                return false;
            }

            List<string> issues = Current.Validate(board);

            // 좌표 변환(BoardCoordinates)은 칸 (0, 0) 이 월드 원점이라고 본다 → 맵 루트가 원점에 회전/크기 없이 있어야 한다.
            Transform root = Current.transform;
            if (root.position != Vector3.zero || root.rotation != Quaternion.identity || root.lossyScale != Vector3.one)
                issues.Add($"맵 루트 '{root.name}' 가 원점에 회전/크기 없이 있어야 함(위치 {root.position}, 회전 {root.eulerAngles}, 크기 {root.lossyScale})");

            if (issues.Count > 0)
            {
                Debug.LogError($"[MapLoader] 맵 '{Current.MapId}' 이 코어 보드와 맞지 않음:\n- " + string.Join("\n- ", issues), Current);
                return false;
            }

            coordinates = new BoardCoordinates(board.Width, board.Height, Current.CellSize);
            return true;
        }

        /// <summary>불러온 맵 씬을 내린다(코루틴).</summary>
        public IEnumerator Unload()
        {
            yield return UnloadRoutine();
        }

        private IEnumerator UnloadRoutine()
        {
            Current = null;
            if (!_scene.IsValid() || !_scene.isLoaded)
                yield break;

            if (SceneManager.GetActiveScene() == _scene)
                SceneManager.SetActiveScene(gameObject.scene);

            AsyncOperation unloading = SceneManager.UnloadSceneAsync(_scene);
            _scene = default;
            if (unloading != null)
                yield return unloading;
        }

        private static MapSceneSettings FindSettings(Scene scene)
        {
            if (!scene.IsValid())
                return null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var settings = root.GetComponentInChildren<MapSceneSettings>(true);
                if (settings != null)
                    return settings;
            }

            return null;
        }
    }
}
