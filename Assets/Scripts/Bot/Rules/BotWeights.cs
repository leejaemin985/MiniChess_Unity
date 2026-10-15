namespace MiniChess.Bot.Rules
{
    /// <summary>
    /// 규칙 봇의 판단 가중치. 점수 단위는 대략 "상대에게 준 HP 피해 1" 이다.
    /// 밸런스 값이 아니라 봇 성향 값이며, 봇끼리 대전으로 조정한다.
    /// </summary>
    public class BotWeights
    {
        /// <summary>AP 1 의 기본 가치. 행동 점수에서 비용만큼 뺀다.</summary>
        public double ApCost { get; set; } = 0.5;

        /// <summary>이번 턴에 안 쓰면 최대치에 막혀 버려질 AP 의 가치(거의 공짜).</summary>
        public double WastedApCost { get; set; } = 0.05;

        /// <summary>이 점수 이하의 행동은 하지 않고 턴을 끝낸다.</summary>
        public double MinActionScore { get; set; } = 0.05;

        /// <summary>보호막을 깎는 피해의 가치(HP 피해 대비 비율).</summary>
        public double ShieldDamageRatio { get; set; } = 0.6;

        /// <summary>처치 보너스 기본값. 실제 보너스는 여기에 대상의 위협도를 더한다.</summary>
        public double KillBonus { get; set; } = 6;

        /// <summary>처치 보너스에 더하는 대상 위협도(가장 센 한 방 피해)의 배수.</summary>
        public double KillThreatFactor { get; set; } = 1.5;

        /// <summary>소환물 처치 보너스.</summary>
        public double SummonKillBonus { get; set; } = 1;

        /// <summary>적과의 거리가 선호 거리보다 1칸 멀 때마다 깎는 점수.</summary>
        public double Approach { get; set; } = 1.0;

        /// <summary>이동한 자리에서 이번 턴에 바로 할 수 있는 공격 가치의 반영 비율.</summary>
        public double ImmediateOffense { get; set; } = 0.9;

        /// <summary>다음 상대 턴에 받을 것으로 예상되는 피해 1 당 감점.</summary>
        public double Danger { get; set; } = 0.6;

        /// <summary>형세가 같을 때 위협도 감점에 곱하는 배수(앞서면 1, 같으면 이 값, 뒤지면 CautionBehind).</summary>
        public double CautionEven { get; set; } = 0.5;

        /// <summary>형세가 뒤질 때 위협도 감점에 곱하는 배수. 턴 제한 판정으로 지지 않도록 들어간다.</summary>
        public double CautionBehind { get; set; } = 0.3;

        /// <summary>내 턴이 이 횟수만큼 지나도록 적의 체력(보호막 포함) 합이 줄지 않으면 교착으로 본다.</summary>
        public int StallTurns { get; set; } = 3;

        /// <summary>교착일 때 위협도 감점 배수. 형세와 무관하게 이 값 이하로 낮춰 들어간다.</summary>
        public double CautionStalled { get; set; } = 0.15;

        /// <summary>남은 체력 합 차이가 이 값 미만이면(생존 수가 같을 때) 형세가 같다고 본다.</summary>
        public int EvenHpMargin { get; set; } = 3;

        /// <summary>예상 피해가 남은 체력 이상일 때 유닛 가치에 곱해 추가로 깎는 비율.</summary>
        public double DeathRisk { get; set; } = 0.5;

        /// <summary>적의 이동 가능 칸 수 가정(다음 턴 접근 거리).</summary>
        public int EnemyMobility { get; set; } = 2;

        /// <summary>점령 칸에 서 있는 가치. 이미 진행도가 있으면 진행도만큼 배로 늘어난다.</summary>
        public double Capture { get; set; } = 3;

        /// <summary>점령 진행 중인 적 유닛을 칠 때 더하는 점수.</summary>
        public double CaptureDenial { get; set; } = 3;

        /// <summary>예약 포격이 적에게 맞을 확률 가정(움직일 수 있는 적).</summary>
        public double StrikeHitChance { get; set; } = 0.5;

        /// <summary>움직일 수 없는 적(속박/설치)에게 예약 포격이 맞을 확률 가정.</summary>
        public double StrikeHitChanceImmobile { get; set; } = 0.9;

        /// <summary>속박 부여 가치.</summary>
        public double Root { get; set; } = 2.5;

        /// <summary>이동 거리 제한 부여 가치.</summary>
        public double DistanceLimit { get; set; } = 1.5;

        /// <summary>그 밖의 상태 부여 기본 가치.</summary>
        public double OtherStatus { get; set; } = 0.5;

        /// <summary>지속 피해(화상, 장판) 의 실현 비율.</summary>
        public double DamageOverTime { get; set; } = 0.7;

        /// <summary>당장 위협이 없어도 보호막 1 이 가지는 가치.</summary>
        public double ShieldIdle { get; set; } = 0.25;

        /// <summary>위협받는 아군에게 준 보호막/호위 1 의 가치. 위치 판단과 같게 신중함 배수를 곱한다.</summary>
        public double Protection { get; set; } = 0.8;

        /// <summary>밀어내기 대상 1명당 가치.</summary>
        public double Knockback { get; set; } = 0.5;

        /// <summary>소환 가치.</summary>
        public double Summon { get; set; } = 3;

        /// <summary>적 근처 덫 설치 가치.</summary>
        public double Trap { get; set; } = 1.5;

        /// <summary>장애물 설치/파괴 가치.</summary>
        public double Obstacle { get; set; } = 0.2;

        /// <summary>쓸 곳이 없는 설치형 유닛이 해체할 때의 가치.</summary>
        public double Uninstall { get; set; } = 3;
    }
}
