using System.Collections;
using System.Collections.Generic;
using MiniChess.Client.Common;
using MiniChess.Client.Visuals;
using MiniChess.Core.State;
using MiniChess.Core.Statuses;
using UnityEngine;

namespace MiniChess.Client.Units
{
    /// <summary>
    /// 유닛 하나의 표시(몸체 + 발밑 팀 고리 + 머리 위 작은 얼굴, HP 바, 상태 아이콘). 몸체는 캐릭터 모델 프리팹이 있으면 그것을, 없으면 캡슐을 쓴다(UnitBody).
    /// 이름과 정확한 HP 숫자는 머리 위에 띄우지 않고 하단 유닛 카드에서 본다.
    /// 모델 몸체는 팀 색을 칠하지 않으므로 팀은 발밑 고리로 구분하고, 이번 턴 행동을 마치면 고리를 어둡게 한다. 몸체는 상대 진영 쪽(facingYaw)을 본다.
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
        private const float FaceSize = 0.2f;
        private const float FaceFrame = 0.05f;
        /// <summary>캡슐 꼭대기와 얼굴 사이, 얼굴과 HP 바 사이 간격.</summary>
        private const float FaceGap = 0.08f;
        /// <summary>
        /// 탭으로 유닛을 고르는 몸통 원기둥의 반지름(월드 단위). 실제 캐릭터 실루엣에 맞춰 가늘게 잡는다
        /// (두꺼우면 비스듬한 시점에서 뒤 칸을 많이 가린다).
        /// </summary>
        private const float PickRadius = 0.25f;
        /// <summary>머리 위 HP 바 크기(월드 단위).</summary>
        private const float HpBarWidth = 0.42f;
        private const float HpBarHeight = 0.06f;
        /// <summary>행동 아이콘 한 변 길이(월드 단위)와 등장/사라짐 시간.</summary>
        private const float IconSize = 0.55f;
        private const float IconPopTime = 0.12f;
        private const float IconFadeTime = 0.15f;
        /// <summary>관문 이동에서 가라앉는 깊이(몸 높이보다 조금 깊게)와 가장 작아지는 크기.</summary>
        private const float PortalDepth = 1.2f;
        private const float PortalMinScale = 0.35f;
        /// <summary>발밑 팀 색 고리(Resources, MiniChess/GlowMarker)와 지름(월드).</summary>
        private const string TeamRingPath = "Materials/TeamRing";
        private const float TeamRingSize = 0.8f;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        /// <summary>그림자 소환물의 얼굴 색(보라 그림자 톤, 반투명).</summary>
        private static readonly Color SummonFaceTint = new Color(0.75f, 0.55f, 1f, 0.7f);

        private UnitBody _body;
        private StatusVisualCatalog _statusVisuals;
        /// <summary>지금 몸체 머티리얼을 정한 상태 표시(없으면 null). 바뀔 때 굳기/깨지기 연출 시간을 여기서 읽는다.</summary>
        private StatusVisual _bodyStatus;
        private UnitHpBar _hpBar;
        private FaceBadge _face;
        private StatusBadgeRow _statuses;
        /// <summary>머리 위 표시(HP 바 → 상태 아이콘 줄)가 시작되는 높이(얼굴 위).</summary>
        private float _overheadHeight;
        /// <summary>탭 판정: 몸통 원기둥 높이(몸 꼭대기), 머리 위 얼굴 구의 중심 높이와 반지름(얼굴이 없으면 반지름 0).</summary>
        private float _pickBodyTop;
        private float _pickFaceHeight;
        private float _pickFaceRadius;
        private Transform _iconRoot;
        private SpriteRenderer _icon;
        private Coroutine _iconRoutine;
        /// <summary>행동 아이콘을 띄울 높이(얼굴 중심, 얼굴이 없으면 캡슐 위).</summary>
        private float _iconHeight;
        private Color _teamColor;
        private Color _actedColor;
        private Renderer _ring;
        private MaterialPropertyBlock _ringBlock;
        private Color _ringColor;
        private Color _ringActedColor;
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

        /// <summary>피해 숫자의 기준 위치. 사망 처리로 코어의 Position 이 없어져도 현재 뷰 위치를 사용한다.</summary>
        public Vector3 DamageNumberPosition => transform.TransformPoint(Vector3.up * (_overheadHeight + 0.3f));

        /// <param name="face">얼굴 스프라이트. null 이면 얼굴 없이 캡슐과 라벨만 그린다.</param>
        /// <param name="faceMask">얼굴 마스크 모양. null 이면 원.</param>
        /// <param name="frameShape">얼굴 테두리 모양. null 이면 원.</param>
        /// <param name="statusVisuals">상태 이름/색/아이콘. null 이면 상태를 기본 색과 이름 글자로 그린다.</param>
        /// <param name="statusStyle">상태 배지 모양(UnitsView 인스펙터 값, 공유). null 이면 기본값.</param>
        /// <param name="bodyPrefab">캐릭터 모델 프리팹. null 이면 캡슐.</param>
        /// <param name="ringColor">발밑 고리 색(팀 색). ringActedColor 는 이번 턴 행동을 마쳤을 때의 색.</param>
        /// <param name="facingYaw">몸체가 바라볼 방향(도, 0 = +z). 모델 프리팹은 +z 를 앞으로 만든다.</param>
        public static UnitView Create(
            Transform parent, Unit unit, Color teamColor, Color actedColor, Color ringColor, Color ringActedColor, float facingYaw,
            Sprite face, Sprite faceMask = null, Sprite frameShape = null,
            StatusVisualCatalog statusVisuals = null, StatusBadgeStyle statusStyle = null, GameObject bodyPrefab = null)
        {
            var root = new GameObject($"Unit {unit.Id} {unit.Team} {unit.Stats.Base.Id}");
            root.transform.SetParent(parent, false);

            var view = root.AddComponent<UnitView>();
            view.Unit = unit;
            view._teamColor = teamColor;
            view._actedColor = actedColor;
            view._ringColor = ringColor;
            view._ringActedColor = ringActedColor;
            view._statusVisuals = statusVisuals;
            float scale = unit.IsSummon ? SummonScale : 1f;
            view._body = UnitBody.Create(root.transform, bodyPrefab, BodyHeight, BodyWidth, scale);
            view._body.transform.localRotation = Quaternion.Euler(0f, facingYaw, 0f) * view._body.transform.localRotation;
            view.CreateTeamRing();
            view._pickBodyTop = BodyHeight * scale;
            float labelHeight = BodyHeight * scale + FaceGap;
            view._iconHeight = labelHeight + IconSize * 0.5f;

            if (face != null)
            {
                view._face = FaceBadge.Create(root.transform, face, teamColor, FaceSize * scale, FaceFrame, faceMask, frameShape);
                view._face.transform.localPosition = Vector3.up * (labelHeight + view._face.OuterSize * 0.5f);
                view._iconHeight = labelHeight + view._face.OuterSize * 0.5f;
                view._pickFaceHeight = view._iconHeight;
                view._pickFaceRadius = view._face.OuterSize * 0.5f;

                // 소환물(분신)은 소환자 얼굴을 옅게 쓴다.
                if (unit.IsSummon)
                    view._face.SetImageColor(new Color(1f, 1f, 1f, 0.6f));

                labelHeight += view._face.OuterSize + FaceGap;
            }

            // 아래부터 캡슐 → 얼굴 → HP 바 → 상태 아이콘 줄(상태가 있을 때만).
            view._overheadHeight = labelHeight;
            view._hpBar = UnitHpBar.Create(root.transform, HpBarWidth * scale, HpBarHeight);
            view._hpBar.transform.localPosition = Vector3.up * (labelHeight + HpBarHeight * 0.5f);
            view._statuses = StatusBadgeRow.Create(root.transform, statusVisuals, statusStyle);

            return view;
        }

        /// <summary>
        /// 소환물 모습을 소환자 캐릭터의 소환물 데이터로 바꾼다(분신 → 그림자 몸, 발밑 연기, 보라빛 얼굴).
        /// 기본 몸체 머티리얼이 있으면 팀 색 대신 그 머티리얼로 그린다(팀은 발밑 고리로 구분). 데이터가 없으면 그대로(옅은 팀 색).
        /// </summary>
        public void ApplySummonLook(CharacterVisual visual)
        {
            if (visual == null || visual.SummonBodyMaterial == null)
                return;

            _body.SetBaseMaterial(visual.SummonBodyMaterial);
            if (visual.SummonAura != null)
                Instantiate(visual.SummonAura, transform, false).name = "Summon Aura";
            if (_face != null)
                _face.SetImageColor(SummonFaceTint);
        }

        /// <summary>
        /// 화면 탭의 레이가 이 유닛에 맞는지. 맞으면 레이 위 거리.
        /// 판정은 실제 몸(발밑부터 몸 꼭대기까지의 가는 세로 원기둥)과 머리 위 얼굴(작은 구)만이다. 그 사이 간격, HP 바, 상태 아이콘은 판정이 없다.
        /// 콜라이더 없이 계산하므로 비스듬한 시점에서 상반신/얼굴을 눌러도 그 뒤 칸이 아니라 이 유닛을 고른다. 숨긴 유닛은 맞지 않는다.
        /// </summary>
        public bool TryPick(Ray ray, out float distance)
        {
            distance = float.PositiveInfinity;
            if (!gameObject.activeInHierarchy)
                return false;

            Vector3 foot = transform.position;
            if (_pickFaceRadius > 0f)
            {
                // 얼굴 구: 레이 위에서 구 중심에 가장 가까운 점이 반지름 안이면 맞음(앞쪽 교차 거리).
                Vector3 toFace = foot + Vector3.up * _pickFaceHeight - ray.origin;
                float along = Vector3.Dot(toFace, ray.direction);
                float squared = toFace.sqrMagnitude - along * along;
                float radius = _pickFaceRadius * _pickFaceRadius;
                if (along >= 0f && squared <= radius)
                    distance = along - Mathf.Sqrt(radius - squared);
            }

            float top = foot.y + _pickBodyTop;
            Vector2 origin = new Vector2(ray.origin.x - foot.x, ray.origin.z - foot.z);
            Vector2 direction = new Vector2(ray.direction.x, ray.direction.z);

            // 옆면: 수평으로 축에서 PickRadius 거리가 되는 지점 중 앞쪽, 그 높이가 몸통 안이면 맞음.
            float a = Vector2.Dot(direction, direction);
            float b = 2f * Vector2.Dot(origin, direction);
            float c = Vector2.Dot(origin, origin) - PickRadius * PickRadius;
            float discriminant = b * b - 4f * a * c;
            if (a > 1e-6f && discriminant >= 0f)
            {
                float enter = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
                float y = ray.origin.y + ray.direction.y * enter;
                if (enter >= 0f && enter < distance && y >= foot.y && y <= top)
                    distance = enter;
            }

            // 윗면: 위에서 내려다보면 몸 꼭대기 원판으로 들어온다.
            if (Mathf.Abs(ray.direction.y) > 1e-6f)
            {
                float enter = (top - ray.origin.y) / ray.direction.y;
                Vector2 hit = origin + direction * enter;
                if (enter >= 0f && enter < distance && hit.sqrMagnitude <= PickRadius * PickRadius)
                    distance = enter;
            }

            return !float.IsPositiveInfinity(distance);
        }

        /// <summary>몸체가 delay 초 뒤 duration 초 동안 바닥에서 솟아오르며 형체를 갖춘다(소환 연출).</summary>
        public void PlayAppear(float delay, float duration)
        {
            _body.Appear(delay, duration);
        }

        private void CreateTeamRing()
        {
            Material material = Resources.Load<Material>(TeamRingPath);
            if (material == null)
                return;

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ring.name = "Team Ring";
            Destroy(ring.GetComponent<Collider>()); // 클릭은 칸이 받는다
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = Vector3.up * 0.015f;
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 바닥에 눕힌다
            ring.transform.localScale = Vector3.one * TeamRingSize;
            _ring = ring.GetComponent<Renderer>();
            _ring.sharedMaterial = material;
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ring.receiveShadows = false;
            _ringBlock = new MaterialPropertyBlock();
            SetRingColor(_ringColor);
        }

        private void SetRingColor(Color color)
        {
            if (_ring == null)
                return;

            _ringBlock.SetColor(ColorId, color);
            _ring.SetPropertyBlock(_ringBlock);
        }

        /// <summary>같은 유닛(같은 Id)을 새로 만든 상태의 객체로 바꾼다(온라인 스냅샷 복원). 모양은 그대로 둔다.</summary>
        public void Rebind(Unit unit)
        {
            Unit = unit;
        }

        /// <summary>코어 상태를 그대로 반영한다. 보드에서 빠진 유닛(사망 등)은 숨긴다. 이번 턴에 쓸 기본 공격/스킬이 없으면(행동 종료 포함) 어둡게 그린다.</summary>
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

            bool acted = !Unit.HasCombatActionLeft;
            _body.SetTint(acted ? _actedColor : _teamColor);
            SetRingColor(acted ? _ringActedColor : _ringColor);
            SyncBodyMaterial();
            if (_face != null)
                _face.SetDimmed(acted);

            _statuses.Sync(Unit.Statuses);
            _body.SyncStatuses(Unit.Statuses);
            _hpBar.Set(Unit.Stats.CurrentHp, Unit.Stats.MaxHp, Unit.Stats.Shield);
            _statuses.transform.localPosition = Vector3.up * (_overheadHeight + _hpBar.Height + FaceGap * 0.5f + _statuses.BadgeSize * 0.5f);
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

        /// <summary>몸체 동작 애니메이션(공격, 스킬)을 재생한다. 애니메이션 없는 몸체는 무시.</summary>
        public void PlayAction(UnitAction action)
        {
            _body.PlayAction(action);
        }

        /// <summary>동작 클립의 타격 시점(초, Hit 이벤트). 애니메이션이나 Hit 이벤트가 없으면 null.</summary>
        public float? GetHitTime(UnitAction action)
        {
            return _body.GetEventTime(action, UnitActions.HitEvent);
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
    }
}
