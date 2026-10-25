using System;
using MiniChess.Bot.Search;
using UnityEngine;

namespace MiniChess.Client.Players
{
    /// <summary>
    /// 봇 난이도 "Custom" 의 탐색 설정(인스펙터용). 깊이별 연산량(한 턴, 기본값 기준):
    ///   - 깊이 0: 복사/시뮬레이션 없음. 행동마다 모든 후보에 점수만 매긴다(턴당 약 1ms).
    ///   - 깊이 1: 행동 수 × 남길 순서 수 × 후보 수 만큼 행동을 복사한 경기에서 실행해 보고 판 점수를 매긴다.
    ///            10 × 4 × 6 = 최대 240판(턴당 약 10ms).
    ///   - 깊이 2: 깊이 1 에 더해 상위 응수 후보 수만큼 상대의 한 턴(규칙 봇)을 통째로 실행해 본다.
    ///            어려움(10 × 6 × 8 = 480판 + 응수 6턴)이 턴당 약 30ms.
    ///   - 깊이 3: 응수 후보마다 내 다음 턴을 깊이 1 로 다시 읽는다. 깊이 1 탐색이 응수 후보 수만큼 더 돌아
    ///            기본값으로 최대 240 + 6 × 240판. 시간 제한(timeLimitMs)에 닿으면 그때까지 본 것으로 고른다.
    /// 넓이 값을 키우면 연산은 곱으로 늘지만, 대전 결과로는 넓이보다 깊이의 효과가 컸다.
    /// 봇 계산은 별도 스레드에서 돌아 화면이 멈추지 않는다(BotDriver).
    /// </summary>
    [Serializable]
    public class BotSearchConfig
    {
        [Tooltip("0 = 규칙 봇(실수 없음), 1 = 이번 턴 행동 순서까지, 2 = 상대 응수까지, 3 = 응수 뒤 내 다음 턴까지.")]
        [Range(0, 3)] public int depth = 1;

        [Tooltip("행동 하나마다 남겨 두는 행동 순서 수(빔 넓이).")]
        [Range(1, 16)] public int beamWidth = 4;

        [Tooltip("남긴 순서마다 이어서 실행해 볼 후보 수(규칙 봇 점수가 높은 것부터).")]
        [Range(1, 20)] public int candidatesPerStep = 6;

        [Tooltip("한 턴에 읽는 행동 수 상한.")]
        [Range(1, 20)] public int maxActions = 10;

        [Tooltip("깊이 2 에서 상대 응수까지 실행해 볼 순서 수.")]
        [Range(1, 20)] public int replyCandidates = 6;

        [Tooltip("한 번 읽을 때의 시간 제한(ms). 넘으면 그때까지 읽은 것 중에서 고른다. 0 = 제한 없음.")]
        [Min(0)] public int timeLimitMs = 500;

        public SearchSettings ToSettings()
        {
            return new SearchSettings
            {
                Depth = depth,
                BeamWidth = beamWidth,
                CandidatesPerStep = candidatesPerStep,
                MaxActions = maxActions,
                ReplyCandidates = replyCandidates,
                TimeLimitMs = timeLimitMs,
            };
        }
    }
}
