using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace MiniChess.Bot.Engine
{
    /// <summary>탐색 비용을 나눠 재는 단계.</summary>
    internal enum ProfileStep
    {
        /// <summary>판 복제(GameController.Fork).</summary>
        Fork,
        /// <summary>합법 행동 생성.</summary>
        Generate,
        /// <summary>행동 실행.</summary>
        Execute,
        /// <summary>턴 종료(다음 턴 시작 효과 포함).</summary>
        EndTurn,
        /// <summary>판 평가(BoardAnalysis + 평가 항목).</summary>
        Evaluate,
        /// <summary>판 해시용 스냅샷 생성(Core GetSnapshot).</summary>
        Snapshot,
        /// <summary>스냅샷을 훑어 해시 계산(StateHasher).</summary>
        Hash,
    }

    /// <summary>
    /// 탐색 비용 분해(벤치마크용). Enabled 일 때만 단계별 누적 시간과 횟수를 모은다(꺼져 있으면 비용이 거의 없다).
    /// 단계는 서로 겹치지 않게 잰다. 나머지(빔 정렬, 리스트 할당 등)는 전체 시간에서 뺀 값으로 본다.
    /// GC 시간은 따로 잡지 않고, GC 가 일어난 단계의 시간에 섞인다.
    /// </summary>
    internal static class SearchProfile
    {
        public static bool Enabled;

        private static readonly int StepCount = Enum.GetValues(typeof(ProfileStep)).Length;
        private static readonly long[] Ticks = new long[StepCount];
        private static readonly long[] Calls = new long[StepCount];

        /// <summary>재기 시작한 시각(꺼져 있으면 0).</summary>
        public static long Begin() => Enabled ? Stopwatch.GetTimestamp() : 0;

        public static void End(ProfileStep step, long started)
        {
            if (started == 0)
                return;
            Interlocked.Add(ref Ticks[(int)step], Stopwatch.GetTimestamp() - started);
            Interlocked.Increment(ref Calls[(int)step]);
        }

        public static void Reset()
        {
            Array.Clear(Ticks, 0, StepCount);
            Array.Clear(Calls, 0, StepCount);
        }

        /// <summary>단계별 누적 (단계, 초, 횟수).</summary>
        public static List<(ProfileStep Step, double Seconds, long Calls)> Read()
        {
            var result = new List<(ProfileStep, double, long)>(StepCount);
            for (int i = 0; i < StepCount; i++)
                result.Add(((ProfileStep)i, (double)Interlocked.Read(ref Ticks[i]) / Stopwatch.Frequency, Interlocked.Read(ref Calls[i])));
            return result;
        }
    }
}
