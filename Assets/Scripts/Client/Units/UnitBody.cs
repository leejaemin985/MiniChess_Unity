using System.Collections.Generic;
using MiniChess.Client.Common;
using MiniChess.Client.Visuals;
using UnityEngine;

namespace MiniChess.Client.Units
{
    /// <summary>
    /// 유닛의 몸체(캡슐 또는 캐릭터 모델 프리팹). 하위 렌더러를 모두 모아 색과 머티리얼을 한곳에서 다룬다.
    /// MeshRenderer 와 SkinnedMeshRenderer 는 둘 다 Renderer 라 몸체 종류와 상관없이 같은 방식으로 처리한다.
    /// 머티리얼 교체(상태 표현 등) 중에는 색을 칠하지 않는다(교체 머티리얼의 _BaseColor 가 팀 색으로 물들지 않게).
    /// 교체를 연출할 수도 있다(굳기/깨지기, 마법석 셰이더처럼 _FillHeight/_Crack/_Dissolve 를 가진 머티리얼만): 같은 메시를 겹쳐 그린 덧씌움 렌더러로
    /// 원래 모습 위에 교체 머티리얼이 아래에서 위로 차오르거나(굳기), 교체를 먼저 걷어낸 뒤 덧씌움이 금 가며 조각나 사라진다(깨지기).
    /// 기본 머티리얼(SetBaseMaterial, 분신의 그림자 등)을 쓰면 원래 머티리얼 대신 그것이 기본 모습이 되고, 매 프레임 몸체 높이 범위(_BottomY/_TopY)를 넣어 준다.
    /// 그 몸체는 바닥에서 솟아오르며 형체를 갖출 수 있다(Appear, _FillHeight).
    /// 순간이동 노이즈(BlinkOut/BlinkIn): 캐릭터 툰 셰이더의 _BLINK 변형으로 세로 줄무늬가 번지며 사라지거나 나타난다.
    /// 재생하는 동안만 머티리얼을 복제해 키워드를 켜고, 다 나타나면 원래 머티리얼로 되돌린다(평소 변형에는 비용이 없다).
    /// _Blink 를 지원하지 않는 머티리얼의 렌더러는 절반쯤 사라졌을 때 통째로 숨긴다.
    /// </summary>
    public class UnitBody : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int FillHeightId = Shader.PropertyToID("_FillHeight");
        private static readonly int CrackId = Shader.PropertyToID("_Crack");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int DepthOffsetId = Shader.PropertyToID("_DepthOffset");
        private static readonly int BottomYId = Shader.PropertyToID("_BottomY");
        private static readonly int TopYId = Shader.PropertyToID("_TopY");
        private static readonly int BlinkId = Shader.PropertyToID("_Blink");
        private static readonly int BlinkSeedId = Shader.PropertyToID("_BlinkSeed");
        private const string BlinkKeyword = "_BLINK";
        private const float NoFill = 100000f;

        /// <summary>깨지기 연출에서 금이 번지는 구간(나머지는 조각나 사라짐).</summary>
        private const float CrackPortion = 0.55f;
        /// <summary>굳기 경계가 몸체 위아래 끝을 넘어서는 여유(월드).</summary>
        private const float FillMargin = 0.1f;

        private enum Transition { None, Petrify, Shatter }

        private Renderer[] _renderers;
        private Material[][] _originals;
        /// <summary>리깅된 모델 몸체의 Animator. 캡슐이나 리깅 안 된 모델이면 null.</summary>
        private Animator _animator;
        /// <summary>설치 여부에 따라 자세를 바꾸는 부품(박격포 설치 → 권총 허리춤 등). 없으면 빈 배열.</summary>
        private InstallStance[] _stances;
        private MaterialPropertyBlock _block;
        /// <summary>색을 칠하는 몸체인지. 캡슐만 칠하고, 모델은 텍스처 색이 덮이지 않게 칠하지 않는다.</summary>
        private bool _tintable;
        private Color? _tint;
        private Material _override;

        private readonly List<Renderer> _overlays = new List<Renderer>();
        private MaterialPropertyBlock _overlayBlock;
        private Material _overlaySource;
        private Material _overlayMaterial;
        private Transition _transition;
        private float _transitionTime;
        private float _transitionDuration;

        private Material _baseMaterial;
        private MaterialPropertyBlock _baseBlock;
        private float _appearDelay;
        private float _appearDuration;
        private float _appearTime = -1f;

        /// <summary>순간이동 노이즈 중인 렌더러별 복제 머티리얼(지원하지 않는 칸은 원래 머티리얼). 재생 중이 아니면 null.</summary>
        private Material[][] _blinkMaterials;
        private readonly List<Material> _blinkClones = new List<Material>();
        private float _blink;
        private float _blinkFrom;
        private float _blinkTo;
        private float _blinkTime;
        private float _blinkDuration;
        private bool _blinkPlaying;

        /// <param name="prefab">캐릭터 모델 프리팹. null 이면 width × height 캡슐을 만든다.</param>
        /// <param name="scale">전체 크기 배율(소환물 축소 등).</param>
        public static UnitBody Create(Transform parent, GameObject prefab, float height, float width, float scale)
        {
            GameObject body;
            if (prefab != null)
            {
                body = Instantiate(prefab, parent, false);
                body.transform.localScale *= scale;
            }
            else
            {
                // 기본 캡슐은 높이 2, 지름 1.
                body = RuntimePrimitive.Create(PrimitiveType.Capsule);
                body.transform.SetParent(parent, false);
                body.transform.localPosition = Vector3.up * (height * 0.5f * scale);
                body.transform.localScale = new Vector3(width, height * 0.5f, width) * scale;
            }

            body.name = "Body";
            var unitBody = body.AddComponent<UnitBody>();
            unitBody.Collect(tintable: prefab == null);
            return unitBody;
        }

        /// <summary>동작 애니메이션을 재생한다(Animator 트리거). 애니메이션이 없는 몸체(캡슐, 리깅 안 된 모델)는 무시한다.</summary>
        public void PlayAction(UnitAction action)
        {
            if (_animator != null && _animator.runtimeAnimatorController != null)
                _animator.SetTrigger(UnitActions.TriggerId(action));
        }

        /// <summary>
        /// 동작 클립에서 eventName 애니메이션 이벤트(Hit 등)의 첫 시간(초, 동작 시작부터)을 찾는다.
        /// 동작 클립은 지금 컨트롤러(캐릭터별 오버라이드면 바꿔 낀 클립)의 슬롯 클립이다. 애니메이션이 없거나 이벤트가 없으면 null.
        /// </summary>
        public float? GetEventTime(UnitAction action, string eventName)
        {
            AnimationClip clip = FindActionClip(action);
            if (clip == null)
                return null;

            foreach (AnimationEvent animationEvent in clip.events)
            {
                if (animationEvent.functionName == eventName)
                    return animationEvent.time;
            }

            return null;
        }

        /// <summary>동작 클립 길이(초). 애니메이션이 없으면 0.</summary>
        public float GetActionLength(UnitAction action)
        {
            AnimationClip clip = FindActionClip(action);
            return clip != null ? clip.length : 0f;
        }

        private AnimationClip FindActionClip(UnitAction action)
        {
            RuntimeAnimatorController controller = _animator != null ? _animator.runtimeAnimatorController : null;
            if (controller == null)
                return null;

            string slot = UnitActions.SlotClipName(action);
            if (controller is AnimatorOverrideController overrides)
                return overrides[slot];

            foreach (AnimationClip clip in controller.animationClips)
            {
                if (clip.name == slot)
                    return clip;
            }

            return null;
        }

        /// <summary>박격포 설치 여부(Unit.IsInstalled)를 몸체 자세(InstallStance: 무기 보관 위치, 보조 대기)에 반영한다. 자세 설정이 없는 몸체는 무시.</summary>
        public void SyncInstalled(bool installed)
        {
            foreach (InstallStance stance in _stances)
                stance.Sync(installed);
        }

        /// <summary>몸체 색(팀 색, 행동 완료 색). 모델 몸체이거나 머티리얼 교체 중이면 기억만 하고 칠하지 않는다.</summary>
        public void SetTint(Color color)
        {
            if (_tint == color)
                return;

            _tint = color;
            ApplyTint();
        }

        /// <summary>모든 렌더러의 모든 머티리얼 칸을 material 로 바꾼다. null 이면 원래 머티리얼로 되돌린다.</summary>
        public void SetOverride(Material material)
        {
            if (_override == material)
                return;

            EndBlink();
            _override = material;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (material == null)
                {
                    _renderers[i].sharedMaterials = _originals[i];
                    continue;
                }

                var replaced = new Material[_originals[i].Length];
                for (int slot = 0; slot < replaced.Length; slot++)
                    replaced[slot] = material;
                _renderers[i].sharedMaterials = replaced;
            }

            ApplyTint();
        }

        /// <summary>
        /// 원래 머티리얼 대신 material 을 기본 모습으로 쓴다(분신의 그림자 몸 등). 색은 칠하지 않고, 매 프레임 몸체 높이 범위를 머티리얼에 넣어 준다.
        /// 상태 교체(SetOverride)가 풀리면 이 머티리얼로 돌아온다.
        /// </summary>
        public void SetBaseMaterial(Material material)
        {
            EndBlink();
            _baseMaterial = material;
            _baseBlock = new MaterialPropertyBlock();
            _tintable = false;
            for (int i = 0; i < _originals.Length; i++)
            {
                var replaced = new Material[_originals[i].Length];
                for (int slot = 0; slot < replaced.Length; slot++)
                    replaced[slot] = material;
                _originals[i] = replaced;
                if (_override == null)
                    _renderers[i].sharedMaterials = replaced;
            }

            ApplyTint();
            UpdateBase();
        }

        /// <summary>기본 머티리얼 몸체가 delay 초 뒤 duration 초 동안 바닥에서 위로 솟아오르며 형체를 갖춘다(그 전에는 보이지 않음). 기본 머티리얼이 _FillHeight 를 지원해야 한다.</summary>
        public void Appear(float delay, float duration)
        {
            if (_baseMaterial == null || duration <= 0f)
                return;

            _appearDelay = delay;
            _appearDuration = duration;
            _appearTime = 0f;
            UpdateBase();
        }

        /// <summary>원래 모습 위로 material 이 아래에서 위로 차올라 굳은 뒤 교체한다(duration 초). duration 이 0 이면 바로 교체한다.</summary>
        public void PetrifyTo(Material material, float duration)
        {
            if (material == null || duration <= 0f)
            {
                EndTransition();
                SetOverride(material);
                return;
            }

            SetOverride(null);
            BeginTransition(Transition.Petrify, material, duration);
        }

        /// <summary>교체(material)를 걷어내 원래 모습으로 되돌리고, 그 위에 덧씌운 material 이 금 가며 조각나 사라지게 한다(duration 초). duration 이 0 이면 바로 되돌린다.</summary>
        public void ShatterFrom(Material material, float duration)
        {
            EndTransition();
            SetOverride(null);
            if (material != null && duration > 0f)
                BeginTransition(Transition.Shatter, material, duration);
        }

        /// <summary>순간이동 노이즈로 duration 초 동안 사라진다. 끝난 뒤에도 BlinkIn 전까지 숨은 채로 있다.</summary>
        public void BlinkOut(float duration) => BeginBlink(1f, duration);

        /// <summary>순간이동 노이즈로 duration 초 동안 나타난다. 다 나타나면 원래 머티리얼로 되돌린다.</summary>
        public void BlinkIn(float duration) => BeginBlink(0f, duration);

        private void BeginBlink(float target, float duration)
        {
            if (_blinkMaterials == null)
            {
                if (target <= 0f)
                    return;
                CreateBlinkMaterials();
            }

            float seed = Random.Range(0f, 100f);
            foreach (Material clone in _blinkClones)
                clone.SetFloat(BlinkSeedId, seed);

            _blinkFrom = _blink;
            _blinkTo = target;
            _blinkTime = 0f;
            _blinkDuration = Mathf.Max(duration, 0.0001f);
            _blinkPlaying = true;
            UpdateBlink();
        }

        /// <summary>지금 머티리얼 칸마다 _Blink 를 지원하면 복제해 키워드를 켜고 바꿔 끼운다.</summary>
        private void CreateBlinkMaterials()
        {
            _blinkMaterials = new Material[_renderers.Length][];
            for (int i = 0; i < _renderers.Length; i++)
            {
                Material[] current = _renderers[i].sharedMaterials;
                var replaced = new Material[current.Length];
                for (int slot = 0; slot < current.Length; slot++)
                {
                    Material source = current[slot];
                    if (source == null || !source.HasProperty(BlinkId))
                    {
                        replaced[slot] = source;
                        continue;
                    }

                    var clone = new Material(source) { name = source.name + " (Blink)" };
                    clone.EnableKeyword(BlinkKeyword);
                    _blinkClones.Add(clone);
                    replaced[slot] = clone;
                }

                _blinkMaterials[i] = replaced;
                _renderers[i].sharedMaterials = replaced;
            }
        }

        private void UpdateBlink()
        {
            if (!_blinkPlaying)
                return;

            _blinkTime += Time.deltaTime;
            float t = Mathf.Clamp01(_blinkTime / _blinkDuration);
            _blink = Mathf.Lerp(_blinkFrom, _blinkTo, Mathf.SmoothStep(0f, 1f, t));
            foreach (Material clone in _blinkClones)
                clone.SetFloat(BlinkId, _blink);

            // _Blink 를 지원하지 않는 렌더러는 절반쯤에서 통째로 숨긴다.
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (!UsesBlink(_blinkMaterials[i]))
                    _renderers[i].enabled = _blink < 0.5f;
            }

            if (t < 1f)
                return;

            _blinkPlaying = false;
            if (_blink <= 0f)
                EndBlink();
        }

        private static bool UsesBlink(Material[] materials)
        {
            foreach (Material material in materials)
            {
                if (material != null && material.IsKeywordEnabled(BlinkKeyword))
                    return true;
            }

            return false;
        }

        /// <summary>노이즈를 멈추고 노이즈 전 머티리얼(원래, 또는 교체 중이면 교체 머티리얼)로 되돌린 뒤 복제본을 지운다.</summary>
        private void EndBlink()
        {
            if (_blinkMaterials == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].enabled = true;
                if (_override == null)
                {
                    _renderers[i].sharedMaterials = _originals[i];
                    continue;
                }

                var replaced = new Material[_originals[i].Length];
                for (int slot = 0; slot < replaced.Length; slot++)
                    replaced[slot] = _override;
                _renderers[i].sharedMaterials = replaced;
            }

            foreach (Material clone in _blinkClones)
                Destroy(clone);
            _blinkClones.Clear();
            _blinkMaterials = null;
            _blinkPlaying = false;
            _blink = 0f;
        }

        private void Update()
        {
            UpdateBlink();
            UpdateBase();
            if (_transition == Transition.None)
                return;

            _transitionTime += Time.deltaTime;
            float t = Mathf.Clamp01(_transitionTime / _transitionDuration);
            _overlayBlock.Clear();
            if (_transition == Transition.Petrify)
            {
                GetHeightRange(out float bottom, out float top);
                float eased = 1f - (1f - t) * (1f - t);
                _overlayBlock.SetFloat(FillHeightId, Mathf.Lerp(bottom - FillMargin, top + FillMargin, eased));
            }
            else
            {
                _overlayBlock.SetFloat(CrackId, Mathf.Clamp01(t / CrackPortion));
                _overlayBlock.SetFloat(DissolveId, Mathf.Clamp01((t - CrackPortion) / (1f - CrackPortion)));
            }

            foreach (Renderer overlay in _overlays)
                overlay.SetPropertyBlock(_overlayBlock);

            if (t < 1f)
                return;

            Material petrified = _transition == Transition.Petrify ? _overlaySource : null;
            EndTransition();
            if (petrified != null)
                SetOverride(petrified);
        }

        /// <summary>숨겨지면(사망 등) 연출을 끝낸다. 굳는 중이었으면 바로 굳힌다.</summary>
        private void OnDisable()
        {
            EndBlink();
            Material petrified = _transition == Transition.Petrify ? _overlaySource : null;
            EndTransition();
            if (petrified != null)
                SetOverride(petrified);
        }

        private void OnDestroy()
        {
            EndBlink();
            if (_overlayMaterial != null)
                Destroy(_overlayMaterial);
        }

        private void BeginTransition(Transition transition, Material material, float duration)
        {
            if (_overlaySource != material)
            {
                _overlaySource = material;
                if (_overlayMaterial != null)
                    Destroy(_overlayMaterial);
                // 같은 메시를 겹쳐 그리므로 깊이를 살짝 앞당겨 원래 모습과 깜빡이지 않게 한다.
                _overlayMaterial = new Material(material) { name = material.name + " (Overlay)" };
                _overlayMaterial.SetFloat(DepthOffsetId, -1f);
            }

            if (_overlayBlock == null)
                CreateOverlays();

            foreach (Renderer overlay in _overlays)
            {
                var materials = new Material[overlay.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = _overlayMaterial;
                overlay.sharedMaterials = materials;
                overlay.gameObject.SetActive(true);
            }

            _transition = transition;
            _transitionTime = 0f;
            _transitionDuration = duration;
            Update();
        }

        private void EndTransition()
        {
            _transition = Transition.None;
            foreach (Renderer overlay in _overlays)
            {
                if (overlay != null)
                    overlay.gameObject.SetActive(false);
            }
        }

        /// <summary>몸체 렌더러마다 같은 메시를 그리는 덧씌움 렌더러를 만든다(스킨 메시는 같은 뼈를 따른다). 그림자는 원래 모습이 드리운다.</summary>
        private void CreateOverlays()
        {
            _overlayBlock = new MaterialPropertyBlock();
            foreach (Renderer source in _renderers)
            {
                Renderer overlay = null;
                if (source is SkinnedMeshRenderer skinned)
                {
                    var go = new GameObject(source.name + " (Overlay)");
                    go.transform.SetParent(source.transform.parent, false);
                    go.transform.localPosition = source.transform.localPosition;
                    go.transform.localRotation = source.transform.localRotation;
                    go.transform.localScale = source.transform.localScale;
                    var copy = go.AddComponent<SkinnedMeshRenderer>();
                    copy.sharedMesh = skinned.sharedMesh;
                    copy.bones = skinned.bones;
                    copy.rootBone = skinned.rootBone;
                    copy.localBounds = skinned.localBounds;
                    overlay = copy;
                }
                else if (source.TryGetComponent(out MeshFilter filter))
                {
                    var go = new GameObject(source.name + " (Overlay)");
                    go.transform.SetParent(source.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    overlay = go.AddComponent<MeshRenderer>();
                }

                if (overlay == null)
                    continue;

                overlay.sharedMaterials = source.sharedMaterials;
                overlay.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                overlay.receiveShadows = source.receiveShadows;
                overlay.gameObject.SetActive(false);
                _overlays.Add(overlay);
            }
        }

        /// <summary>기본 머티리얼 몸체에 높이 범위와 솟아오르는 경계를 넣는다.</summary>
        private void UpdateBase()
        {
            if (_baseMaterial == null)
                return;

            GetHeightRange(out float bottom, out float top);
            float fill = NoFill;
            if (_appearTime >= 0f)
            {
                _appearTime += Time.deltaTime;
                float t = Mathf.Clamp01((_appearTime - _appearDelay) / _appearDuration);
                float eased = 1f - (1f - t) * (1f - t);
                fill = Mathf.Lerp(bottom - FillMargin, top + FillMargin, eased);
                if (t >= 1f)
                    _appearTime = -1f;
            }

            foreach (Renderer target in _renderers)
            {
                target.GetPropertyBlock(_baseBlock);
                _baseBlock.SetFloat(BottomYId, bottom);
                _baseBlock.SetFloat(TopYId, top);
                _baseBlock.SetFloat(FillHeightId, fill);
                target.SetPropertyBlock(_baseBlock);
            }
        }

        private void GetHeightRange(out float bottom, out float top)
        {
            bottom = float.MaxValue;
            top = float.MinValue;
            foreach (Renderer target in _renderers)
            {
                Bounds bounds = target.bounds;
                bottom = Mathf.Min(bottom, bounds.min.y);
                top = Mathf.Max(top, bounds.max.y);
            }
        }

        private void Collect(bool tintable)
        {
            _tintable = tintable;
            _block = new MaterialPropertyBlock();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _animator = GetComponentInChildren<Animator>(true);
            _stances = GetComponentsInChildren<InstallStance>(true);
            if (_animator != null && !_animator.TryGetComponent(out UnitAnimationEvents _))
                _animator.gameObject.AddComponent<UnitAnimationEvents>();
            _originals = new Material[_renderers.Length][];
            for (int i = 0; i < _renderers.Length; i++)
                _originals[i] = _renderers[i].sharedMaterials;
        }

        private void ApplyTint()
        {
            bool tint = _tintable && _override == null && _tint.HasValue;
            foreach (Renderer target in _renderers)
            {
                if (!tint)
                {
                    target.SetPropertyBlock(null);
                    continue;
                }

                target.GetPropertyBlock(_block);
                _block.SetColor(ColorId, _tint.Value);
                target.SetPropertyBlock(_block);
            }
        }
    }
}
