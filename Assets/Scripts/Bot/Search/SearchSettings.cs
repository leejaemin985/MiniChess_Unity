namespace MiniChess.Bot.Search
{
    /// <summary>탐색 봇(SearchBot)이 얼마나 넓고 깊게 읽을지. 난이도(BotDifficulty)가 이 값을 정한다.</summary>
    public class SearchSettings
    {
        /// <summary>
        /// 읽는 깊이. 1 = 이번 내 턴의 행동 순서를 실행해 보고 턴을 끝낸 판으로 고른다.
        /// 2 = 거기에 상대의 다음 턴 응수(규칙 봇으로 가정)까지 실행해 본 판으로 고른다.
        /// 3 = 응수 뒤 내 다음 턴도 깊이 1 로 읽어 본 점수로 고른다(응수 후보마다 깊이 1 탐색 한 번이라 연산이 크다).
        /// </summary>
        public int Depth { get; set; } = 1;

        /// <summary>한 단계(행동 하나)마다 남겨 두는 행동 순서 수.</summary>
        public int BeamWidth { get; set; } = 4;

        /// <summary>남겨 둔 순서마다 이어서 실행해 볼 후보 수(규칙 봇 점수가 높은 것부터).</summary>
        public int CandidatesPerStep { get; set; } = 6;

        /// <summary>
        /// 후보 중 같은 유닛의 같은 스킬(지정 칸만 다른 변형)이 차지할 수 있는 수.
        /// 한 스킬의 비슷한 변형들이 후보를 모두 차지해 다른 행동을 읽지 못하는 것을 막는다.
        /// </summary>
        public int MaxVariantsPerSkill { get; set; } = 2;

        /// <summary>한 턴에 읽는 행동 수 상한.</summary>
        public int MaxActions { get; set; } = 10;

        /// <summary>깊이 2 에서 상대 응수까지 실행해 볼 행동 순서 수(깊이 1 판 점수가 높은 것부터).</summary>
        public int ReplyCandidates { get; set; } = 6;

        /// <summary>규칙 봇 점수가 이 값 이하인 후보는 실행해 보지 않는다(쓸모없는 이동 등). 음수면 다소 손해인 준비 행동도 본다.</summary>
        public double MinCandidateScore { get; set; } = -1;

        /// <summary>한 번 읽을 때의 시간 제한(ms). 넘으면 그때까지 읽은 것 중에서 고른다. 0 이면 제한 없음(테스트는 결과가 매번 같도록 0).</summary>
        public int TimeLimitMs { get; set; }
    }
}
