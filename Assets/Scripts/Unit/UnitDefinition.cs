namespace MiniChess
{
    public enum UnitKind
    {
        Test, Warrior, Guardian, Ranger, Controller,
        Assassin, Pyromancer, Warper, Breaker, Trapper
    }

    public enum UnitSkill
    {
        None,
        StrongStrike, Shield, Snipe, Knockback,
        ShadowLeap,  // Teleport to an empty tile, ignoring everything in between.
        FireZone,    // Lasting damage field on a plus-shaped set of tiles.
        Swap,        // Exchange two allies' positions.
        Charge,      // Straight-line dash that stops at the first blocker.
        SetTrap      // Arm a tile against the next enemy that steps on it.
    }

    // What a skill needs pointed at it, which also decides how the HUD collects clicks.
    public enum SkillTargeting { None, EnemyUnit, AllyUnit, EmptyTile, AnyTile, TwoAllies }

    // Immutable definitions are shared by scene components and pure-data simulations.
    public sealed class UnitDefinition
    {
        public UnitKind Kind { get; }
        public string Name { get; }
        public UnitRole Role { get; }
        public UnitPosition Position { get; }
        public UnitPassive Passive { get; }
        public int MaxHP { get; }
        public int AttackDamage { get; }
        public int AttackRange { get; }
        public UnitSkill Skill { get; }
        public string SkillName { get; }
        public int SkillRange { get; }
        public int SkillPower { get; }
        public int? SkillCostOverride { get; }
        public int? SkillAPCostOverride { get; }
        public int AimRangeBonus { get; }
        public string Description { get; }

        internal UnitDefinition(UnitKind kind, string name, UnitRole role, int hp,
            int attackDamage, int attackRange, UnitSkill skill, string skillName,
            int skillRange = 1, int skillPower = 1, int? skillCost = null,
            int aimRangeBonus = 0, string description = "", int? skillAPCost = null,
            UnitPosition position = UnitPosition.Melee, UnitPassive passive = UnitPassive.None)
        {
            if (hp < 1 || attackDamage < 0 || attackRange < 1 || skillRange < 1
                || skillPower < 0 || skillCost < 0 || skillAPCost < 1 || aimRangeBonus < 0)
                throw new System.ArgumentOutOfRangeException(nameof(hp), "Unit values are outside their valid ranges.");
            Kind = kind; Name = name; Role = role; MaxHP = hp;
            AttackDamage = attackDamage; AttackRange = attackRange;
            Skill = skill; SkillName = skillName;
            SkillRange = skillRange; SkillPower = skillPower; SkillCostOverride = skillCost;
            AimRangeBonus = aimRangeBonus; Description = description;
            SkillAPCostOverride = skillAPCost;
            Position = position; Passive = passive;
        }

        public static SkillTargeting GetTargeting(UnitSkill skill)
        {
            switch (skill)
            {
                case UnitSkill.StrongStrike:
                case UnitSkill.Snipe:
                case UnitSkill.Knockback: return SkillTargeting.EnemyUnit;
                case UnitSkill.Shield: return SkillTargeting.AllyUnit;
                case UnitSkill.ShadowLeap:
                case UnitSkill.Charge:
                case UnitSkill.SetTrap: return SkillTargeting.EmptyTile;
                case UnitSkill.FireZone: return SkillTargeting.AnyTile;
                case UnitSkill.Swap: return SkillTargeting.TwoAllies;
                default: return SkillTargeting.None;
            }
        }

        public SkillTargeting Targeting => GetTargeting(Skill);
        // A movement skill leaves the combat action unspent, so an attack may follow.
        public bool IsMovementSkill => Skill == UnitSkill.ShadowLeap || Skill == UnitSkill.Charge;

        public int GetSkillAPCost(BattleRules rules) => SkillAPCostOverride ?? rules.AP.DefaultSkillAP;
        public int GetSkillResourceCost(BattleRules rules) => SkillCostOverride ?? rules.SkillCost;
        public int GetSkillPower(BattleRules rules) =>
            Skill == UnitSkill.Shield && SkillPower == 0 ? rules.ShieldAmount : SkillPower;

        // Cost line shown next to the skill button, before the skill is committed.
        public string GetSkillCostText(BattleRules rules)
        {
            if (Skill == UnitSkill.None) return "";
            return $"{GetSkillAPCost(rules)} AP"
                + (rules.AP.UseSkillResource ? $" + {GetSkillResourceCost(rules)} SP" : "");
        }

        public string GetPassiveText()
        {
            switch (Passive)
            {
                case UnitPassive.IsolationHunt:
                    return "고립 사냥: 공격 대상 주변 8칸에 그 편의 다른 유닛이 없으면 피해 +1.";
                case UnitPassive.Ember:
                    return "잔불: 자신이 만든 화염 장판 위의 적을 기본 공격하면 피해 +1.";
                case UnitPassive.SpatialEcho:
                    return "공간 잔향: 자신의 스킬로 위치가 바뀐 아군은 턴 종료까지 처음 받는 피해를 1 줄입니다.";
                case UnitPassive.Impact:
                    return "충격: 한 번에 2칸 이상 이동한 턴의 기본 공격은 피해 +1.";
                case UnitPassive.MarkedPrey:
                    return "사냥감 포착: 자신의 덫에 걸린 적을 다음 자기 턴이 끝나기 전까지 공격하면 피해 +1.";
                default: return "";
            }
        }

        public string GetSkillEffectText(BattleRules rules)
        {
            int power = GetSkillPower(rules);
            switch (Skill)
            {
                case UnitSkill.StrongStrike: return $"적 하나에게 {power} 피해를 줍니다.";
                case UnitSkill.Snipe: return $"적 하나에게 {power} 피해를 줍니다. 중간에 낀 유닛을 무시합니다.";
                case UnitSkill.Shield: return $"아군 하나에게 피해를 {power}만큼 막아 주는 보호막을 겁니다. "
                    + "그 아군의 다음 턴이 끝날 때까지 유지됩니다.";
                case UnitSkill.Knockback: return $"적 하나를 {power}칸 밀어냅니다. 도착 칸이 비어 있어야 합니다.";
                case UnitSkill.ShadowLeap: return "비어 있는 칸으로 즉시 순간이동합니다. "
                    + "경로와 길막을 무시하며, 이동 후에도 기본 공격이 가능합니다.";
                case UnitSkill.FireZone: return $"지정한 칸과 상하좌우 1칸에 화염 장판을 깝니다. "
                    + $"적이 들어오거나 그 위에서 턴을 시작하면 {power} 피해. 2번의 자기 턴 동안 유지됩니다.";
                case UnitSkill.Swap: return "사거리 안의 아군 둘을 골라 위치를 맞바꿉니다. 경로는 무시합니다.";
                case UnitSkill.Charge: return "직선(상하좌우 또는 대각선)으로 돌진합니다. "
                    + "경로가 모두 비어 있어야 하며, 돌진 후에도 기본 공격이 가능합니다.";
                case UnitSkill.SetTrap: return $"빈 칸에 덫을 놓습니다. 적이 밟으면 {power} 피해를 주고 "
                    + "그 이동을 즉시 끝냅니다. 덫은 최대 2개까지 유지됩니다.";
                default: return "스킬이 없습니다.";
            }
        }

        public string GetSkillTargetText()
        {
            switch (Targeting)
            {
                case SkillTargeting.EnemyUnit: return "적 유닛 1";
                case SkillTargeting.AllyUnit: return "아군 유닛 1";
                case SkillTargeting.EmptyTile: return "빈 칸 1";
                case SkillTargeting.AnyTile: return "칸 1";
                case SkillTargeting.TwoAllies: return "아군 유닛 2";
                default: return "";
            }
        }

        // One line for the HUD so the player can read the skill before using it.
        public string GetSkillSummary(BattleRules rules)
        {
            if (Skill == UnitSkill.None) return "스킬 없음";
            return $"{SkillName} ({GetSkillCostText(rules)}, 사거리 {SkillRange}, 대상 {GetSkillTargetText()})"
                + $" : {GetSkillEffectText(rules)}";
        }

        public string GetDescription(BattleRules rules)
        {
            string text = $"{Name} [{GetPositionName(Position)}] | 체력 {MaxHP} | 공격력 {AttackDamage}"
                + $" | 사거리 {AttackRange}";
            text += $"\n이동: {rules.AP.GetMovePatternName()} 1~{rules.AP.MaxMoveDistance}칸,"
                + $" 1칸당 {rules.AP.MoveAPPerTile} AP. 기본 공격: {rules.AP.AttackAP} AP.";
            if (rules.AP.MaxMoveTilesPerTurn > 0)
                text += $" 한 턴에 최대 {rules.AP.MaxMoveTilesPerTurn}칸까지 이동합니다.";
            if (!string.IsNullOrWhiteSpace(Description)) text += "\n" + Description;
            if (AimRangeBonus > 0)
                text += $"\n조준: 이 유닛이 아직 이동하지 않았다면 기본 사거리 +{AimRangeBonus}.";
            if (Passive != UnitPassive.None) text += "\n" + GetPassiveText();
            if (Skill == UnitSkill.None) return text + "\n스킬 없음.";
            text += "\n" + GetSkillSummary(rules);
            return text + "\n유닛마다 한 턴에 전투는 1회이며, 전투한 유닛은 그 턴에 더 이동할 수 없습니다."
                + " 쓰지 않은 AP는 다음 턴으로 이월됩니다.";
        }

        public static string GetPositionName(UnitPosition position)
        {
            switch (position)
            {
                case UnitPosition.Melee: return "근접";
                case UnitPosition.Guard: return "수호";
                case UnitPosition.Ranged: return "원거리";
                default: return "제어";
            }
        }
    }

    public static class UnitDefinitions
    {
        public static readonly UnitDefinition Test = new UnitDefinition(
            UnitKind.Test, "테스트", UnitRole.Combat, 3, 1, 1, UnitSkill.None, "없음");
        public static readonly UnitDefinition Warrior = new UnitDefinition(
            UnitKind.Warrior, "전사", UnitRole.Combat, 3, 1, 1, UnitSkill.StrongStrike, "강타",
            skillPower: 2, position: UnitPosition.Melee);
        public static readonly UnitDefinition Guardian = new UnitDefinition(
            UnitKind.Guardian, "수호자", UnitRole.Guardian, 4, 1, 1, UnitSkill.Shield, "보호막",
            skillPower: 0, position: UnitPosition.Guard);
        public static readonly UnitDefinition Ranger = new UnitDefinition(
            UnitKind.Ranger, "궁수", UnitRole.Combat, 2, 1, 3, UnitSkill.Snipe, "저격", 4, 2, 2, 1,
            position: UnitPosition.Ranged);
        public static readonly UnitDefinition Controller = new UnitDefinition(
            UnitKind.Controller, "제어자", UnitRole.Controller, 2, 1, 1, UnitSkill.Knockback, "넉백",
            position: UnitPosition.Control);

        // v0.4 additions. Numbers are the document's prototype values.
        public static readonly UnitDefinition Assassin = new UnitDefinition(
            UnitKind.Assassin, "암살자", UnitRole.Combat, 5, 2, 1, UnitSkill.ShadowLeap, "그림자 도약",
            skillRange: 3, skillPower: 1, skillAPCost: 3, position: UnitPosition.Melee,
            passive: UnitPassive.IsolationHunt,
            description: "후열로 파고들어 고립된 적을 끊어내는 기동형 공격 유닛.");
        public static readonly UnitDefinition Pyromancer = new UnitDefinition(
            UnitKind.Pyromancer, "화염술사", UnitRole.Controller, 5, 2, 2, UnitSkill.FireZone, "화염 지대",
            skillRange: 3, skillPower: 1, skillAPCost: 3, position: UnitPosition.Ranged,
            passive: UnitPassive.Ember,
            description: "지속 장판으로 특정 칸을 위험 지역으로 만들어 진형을 흔드는 공간 통제 유닛.");
        public static readonly UnitDefinition Warper = new UnitDefinition(
            UnitKind.Warper, "워프술사", UnitRole.Controller, 5, 2, 2, UnitSkill.Swap, "위치 교환",
            skillRange: 3, skillPower: 1, skillAPCost: 3, position: UnitPosition.Control,
            passive: UnitPassive.SpatialEcho,
            description: "아군 위치를 즉시 재편해 위기 유닛을 구하거나 공격각을 만드는 유닛.");
        public static readonly UnitDefinition Breaker = new UnitDefinition(
            UnitKind.Breaker, "돌진전사", UnitRole.Combat, 7, 2, 1, UnitSkill.Charge, "돌진",
            skillRange: 3, skillPower: 1, skillAPCost: 3, position: UnitPosition.Melee,
            passive: UnitPassive.Impact,
            description: "직선 돌진으로 거리를 빠르게 좁히는 전면 진입형 근접 유닛.");
        public static readonly UnitDefinition Trapper = new UnitDefinition(
            UnitKind.Trapper, "트래퍼", UnitRole.Controller, 5, 2, 2, UnitSkill.SetTrap, "덫 설치",
            skillRange: 2, skillPower: 1, skillAPCost: 2, position: UnitPosition.Control,
            passive: UnitPassive.MarkedPrey,
            description: "미리 칸을 봉쇄해 상대 이동 경로를 제한하는 예측형 컨트롤러.");

        // Draft pool. Guard currently has a single candidate, so that slot is fixed
        // until another guard unit exists.
        public static readonly UnitDefinition[] Pool =
        {
            Warrior, Guardian, Ranger, Controller,
            Assassin, Pyromancer, Warper, Breaker, Trapper
        };
    }
}
