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

        [Tooltip("상태 이름/색/아이콘(유닛 머리 위 상태 아이콘). 비우면 Resources/StatusVisualCatalog 를 불러온다.")]
        [SerializeField] private StatusVisualCatalog statusVisuals;

        [Tooltip("유닛 머리 위 상태 배지 모양. 플레이 중에 바꾸면 바로 반영된다.")]
        [SerializeField] private StatusBadgeStyle statusBadge = new StatusBadgeStyle();

        [Tooltip("일반 이동에서 칸 하나를 지나는 시간(초).")]
        [SerializeField, Min(0f)] private float moveCellDuration = 0.12f;
        [Tooltip("돌진 전체에 걸리는 시간(초, 칸 수와 무관). 돌진 경로 이펙트(FX_Dash)의 travelTime 과 맞춘다.")]
        [SerializeField, Min(0f)] private float dashDuration = 0.15f;
        [Tooltip("밀려남/당겨짐에서 칸 하나를 지나는 시간(초). 감속하며 미끄러진다.")]
        [SerializeField, Min(0f)] private float knockbackCellDuration = 0.1f;
        [Tooltip("밀려남/당겨짐이 출발하기 전 기다리는 시간(초). 스킬 타격 연출(심판 둔기 등)이 닿는 시점과 맞춘다.")]
        [SerializeField, Min(0f)] private float knockbackDelay = 0.15f;

        private readonly List<Vector3> _movePoints = new List<Vector3>();
        /// <summary>방금 소환되어 뷰를 만들 때 솟아오르는 연출을 할 소환물 Id(ExpectSummon).</summary>
        private readonly HashSet<int> _appearing = new HashSet<int>();

        /// <summary>
        /// 유닛을 지금 자리에서 cells(거치는 칸, 마지막이 도착 칸)를 차례로 지나 옮긴다. Sync 전에 호출한다.
        /// 돌진은 칸 수와 상관없이 dashDuration 동안 감속하고, 밀려남/당겨짐은 knockbackDelay 뒤 칸마다 knockbackCellDuration 으로 감속하며 미끄러진다.
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
                    break;
                case UnitMoveStyle.Knockback:
                    view.BeginMove(_movePoints, knockbackCellDuration * cells.Count, easeOut: true, knockbackDelay);
                    break;
                default:
                    view.BeginMove(_movePoints, moveCellDuration * cells.Count, easeOut: false);
                    break;
            }
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
        public void Build(GameState state, BoardCoordinates coordinates, CharacterVisualCatalog visuals)
        {
            Clear();
            _coordinates = coordinates;
            _state = state;
            _visuals = visuals;

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

        public bool TryGetView(Unit unit, out UnitView view)
        {
            return _views.TryGetValue(unit.Id, out view);
        }

        /// <summary>유닛 Id 로 뷰를 찾는다. 코어에서 이미 빠진(사망한) 유닛도 뷰가 남아 있으면 찾는다.</summary>
        public bool TryGetView(int unitId, out UnitView view)
        {
            return _views.TryGetValue(unitId, out view) && view != null;
        }

        public void Clear()
        {
            foreach (UnitView view in _views.Values)
            {
                if (view != null)
                    Destroy(view.gameObject);
            }

            _views.Clear();
            _appearing.Clear();
            _state = null;
        }

        /// <summary>소환물은 팀 색을 옅게 해 본체와 구분한다.</summary>
        private UnitView CreateView(Unit unit)
        {
            Color color = GetTeamColor(unit.Team);
            if (unit.IsSummon)
                color = Color.Lerp(color, Color.white, summonLighten);

            Color actedColor = Color.Lerp(color, Color.gray, actedDarken);
            if (statusVisuals == null)
                statusVisuals = Resources.Load<StatusVisualCatalog>(StatusVisualCatalog.ResourcePath);

            UnitView view = UnitView.Create(
                transform, unit, color, actedColor, FindFace(unit), _visuals?.FaceMask, _visuals?.FaceFrame, statusVisuals, statusBadge, FindBodyPrefab(unit));

            // 소환물: 소환자 캐릭터의 소환물 모습(분신 → 그림자, 발밑 팀 색 고리). 방금 소환된 것이면 바닥에서 솟아오른다.
            CharacterVisual summonVisual = FindSummonVisual(unit);
            view.ApplySummonLook(summonVisual, GetTeamColor(unit.Team));
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
