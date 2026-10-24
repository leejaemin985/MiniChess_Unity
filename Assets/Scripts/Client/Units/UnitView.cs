using System.Collections;
using System.Collections.Generic;
using MiniChess.Client.Common;
using MiniChess.Client.Visuals;
using MiniChess.Core.State;
using MiniChess.Core.Statuses;
using TMPro;
using UnityEngine;

namespace MiniChess.Client.Units
{
    /// <summary>
    /// 유닛 하나의 표시(몸체 + 얼굴 + 이름/HP 라벨). 몸체는 캐릭터 모델 프리팹이 있으면 그것을, 없으면 캡슐을 쓴다(UnitBody).
    /// 얼굴은 시각 데이터가 있을 때만 몸체 위에 띄운다. 코어 Unit 을 읽기만 하며, Sync 로 상태를 그대로 반영한다.
    /// 상태 표시 데이터에 몸체 머티리얼이 있는 상태(봉인 등)에 걸려 있으면 몸체 머티리얼을 그것으로 바꾼다.
    /// </summary>
    public class UnitView : MonoBehaviour
    {
        private const float BodyHeight = 1f;
        private const float BodyWidth = 0.6f;
        /// <summary>소환물(분신)은 본체보다 작게 그린다.</summary>
        private const float SummonScale = 0.75f;
        /// <summary>얼굴 한 변 길이와 테두리 두께(월드 단위).</summary>
        private const float FaceSize = 0.6f;
        private const float FaceFrame = 0.05f;
        /// <summary>캡슐 꼭대기와 얼굴 사이, 얼굴과 라벨 사이 간격.</summary>
        private const float FaceGap = 0.08f;
        /// <summary>행동 아이콘 한 변 길이(월드 단위)와 등장/사라짐 시간.</summary>
        private const float IconSize = 0.55f;
        private const float IconPopTime = 0.12f;
        private const float IconFadeTime = 0.15f;
        /// <summary>관문 이동에서 가라앉는 깊이(몸 높이보다 조금 깊게)와 가장 작아지는 크기.</summary>
        private const float PortalDepth = 1.2f;
        private const float PortalMinScale = 0.35f;

        private UnitBody _body;
        private StatusVisualCatalog _statusVisuals;
        /// <summary>지금 몸체 머티리얼을 정한 상태 표시(없으면 null). 바뀔 때 굳기/깨지기 연출 시간을 여기서 읽는다.</summary>
        private StatusVisual _bodyStatus;
        private TextMeshPro _label;
        private FaceBadge _face;
        private StatusBadgeRow _statuses;
        /// <summary>상태가 없을 때 라벨(이름/HP) 아래끝 높이. 상태가 있으면 그 사이에 상태 아이콘 줄이 들어가고 라벨이 올라간다.</summary>
        private float _labelBaseHeight;
        private Transform _iconRoot;
        private SpriteRenderer _icon;
        private Coroutine _iconRoutine;
        /// <summary>행동 아이콘을 띄울 높이(얼굴 중심, 얼굴이 없으면 캡슐 위).</summary>
        private float _iconHeight;
        private Color _teamColor;
        private Color _actedColor;
        /// <summary>Sync 가 정한 자리. 이동 연출이 끝나면 여기에 맞춘다.</summary>
        private Vector3 _targetPosition;
        /// <summary>이동 연출 경로(지금 자리 → 거치는 칸들). 비어 있으면 이동 중이 아니다(Sync 가 바로 옮긴다).</summary>
        private readonly List<Vector3> _movePath = new List<Vector3>();
        private float _moveStart;
        private float _moveDuration;
        /// <summary>경로 전체에 감속을 거는지(돌진). 아니면 일정한 속도로 칸을 지난다.</summary>
        private bool _moveEaseOut;
        /// <summary>관문 이동 중인지(출발 칸으로 가라앉았다가 도착 칸에서 솟아오름). 이동 중이면 Sync 가 자리를 바로 옮기지 않는다.</summary>
        private bool _portal;
        private Vector3 _portalFrom;
        private Vector3 _portalTo;
        private float _portalStart;
        private float _portalSink;
        private float _portalRise;

        public Unit Unit { get; private set; }

        /// <param name="face">얼굴 스프라이트. null 이면 얼굴 없이 캡슐과 라벨만 그린다.</param>
        /// <param name="faceMask">얼굴 마스크 모양. null 이면 원.</param>
        /// <param name="frameShape">얼굴 테두리 모양. null 이면 원.</param>
        /// <param name="statusVisuals">상태 이름/색/아이콘. null 이면 상태를 기본 색과 이름 글자로 그린다.</param>
        /// <param name="statusStyle">상태 배지 모양(UnitsView 인스펙터 값, 공유). null 이면 기본값.</param>
        /// <param name="bodyPrefab">캐릭터 모델 프리팹. null 이면 캡슐.</param>
        public static UnitView Create(
            Transform parent, Unit unit, Color teamColor, Color actedColor, Sprite face, Sprite faceMask = null, Sprite frameShape = null,
            StatusVisualCatalog statusVisuals = null, StatusBadgeStyle statusStyle = null, GameObject bodyPrefab = null)
        {
            var root = new GameObject($"Unit {unit.Id} {unit.Team} {unit.Stats.Base.Id}");
            root.transform.SetParent(parent, false);

            var view = root.AddComponent<UnitView>();
            view.Unit = unit;
            view._teamColor = teamColor;
            view._actedColor = actedColor;
            view._statusVisuals = statusVisuals;
            float scale = unit.IsSummon ? SummonScale : 1f;
            view._body = UnitBody.Create(root.transform, bodyPrefab, BodyHeight, BodyWidth, scale);
            float labelHeight = BodyHeight * scale + FaceGap;
            view._iconHeight = labelHeight + IconSize * 0.5f;

            if (face != null)
            {
                view._face = FaceBadge.Create(root.transform, face, teamColor, FaceSize * scale, FaceFrame, faceMask, frameShape);
                view._face.transform.localPosition = Vector3.up * (labelHeight + view._face.OuterSize * 0.5f);
                view._iconHeight = labelHeight + view._face.OuterSize * 0.5f;

                // 소환물(분신)은 소환자 얼굴을 옅게 쓴다.
                if (unit.IsSummon)
                    view._face.SetImageColor(new Color(1f, 1f, 1f, 0.6f));

                labelHeight += view._face.OuterSize + FaceGap;
            }

            // 아래부터 캡슐 → 얼굴 → 상태 아이콘 줄(상태가 있을 때만) → 이름/HP.
            view._labelBaseHeight = labelHeight;
            view._statuses = StatusBadgeRow.Create(root.transform, statusVisuals, statusStyle);
            view._label = CreateLabel(root.transform, labelHeight);

            return view;
        }

        /// <summary>같은 유닛(같은 Id)을 새로 만든 상태의 객체로 바꾼다(온라인 스냅샷 복원). 모양은 그대로 둔다.</summary>
        public void Rebind(Unit unit)
        {
            Unit = unit;
        }

        /// <summary>코어 상태를 그대로 반영한다. 보드에서 빠진 유닛(사망 등)은 숨긴다.</summary>
        public void Sync(BoardCoordinates coordinates)
        {
            if (!Unit.IsPlaced)
            {
                _movePath.Clear();
                EndPortal();
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            _targetPosition = coordinates.ToWorld(Unit.Position.Value);
            if (_movePath.Count == 0 && !_portal)
                transform.localPosition = _targetPosition;

            UnitTurnState turn = Unit.TurnState;
            bool acted = turn.CombatActionUsed || turn.ActionsEnded;
            _body.SetTint(acted ? _actedColor : _teamColor);
            SyncBodyMaterial();
            if (_face != null)
                _face.SetDimmed(acted);

            _statuses.Sync(Unit.Statuses);
            float badgeSize = _statuses.BadgeSize;
            _statuses.transform.localPosition = Vector3.up * (_labelBaseHeight + badgeSize * 0.5f);
            float statusRow = Unit.Statuses.Count > 0 ? badgeSize + FaceGap : 0f;
            _label.transform.localPosition = Vector3.up * (_labelBaseHeight + statusRow);
            _label.text = $"<size=70%>{Unit.Stats.Base.Name}</size>\n{Unit.Stats.CurrentHp}/{Unit.Stats.MaxHp}{FormatShield()}";
        }

        /// <summary>
        /// 지금 자리에서 cells(거치는 칸들의 월드 위치, 마지막이 도착 칸)를 차례로 지나 duration 초 동안 옮긴다. Sync 전에 호출한다.
        /// easeOut 이면 경로 전체에 감속을 건다(돌진, 밀려남), 아니면 일정한 속도로 지난다(일반 이동).
        /// delay 초 동안은 제자리에 있다가 출발한다(밀려남을 타격 시점에 맞출 때).
        /// 끝나면 Sync 가 정한 자리에 맞춘다. 이 연출 없이 Sync 하면 바로 옮겨진다(워프 등).
        /// </summary>
        public void BeginMove(IReadOnlyList<Vector3> cells, float duration, bool easeOut, float delay = 0f)
        {
            if (!gameObject.activeInHierarchy || duration <= 0f || cells == null || cells.Count == 0)
                return;

            _movePath.Clear();
            _movePath.Add(transform.localPosition);
            _movePath.AddRange(cells);
            _moveStart = Time.time + delay;
            _moveDuration = duration;
            _moveEaseOut = easeOut;
        }

        /// <summary>
        /// 관문 이동: delay 초 뒤 sinkTime 동안 from 칸 아래로 작아지며 가라앉고, riseTime 동안 to 칸에서 커지며 솟아오른다(실 관문 등). Sync 전에 호출한다.
        /// 위치는 BeginMove 와 같은 좌표(칸 윗면 중심). 끝나면 Sync 가 정한 자리에 맞춘다.
        /// </summary>
        public void BeginPortal(Vector3 from, Vector3 to, float delay, float sinkTime, float riseTime)
        {
            if (!gameObject.activeInHierarchy)
                return;

            _movePath.Clear();
            _portal = true;
            _portalFrom = from;
            _portalTo = to;
            _portalStart = Time.time + delay;
            _portalSink = Mathf.Max(0.01f, sinkTime);
            _portalRise = Mathf.Max(0.01f, riseTime);
            transform.localPosition = from;
        }

        private void Update()
        {
            if (_portal)
            {
                UpdatePortal();
                return;
            }

            if (_movePath.Count == 0)
                return;

            float t = Mathf.Clamp01((Time.time - _moveStart) / _moveDuration);
            if (_moveEaseOut)
                t = 1f - (1f - t) * (1f - t);

            if (t >= 1f || Mathf.Approximately(_moveDuration, 0f))
            {
                _movePath.Clear();
                transform.localPosition = _targetPosition;
                return;
            }

            // 경로 구간마다 같은 시간을 나눠 쓴다(칸 하나 = 구간 하나).
            float scaled = t * (_movePath.Count - 1);
            int segment = Mathf.Min((int)scaled, _movePath.Count - 2);
            transform.localPosition = Vector3.Lerp(_movePath[segment], _movePath[segment + 1], scaled - segment);
        }

        private void UpdatePortal()
        {
            float elapsed = Time.time - _portalStart;
            if (elapsed >= _portalSink + _portalRise)
            {
                EndPortal();
                transform.localPosition = _targetPosition;
                return;
            }

            // under: 0 = 제자리, 1 = 바닥 아래로 다 들어감.
            bool rising = elapsed >= _portalSink;
            float t = rising ? 1f - (elapsed - _portalSink) / _portalRise : Mathf.Clamp01(elapsed / _portalSink);
            float under = t * t;
            transform.localPosition = (rising ? _portalTo : _portalFrom) + Vector3.down * (PortalDepth * under);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, PortalMinScale, under);
        }

        private void EndPortal()
        {
            _portal = false;
            transform.localScale = Vector3.one;
        }

        /// <summary>
        /// 행동 아이콘(기본 공격 등)을 얼굴 위에 duration 초 동안 띄운다. 커지며 나타나고 끝에 흐려진다.
        /// 이미 떠 있으면 처음부터 다시 보여준다.
        /// </summary>
        public void ShowActionIcon(Sprite icon, float duration)
        {
            if (icon == null || !gameObject.activeInHierarchy)
                return;

            if (_icon == null)
                CreateIcon();

            _icon.sprite = icon;
            float width = icon.bounds.size.x;
            _icon.transform.localScale = Vector3.one * (width > 0f ? IconSize / width : IconSize);

            if (_iconRoutine != null)
                StopCoroutine(_iconRoutine);
            _iconRoutine = StartCoroutine(IconRoutine(duration));
        }

        private IEnumerator IconRoutine(float duration)
        {
            _iconRoot.gameObject.SetActive(true);

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float pop = Mathf.Clamp01(t / IconPopTime);
                _iconRoot.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, 1f - (1f - pop) * (1f - pop));

                float remaining = duration - t;
                _icon.color = new Color(1f, 1f, 1f, remaining < IconFadeTime ? remaining / IconFadeTime : 1f);
                yield return null;
            }

            _iconRoot.gameObject.SetActive(false);
            _iconRoutine = null;
        }

        private void CreateIcon()
        {
            var root = new GameObject("ActionIcon");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.up * _iconHeight;
            root.AddComponent<Billboard>();
            _iconRoot = root.transform;

            var image = new GameObject("Icon");
            image.transform.SetParent(_iconRoot, false);
            // 얼굴보다 카메라 쪽으로 살짝 앞에, 정렬 순서도 위로 둔다.
            image.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            _icon = image.AddComponent<SpriteRenderer>();
            _icon.sortingOrder = 10;

            root.SetActive(false);
        }

        /// <summary>보호막이 있으면 HP 옆에 " +3" 형태(하늘색)로 붙인다.</summary>
        private string FormatShield()
        {
            int shield = Unit.Stats.Shield;
            return shield > 0 ? $" <color=#7FD4FF>+{shield}</color>" : string.Empty;
        }

        /// <summary>
        /// 몸체 머티리얼을 걸린 상태대로 맞춘다. 원래 모습 → 상태 머티리얼은 굳기(BodyApplyTime), 상태 머티리얼 → 원래 모습은 깨지기(BodyRemoveTime)로 연출하고,
        /// 상태 머티리얼끼리 바뀌면 바로 바꾼다.
        /// </summary>
        private void SyncBodyMaterial()
        {
            StatusVisual next = FindBodyStatus();
            if (next == _bodyStatus)
                return;

            if (_bodyStatus == null)
                _body.PetrifyTo(next.BodyMaterial, next.BodyApplyTime);
            else if (next == null)
                _body.ShatterFrom(_bodyStatus.BodyMaterial, _bodyStatus.BodyRemoveTime);
            else
                _body.PetrifyTo(next.BodyMaterial, 0f);

            _bodyStatus = next;
        }

        /// <summary>걸린 상태 중 몸체 머티리얼이 지정된 첫 상태(먼저 걸린 상태 우선). 없으면 null(원래 머티리얼).</summary>
        private StatusVisual FindBodyStatus()
        {
            if (_statusVisuals == null)
                return null;

            foreach (StatusEffect status in Unit.Statuses)
            {
                StatusVisual visual = _statusVisuals.Find(status.Definition.Id);
                if (visual != null && visual.BodyMaterial != null)
                    return visual;
            }

            return null;
        }

        private static TextMeshPro CreateLabel(Transform parent, float height)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * height;
            go.AddComponent<Billboard>();

            var label = go.AddComponent<TextMeshPro>();
            KoreanFont.Apply(label);
            label.fontSize = 3f;
            label.alignment = TextAlignmentOptions.Bottom;
            label.enableWordWrapping = false;
            label.color = Color.white;
            label.outlineWidth = 0.25f;
            label.outlineColor = Color.black;
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            label.rectTransform.sizeDelta = new Vector2(2.5f, 1.5f);

            return label;
        }
    }
}
