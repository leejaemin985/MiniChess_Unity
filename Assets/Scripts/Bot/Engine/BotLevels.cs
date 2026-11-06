using System;

namespace MiniChess.Bot.Engine
{
    /// <summary>
    /// 레벨(1~10) 별 엔진 설정. 레벨 테스트(봇 깨기)용으로, 인접 레벨끼리 대전(Arena)해 위 레벨이 이기도록 맞춘다.
    ///   1~5 : 이번 턴만 읽는다(깊이 1). 낮을수록 한 턴 행동 수가 적고, 더 낮은 순위의 계획을 자주 고른다.
    ///   6   : 깊이 1, 실수 없음.
    ///   7~10: 상대 응수와 그 뒤 내 턴까지 읽는다(깊이 3). 높을수록 비교하는 계획이 많다.
    /// 대전으로 확인한 점(2026-11-06): 깊이 1 에서는 빔 폭을 넓혀도 거의 강해지지 않고, 깊이 3 도 빔 2 면 깊이 1 보다 약하다.
    /// 실력 차이는 한 턴 행동 수 제한, 실수 확률/범위, 깊이 3(빔 4 이상)에서 난다.
    /// 실력은 시간이 아니라 탐색량으로 정해 기기와 무관하게 같고, 시간 상한(60초)은 안전장치로만 둔다.
    /// </summary>
    public static class BotLevels
    {
        public const int Min = 1;
        public const int Max = 10;

        /// <summary>시간 상한(ms). 느린 기기에서 넘으면 그때까지 읽은 것으로 고른다.</summary>
        public const int TimeLimitMs = 60000;

        public static EngineSettings Settings(int level)
        {
            switch (Math.Max(Min, Math.Min(Max, level)))
            {
                case 1: return Shallow(beam: 2, maxActions: 3, mistake: 0.6, mistakeRange: 6);
                case 2: return Shallow(beam: 2, maxActions: 4, mistake: 0.5, mistakeRange: 6);
                case 3: return Shallow(beam: 2, maxActions: 6, mistake: 0.4, mistakeRange: 5);
                case 4: return Shallow(beam: 4, maxActions: 10, mistake: 0.3, mistakeRange: 4);
                case 5: return Shallow(beam: 4, maxActions: 10, mistake: 0.15, mistakeRange: 3);
                case 6: return Shallow(beam: 4, maxActions: 10, mistake: 0, mistakeRange: 2);
                case 7: return Deep(myPlans: 3, replyPlans: 3, beam: 4);
                case 8: return Deep(myPlans: 4, replyPlans: 4, beam: 4);
                case 9: return Deep(myPlans: 6, replyPlans: 5, beam: 6);
                default: return Deep(myPlans: 8, replyPlans: 6, beam: 8);
            }
        }

        private static EngineSettings Shallow(int beam, int maxActions, double mistake, int mistakeRange)
        {
            return new EngineSettings
            {
                Depth = 1,
                BeamWidth = beam,
                MaxActions = maxActions,
                MistakeChance = mistake,
                MistakeRange = mistakeRange,
                TimeLimitMs = TimeLimitMs,
            };
        }

        private static EngineSettings Deep(int myPlans, int replyPlans, int beam)
        {
            return new EngineSettings
            {
                Depth = 3,
                MyPlans = myPlans,
                ReplyPlans = replyPlans,
                BeamWidth = beam,
                TimeLimitMs = TimeLimitMs,
            };
        }
    }
}
