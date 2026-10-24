using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 자리 교정(FX_StonePush). 대상 뒤 바닥에서 마법석 가시(spike)가 미는 방향으로 기울어 솟아 대상을 쳐내고(riseTime 에 맞닿음, 유닛 밀려남 지연과 맞춤),
    /// 대상이 떠난 칸마다 작은 마법석 기둥(pillars)이 차례로 솟아 밀린 칸 수를 보여 준다(usePillars 를 끄면 가시만).
    /// 잠시 뒤 모두 금 가며 부서져 가라앉는다(crumble 파티클). 막혀서 안 밀렸으면 기둥 없이 가시만 솟았다 바로 부서진다.
    /// 조각(가시/기둥)은 피벗이 바닥에 있고, 높이 방향 크기로 솟고 가라앉는다. 파티클은 조각 밖(루트 아래)에 두고 Place 때 조각 자리로 옮긴다.
    /// </summary>
    public sealed class StonePushEffect : PushEffect
    {
        [SerializeField] private Transform spike;
        [Tooltip("대상 칸 중심에서 가시가 솟는 자리까지 뒤로 떨어진 거리(월드).")]
        [SerializeField] private float spikeBack = 0.42f;
        [SerializeField] private ParticleSystem spikeDust;
        [SerializeField] private ParticleSystem impact;
        [SerializeField] private ParticleSystem spikeCrumble;

        [Tooltip("대상이 떠난 칸마다 기둥을 솟게 할지(끄면 가시만 = 밀린 칸 수를 따로 보이지 않음).")]
        [SerializeField] private bool usePillars = true;
        [Tooltip("밀린 칸 순서대로 쓰는 기둥(최대 밀기 칸 수만큼). 각 기둥 아래에 솟을 때 먼지, 부서질 때 파편 파티클을 둔다.")]
        [SerializeField] private Transform[] pillars = new Transform[2];
        [SerializeField] private ParticleSystem[] pillarDust = new ParticleSystem[2];
        [SerializeField] private ParticleSystem[] pillarCrumble = new ParticleSystem[2];

        [Header("Timing")]
        [Tooltip("가시가 솟아 대상에 닿는 시간(초). 유닛 밀려남 지연(UnitsView.knockbackDelay)과 맞춘다.")]
        [SerializeField, Min(0.01f)] private float riseTime = 0.15f;
        [Tooltip("대상이 칸 하나를 지나는 시간(초). UnitsView.knockbackCellDuration 과 맞춘다.")]
        [SerializeField, Min(0.01f)] private float cellTime = 0.1f;
        [Tooltip("대상이 칸을 떠난 뒤 그 칸 기둥이 솟기 시작하기까지(초).")]
        [SerializeField, Min(0f)] private float pillarLag = 0.04f;
        [SerializeField, Min(0.01f)] private float pillarRise = 0.09f;
        [Tooltip("다 솟은 뒤 부서지기 전까지 머무는 시간(초).")]
        [SerializeField, Min(0f)] private float hold = 0.35f;
        [SerializeField, Min(0.01f)] private float crumbleTime = 0.25f;

        private Vector3 _spikeScale;
        private Vector3[] _pillarScales;
        private float _elapsed;
        private bool _playing;
        private bool _impacted;
        private bool _crumbled;
        private int _pillarsStarted;
        private int _pillarCount;

        protected override bool PlayParticlesOnSpawn => false;

        protected override bool IsFinished => !_playing && !IsAnyParticleAlive();

        private float CrumbleStart => PushedCells > 0
            ? riseTime + PushedCells * cellTime + pillarLag + pillarRise + hold
            : riseTime + hold * 0.5f;

        private void Awake()
        {
            _spikeScale = spike.localScale;
            _pillarScales = new Vector3[pillars.Length];
            for (int i = 0; i < pillars.Length; i++)
                _pillarScales[i] = pillars[i].localScale;
        }

        protected override void OnPlaced()
        {
            spike.localPosition = Vector3.back * spikeBack;
            spikeDust.transform.localPosition = spike.localPosition;
            spikeCrumble.transform.localPosition = spike.localPosition;
            _pillarCount = usePillars ? Mathf.Min(PushedCells, pillars.Length) : 0;
            for (int i = 0; i < pillars.Length; i++)
            {
                Vector3 position = Vector3.forward * (CellSpacing * i);
                pillars[i].localPosition = position;
                pillarDust[i].transform.localPosition = position;
                pillarCrumble[i].transform.localPosition = position;
                pillars[i].gameObject.SetActive(false);
            }

            _elapsed = 0f;
            _playing = true;
            _impacted = false;
            _crumbled = false;
            _pillarsStarted = 0;
            spikeDust.Play(true);
            Apply();
        }

        protected override void OnReleased()
        {
            _playing = false;
        }

        protected override void Update()
        {
            if (_playing)
            {
                _elapsed += Time.deltaTime;
                Apply();
            }

            base.Update();
        }

        private void Apply()
        {
            if (!_impacted && _elapsed >= riseTime)
            {
                _impacted = true;
                impact.Play(true);
            }

            while (_pillarsStarted < _pillarCount && _elapsed >= PillarStart(_pillarsStarted))
            {
                pillars[_pillarsStarted].gameObject.SetActive(true);
                pillarDust[_pillarsStarted].Play(true);
                _pillarsStarted++;
            }

            float crumble = Mathf.Clamp01((_elapsed - CrumbleStart) / crumbleTime);
            if (!_crumbled && crumble > 0f)
            {
                _crumbled = true;
                spikeCrumble.Play(true);
                for (int i = 0; i < _pillarCount; i++)
                    pillarCrumble[i].Play(true);
            }

            SetHeight(spike, _spikeScale, Overshoot(_elapsed / riseTime), crumble);
            for (int i = 0; i < _pillarsStarted; i++)
                SetHeight(pillars[i], _pillarScales[i], Overshoot((_elapsed - PillarStart(i)) / pillarRise), crumble);

            if (crumble >= 1f)
            {
                _playing = false;
                spike.localScale = Vector3.zero;
                foreach (Transform pillar in pillars)
                    pillar.gameObject.SetActive(false);
            }
        }

        /// <summary>index 번째 칸(0 = 대상이 있던 칸) 기둥이 솟기 시작하는 시각: 대상이 그 칸을 반쯤 지나 떠날 때.</summary>
        private float PillarStart(int index) => riseTime + (index + 0.5f) * cellTime + pillarLag;

        // 솟는 높이(0 → 1, 끝에 살짝 넘쳤다 돌아옴)와 부서짐(높이가 줄며 바닥으로 가라앉음).
        private static void SetHeight(Transform piece, Vector3 scale, float rise, float crumble)
        {
            float height = rise * (1f - crumble * crumble);
            float width = 1f - crumble * 0.3f;
            piece.localScale = new Vector3(scale.x * width, scale.y * Mathf.Max(height, 0.0001f), scale.z * width);
        }

        private static float Overshoot(float t)
        {
            t = Mathf.Clamp01(t);
            const float S = 1.8f;
            float u = t - 1f;
            return 1f + u * u * ((S + 1f) * u + S);
        }
    }
}
