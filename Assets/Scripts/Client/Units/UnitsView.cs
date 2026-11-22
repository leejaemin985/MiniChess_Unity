using System.Collections.Generic;
using MiniChess.Client.Common;
using MiniChess.Client.Visuals;
using MiniChess.Core.Common;
using MiniChess.Core.State;
using UnityEngine;

namespace MiniChess.Client.Units
{
    /// <summary>유닛 이동 연출 방식.</summary>
    public enum UnitMoveStyle
    {
        /// <summary>일반 이동: 칸마다 일정한 속도.</summary>
        Walk,

        /// <summary>돌진: 짧게 감속하며 미끄러짐.</summary>
        Dash,

        /// <summary>밀려남/당겨짐: 타격 시점까지 기다렸다가 감속하며 미끄러짐.</summary>
        Knockback,
    }

    /// <summary>
    /// 연속 베기 순간이동 연출 타이밍(초). 출발(MoveStart)에 노이즈로 사라지고, 대상마다 그 대상 위로 순간이동해 잠깐 보였다가 다시 사라지고,
    /// 마지막에 착지 칸으로 순간이동해 착지(Land)에 맞춰 나타난다. 대상 위 등장은 사라진 뒤 ~ 착지 등장 전 구간에 고르게 나눈다.
    /// </summary>
    [System.Serializable]
    public class ChainBlinkTiming
    {
        [Tooltip("끄면 노이즈 없이 보인 채로 순간이동만 한다.")]
        public bool enabled = true;

        [Tooltip("출발(MoveStart)에서 사라지기 시작하는 시점 차이. 음수면 출발 전부터.")]
        public float vanishOffset = 0f;
        [Tooltip("출발할 때 사라지는 데 걸리는 시간.")]
        [Min(0.01f)] public float vanishTime = 0.05f;

        [Tooltip("대상 위에서 보이는 시간(나타나기 시작 ~ 다시 사라지기 시작). 대상 간격보다 길면 겹치지 않게 줄인다.")]
        [Min(0.01f)] public float flashVisibleTime = 0.15f;
        [Tooltip("대상 위에서 나타나는 데 걸리는 시간.")]
        [Min(0.01f)] public float flashInTime = 0.03f;
        [Tooltip("대상 위에서 다시 사라지는 데 걸리는 시간.")]
        [Min(0.01f)] public float flashOutTime = 0.03f;
        [Tooltip("대상 위로 나타날 때 대상 칸 윗면에서 띄우는 높이.")]
        public float targetHeight = 0f;

        [Tooltip("착지할 때 나타나는 데 걸리는 시간(착지 시점에 다 나타난다).")]
        [Min(0.01f)] public float landInTime = 0.06f;
    }

    /// <summary>
    /// 경기의 모든 유닛 뷰를 Unit.Id 기준으로 관리한다.
    /// 경기 중에 생기는 유닛(분신 등 소환물)은 Sync 때 뷰를 새로 만든다.
    /// </summary>
    public class UnitsView : MonoBehaviour
    {
        [SerializeField] private Color player1Color = new Color(0.25f, 0.45f, 0.95f);
        [SerializeField] private Color player2Color = new Color(0.9f, 0.3f, 0.25f);
        [SerializeField, Range(0f, 1f)] private float actedDarken = 0.55f;
        [SerializeField, Range(0f, 1f)] private float summonLighten = 0.45f;

        private readonly Dictionary<int, UnitView> _views = new Dictionary<int, UnitView>();
        private BoardCoordinates _coordinates;
        private GameState _state;
        private CharacterVisualCatalog _visuals;
        /// <summary>팀마다 몸체가 바라볼 방향(도). 비어 있으면 0(+z).</summary>
        private System.Func<Team, float> _facingYaw;

        [Tooltip("상태 이름/색/아이콘(유닛 머리 위 상태 아이콘). 비우면 Resources/StatusVisualCatalog 를 불러온다.")]
        [SerializeField] private StatusVisualCatalog statusVisuals;

        [Tooltip("유닛 머리 위 상태 배지 모양. 플레이 중에 바꾸면 바로 반영된다.")]
        [SerializeField] private StatusBadgeStyle statusBadge = new StatusBadgeStyle();

        [Tooltip("일반 이동에서 칸 하나를 지나는 시간(초).")]
        [SerializeField, Min(0f)] private float moveCellDuration = 0.12f;
        [Tooltip("돌진 전체에 걸리는 시간(초, 칸 수와 무관). 돌진 경로 이펙트(FX_Dash)의 travelTime 과 맞춘다.")]
        [SerializeField, Min(0f)] private float dashDuration = 0.25f;
        [Tooltip("밀려남/당겨짐에서 칸 하나를 지나는 시간(초). 감속하며 미끄러진다.")]
        [SerializeField, Min(0f)] private float knockbackCellDuration = 0.1f;
        [Tooltip("밀려남/당겨짐이 출발하기 전 기다리는 시간(초). 스킬 타격 연출(심판 둔기 등)이 닿는 시점과 맞춘다.")]
        [SerializeField, Min(0f)] private float knockbackDelay = 0.15f;
        [Tooltip("연속 베기 순간이동 연출 타이밍.")]
        [SerializeField] private ChainBlinkTiming chainBlink = new ChainBlinkTiming();
        [Tooltip("동작 동안 무기 궤적을 끌 때 클립 길이에 더하는 시간(초). 동작으로 들어가는 전환(AC_Unit 0.1)과 끝나고 대기로 돌아가는 전환(0.18) 동안의 휘두름까지 끈다.")]
        [SerializeField, Min(0f)] private float actionTrailMargin = 0.35f;

        /// <summary>돌진 전체에 걸리는 시간(초).</summary>
        public float DashDuration => dashDuration;

        /// <summary>순간이동 경로를 찍는 시간 간격(초). 자리는 이 간격 안에서 넘어간다(그동안은 숨어 있다).</summary>
        private const float BlinkPathStep = 0.01f;

        private readonly List<Vector3> _movePoints = new List<Vector3>();
        /// <summary>방금 소환되어 뷰를 만들 때 솟아오르는 연출을 할 소환물 Id(ExpectSummon).</summary>
        private readonly HashSet<int> _appearing = new HashSet<int>();

        /// <summary>
        /// 유닛을 지금 자리에서 cells(거치는 칸, 마지막이 도착 칸)를 차례로 지나 옮긴다. Sync 전에 호출한다.
        /// 돌진은 칸 수와 상관없이 dashDuration 동안 감속하며 그동안 무기 궤적을 끄고, 밀려남/당겨짐은 knockbackDelay 뒤 칸마다 knockbackCellDuration 으로 감속하며 미끄러진다.
        /// 그 밖(일반 이동)은 칸마다 moveCellDuration 의 일정한 속도로 지난다.
        /// </summary>
        public void PlayMove(int unitId, IReadOnlyList<Position> cells, UnitMoveStyle style)
        {
            if (_coordinates == null || cells == null || cells.Count == 0 || !TryGetView(unitId, out UnitView view))
                return;

            _movePoints.Clear();
            foreach (Position cell in cells)
                _movePoints.Add(_coordinates.ToWorld(cell));

            switch (style)
            {
                case UnitMoveStyle.Dash:
                    view.BeginMove(_movePoints, dashDuration, easeOut: true);
                    // 몸 전체가 빠르게 미끄러지면 무기 끝도 빨라져 궤적이 생기므로, 돌진하는 동안은 무기 궤적을 끈다.
                    view.SuppressWeaponTrails(dashDuration);
                    break;
                case UnitMoveStyle.Knockback:
                    view.BeginMove(_movePoints, knockbackCellDuration * cells.Count, easeOut: true, knockbackDelay);
                    break;
                default:
                    view.BeginMove(_movePoints, moveCellDuration * cells.Count, easeOut: false);
                    break;
            }
        }

        /// <summary>
        /// 연속 베기 순간이동 연출(chainBlink 타이밍). Sync 전에 호출한다. 시간은 모두 지금부터(초).
        /// start(MoveStart)에 노이즈로 사라지고, targets 마다 그 칸 위로 순간이동해 잠깐 보였다가 다시 사라지고,
        /// landing 칸으로 순간이동해 land(Land)에 다 나타난다. 몸 방향과 모션은 그대로다.
        /// 자리는 숨어 있는 동안에만 바꾼다(BeginMove 경로를 일정한 간격으로 찍고, 바뀌는 곳은 한 간격 안에서 넘어간다).
        /// 대상마다 보이는 구간의 가운데 시간(피해 숫자 시점)을 돌려준다. 뷰가 없으면 빈 목록.
        /// </summary>
        public List<float> PlayChainBlinkStrike(int unitId, IReadOnlyList<Position> targets, Position landing, float start, float land)
        {
            var flashTimes = new List<float>();
            if (_coordinates == null || !TryGetView(unitId, out UnitView view))
                return flashTimes;

            // 다 사라진 뒤 ~ 착지 등장 전 구간을 대상 수 + 1 로 나눠 대상 위 등장을 고르게 놓는다.
            // 보이는 시간은 다음 등장(또는 착지 등장) 전에 다시 다 사라질 수 있게 대상 간격에 맞춰 줄인다.
            float vanishStart = start + chainBlink.vanishOffset;
            float hidden = vanishStart + chainBlink.vanishTime;
            float landAppear = Mathf.Max(hidden, land - chainBlink.landInTime);
            float spacing = (landAppear - hidden) / (targets.Count + 1);
            float visible = Mathf.Max(0.01f, Mathf.Min(chainBlink.flashVisibleTime, spacing - chainBlink.flashOutTime));

            var steps = new List<(float Time, bool Vanish, float Duration)> { (vanishStart, true, chainBlink.vanishTime) };
            var spots = new List<(float Time, Vector3 Position)>();
            Vector3 lift = Vector3.up * chainBlink.targetHeight;
            for (int i = 0; i < targets.Count; i++)
            {
                float center = hidden + spacing * (i + 1);
                float appear = center - visible * 0.5f;
                flashTimes.Add(center);
                spots.Add((appear, _coordinates.ToWorld(targets[i]) + lift));
                steps.Add((appear, false, chainBlink.flashInTime));
                steps.Add((appear + visible, true, chainBlink.flashOutTime));
            }
            spots.Add((landAppear, _coordinates.ToWorld(landing)));
            steps.Add((landAppear, false, chainBlink.landInTime));

            float duration = Mathf.Max(land, BlinkPathStep);
            int count = Mathf.CeilToInt(duration / BlinkPathStep);
            Vector3 current = view.transform.localPosition;
            int next = 0;
            _movePoints.Clear();
            for (int k = 1; k <= count; k++)
            {
                float time = duration * k / count;
                while (next < spots.Count && spots[next].Time <= time)
                    current = spots[next++].Position;
                _movePoints.Add(current);
            }

            view.BeginMove(_movePoints, duration, easeOut: false);
            if (chainBlink.enabled)
                StartCoroutine(RunBlinkSteps(view, steps));
            return flashTimes;
        }

        private System.Collections.IEnumerator RunBlinkSteps(UnitView view, List<(float Time, bool Vanish, float Duration)> steps)
        {
            float elapsed = 0f;
            foreach ((float time, bool vanish, float duration) in steps)
            {
                while (elapsed < time)
                {
                    yield return null;
                    elapsed += Time.deltaTime;
                }

                if (view == null)
                    yield break;
                if (vanish)
                    view.BlinkOut(duration);
                else
                    view.BlinkIn(duration);
            }
        }

        /// <summary>유닛 동작 클립에서 eventName 이벤트의 시간(초). 뷰, 애니메이션, 이벤트 중 하나라도 없으면 null.</summary>
        public float? GetEventTime(int unitId, UnitAction action, string eventName)
        {
            return TryGetView(unitId, out UnitView view) ? view.GetEventTime(action, eventName) : null;
        }

        /// <summary>유닛 동작 클립 길이(초). 뷰나 애니메이션이 없으면 0.</summary>
        public float GetActionLength(int unitId, UnitAction action)
        {
            return TryGetView(unitId, out UnitView view) ? view.GetActionLength(action) : 0f;
        }

        /// <summary>유닛을 from 칸 관문으로 가라앉혔다가 to 칸 관문에서 솟아오르게 한다(실 관문 등). Sync 전에 호출한다.</summary>
        public void PlayPortal(int unitId, Position from, Position to, float delay, float sinkTime, float riseTime)
        {
            if (_coordinates == null || !TryGetView(unitId, out UnitView view))
                return;

            view.BeginPortal(_coordinates.ToWorld(from), _coordinates.ToWorld(to), delay, sinkTime, riseTime);
        }

        /// <summary>곧 Sync 에서 뷰가 만들어질 소환물. 그 뷰는 소환자 캐릭터의 등장 시간대로 바닥에서 솟아오른다. Sync 전에 호출한다.</summary>
        public void ExpectSummon(int summonId)
        {
            _appearing.Add(summonId);
        }

        /// <summary>소환물의 소환물 데이터(자기 캐릭터 데이터에 없으면 소환자 캐릭터 것). 소환물이 아니거나 없으면 null.</summary>
        public CharacterVisual FindSummonVisual(Unit unit)
        {
            if (_visuals == null || unit == null || !unit.IsSummon)
                return null;

            CharacterVisual own = _visuals.Find(unit.Stats.Base.Id);
            if (own != null && own.SummonBodyMaterial != null)
                return own;

            return unit.SummonOwner != null ? _visuals.Find(unit.SummonOwner.Stats.Base.Id) : null;
        }

        /// <summary>소환자(ownerId)의 캐릭터 데이터 중 소환물 데이터가 있는 것. 없으면 null. 소환물 뷰가 아직 없을 때(소환 순간) 쓴다.</summary>
        public CharacterVisual FindSummonVisualByOwner(int ownerId)
        {
            if (_visuals == null || !TryGetView(ownerId, out UnitView owner))
                return null;

            CharacterVisual visual = _visuals.Find(owner.Unit.Stats.Base.Id);
            return visual != null && visual.SummonBodyMaterial != null ? visual : null;
        }

        /// <param name="visuals">캐릭터 얼굴 데이터. null 이면 얼굴 없이 그린다.</param>
        /// <param name="facingYaw">팀마다 몸체가 바라볼 방향(도, 상대 진영 쪽). null 이면 모두 +z.</param>
        public void Build(GameState state, BoardCoordinates coordinates, CharacterVisualCatalog visuals, System.Func<Team, float> facingYaw = null)
        {
            Clear();
            _coordinates = coordinates;
            _state = state;
            _visuals = visuals;
            _facingYaw = facingYaw;

            Sync();
        }

        /// <summary>모든 유닛 뷰를 현재 코어 상태로 맞춘다. 뷰가 없는 유닛(새 소환물)은 뷰를 만든다.</summary>
        public void Sync()
        {
            if (_state == null)
                return;

            foreach (Unit unit in _state.AllUnits())
            {
                if (!_views.ContainsKey(unit.Id))
                    _views[unit.Id] = CreateView(unit);
            }

            foreach (UnitView view in _views.Values)
                view.Sync(_coordinates);
        }

        /// <summary>
        /// 같은 경기를 새로 만든 상태(온라인: 서버 스냅샷으로 복원)로 바꾼다. 같은 Id 의 뷰는 그대로 두고 새 유닛을 가리키게 하며,
        /// 새 상태에 없는 유닛(사망해 스냅샷에서 빠짐)의 뷰는 지운다.
        /// </summary>
        public void Rebind(GameState state)
        {
            _state = state;
            var units = new Dictionary<int, Unit>();
            foreach (Unit unit in state.AllUnits())
                units[unit.Id] = unit;

            foreach (int id in new List<int>(_views.Keys))
            {
                if (units.TryGetValue(id, out Unit unit))
                {
                    _views[id].Rebind(unit);
                    continue;
                }

                if (_views[id] != null)
                    Destroy(_views[id].gameObject);
                _views.Remove(id);
            }

            Sync();
        }

#if UNITY_EDITOR
        /// <summary>플레이 중 인스펙터에서 상태 배지 모양을 바꾸면 바로 다시 그린다.</summary>
        private void OnValidate()
        {
            if (Application.isPlaying)
                Sync();
        }
#endif

        public Color GetTeamColor(Team team)
        {
            return team == Team.Player1 ? player1Color : player2Color;
        }

        /// <summary>유닛 위에 행동 아이콘을 잠깐 띄운다. 뷰가 없거나 아이콘이 없으면 무시.</summary>
        public void ShowActionIcon(Unit unit, Sprite icon, float duration)
        {
            if (unit != null && icon != null && _views.TryGetValue(unit.Id, out UnitView view))
                view.ShowActionIcon(icon, duration);
        }

        /// <summary>유닛 몸체의 동작 애니메이션(공격, 스킬)을 재생한다. 뷰가 없으면 무시.</summary>
        public void PlayAction(int unitId, UnitAction action)
        {
            if (TryGetView(unitId, out UnitView view))
                view.PlayAction(action);
        }

        /// <summary>유닛 동작 클립의 타격 시점(초, Hit 이벤트). 뷰, 애니메이션, Hit 이벤트 중 하나라도 없으면 null.</summary>
        public float? GetHitTime(int unitId, UnitAction action)
        {
            return TryGetView(unitId, out UnitView view) ? view.GetHitTime(action) : null;
        }

        /// <summary>유닛의 무기 궤적을 동작(action)이 대기로 돌아갈 때까지(클립 길이 + 전환 여유, 최소 돌진 시간) 끈다. 돌진 스킬처럼 이동과 모션이 함께일 때 쓴다.</summary>
        public void SuppressWeaponTrails(int unitId, UnitAction action)
        {
            if (TryGetView(unitId, out UnitView view))
                view.SuppressWeaponTrails(Mathf.Max(dashDuration, view.GetActionLength(action) + actionTrailMargin));
        }

        public bool TryGetView(Unit unit, out UnitView view)
        {
            return _views.TryGetValue(unit.Id, out view);
        }

        /// <summary>
        /// 화면 탭의 레이가 맞는 유닛 중 카메라에 가장 가까운 유닛의 칸(보드 위 유닛만). 맞는 유닛이 없으면 false.
        /// 칸 판정(판 평면)보다 먼저 써서, 비스듬히 볼 때 캐릭터 상반신을 눌러도 그 뒤 칸이 아니라 그 유닛이 눌리게 한다.
        /// </summary>
        public bool TryPickUnit(Ray ray, out Position cell)
        {
            cell = default;
            float nearest = float.PositiveInfinity;
            foreach (UnitView view in _views.Values)
            {
                if (view == null || !view.Unit.IsPlaced || !view.TryPick(ray, out float distance) || distance >= nearest)
                    continue;

                nearest = distance;
                cell = view.Unit.Position.Value;
            }

            return !float.IsPositiveInfinity(nearest);
        }

        /// <summary>유닛 Id 로 뷰를 찾는다. 코어에서 이미 빠진(사망한) 유닛도 뷰가 남아 있으면 찾는다.</summary>
        public bool TryGetView(int unitId, out UnitView view)
        {
            return _views.TryGetValue(unitId, out view) && view != null;
        }

        public void Clear()
        {
            StopAllCoroutines();
            foreach (UnitView view in _views.Values)
            {
                if (view != null)
                    Destroy(view.gameObject);
            }

            _views.Clear();
            _appearing.Clear();
            _state = null;
        }

        /// <summary>소환물은 팀 색을 옅게 해 본체와 구분한다. 발밑 고리는 본래 팀 색이고, 행동을 마치면 어두워진다.</summary>
        private UnitView CreateView(Unit unit)
        {
            Color ringColor = GetTeamColor(unit.Team);
            Color color = ringColor;
            if (unit.IsSummon)
                color = Color.Lerp(color, Color.white, summonLighten);

            Color actedColor = Color.Lerp(color, Color.gray, actedDarken);
            Color ringActedColor = Color.Lerp(ringColor, Color.gray, actedDarken);
            if (statusVisuals == null)
                statusVisuals = Resources.Load<StatusVisualCatalog>(StatusVisualCatalog.ResourcePath);

            float facing = _facingYaw?.Invoke(unit.Team) ?? 0f;
            UnitView view = UnitView.Create(
                transform, unit, color, actedColor, ringColor, ringActedColor, facing,
                FindFace(unit), _visuals?.FaceMask, _visuals?.FaceFrame, statusVisuals, statusBadge, FindBodyPrefab(unit));

            // 소환물: 소환자 캐릭터의 소환물 모습(분신 → 그림자). 방금 소환된 것이면 바닥에서 솟아오른다.
            CharacterVisual summonVisual = FindSummonVisual(unit);
            view.ApplySummonLook(summonVisual);
            if (_appearing.Remove(unit.Id) && summonVisual != null)
                view.PlayAppear(summonVisual.SummonAppearDelay, summonVisual.SummonAppearTime);

            return view;
        }

        /// <summary>유닛의 몸체 모델 프리팹. 소환물은 자기 데이터가 없으면 소환자의 것을 쓴다. 없으면 null(캡슐).</summary>
        private GameObject FindBodyPrefab(Unit unit)
        {
            if (_visuals == null)
                return null;

            GameObject prefab = _visuals.Find(unit.Stats.Base.Id)?.BodyPrefab;
            if (prefab == null && unit.IsSummon && unit.SummonOwner != null)
                prefab = _visuals.Find(unit.SummonOwner.Stats.Base.Id)?.BodyPrefab;

            return prefab;
        }

        /// <summary>유닛의 얼굴. 소환물은 자기 데이터가 없으면 소환자의 얼굴을 쓴다.</summary>
        private Sprite FindFace(Unit unit)
        {
            if (_visuals == null)
                return null;

            Sprite face = _visuals.FindFace(unit.Stats.Base.Id);
            if (face == null && unit.IsSummon && unit.SummonOwner != null)
                face = _visuals.FindFace(unit.SummonOwner.Stats.Base.Id);

            return face;
        }
    }
}
