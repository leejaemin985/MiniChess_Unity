using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 연속 베기 연출(FX_AssassinChainStrike). UnitsView 가 순간이동 단계(사라짐/나타남)에 맞춰 부른다.
    ///   - smoke: 몸 연기(bodySmoke)와 펑(puff)의 부모. 시전자를 따라다닌다(파티클은 World 라 지나간 자리에 남는다).
    ///     몸 연기는 보이는 동안만 뿜고, 사라질 때와 나타날 때 펑을 한 번씩 터뜨린다.
    ///   - cuts: 대상 위 베기 원호들. 차례로 돌려 쓰며, 대상 자리(+ cutHeight)에서 카메라 → 대상 시선 축으로 cutTilt 만큼 기울인다.
    ///     번갈아 좌우를 뒤집어(거울상) 대각선 방향이 대상마다 바뀐다(원호가 한쪽이 끊긴 호라 각도만 뒤집으면 가파르게 보인다).
    /// 루트는 꺼낸 자리에 그대로 둔다(베기 원호는 Local 이라 루트가 움직이면 따라 움직인다). 끝나면 Finish 로 방출을 멈추고 풀로 돌아간다.
    /// </summary>
    public sealed class ChainStrikeEffect : EffectBase
    {
        [SerializeField] private Transform smoke;
        [SerializeField] private ParticleSystem bodySmoke;
        [SerializeField] private ParticleSystem puff;
        [Tooltip("펑 한 번에 내는 연기 수.")]
        [SerializeField, Min(1)] private int puffCount = 14;
        [SerializeField] private Transform[] cuts;
        [Tooltip("베기 원호를 화면에서 대각선으로 기울이는 각도(도).")]
        [SerializeField, Range(0f, 80f)] private float cutTilt = 35f;
        [Tooltip("발밑에서 베기 원호 중심까지 높이(몸통).")]
        [SerializeField] private float cutHeight = 0.55f;

        private Transform _follow;
        private int _cutIndex;

        protected override bool PlayParticlesOnSpawn => false;

        /// <summary>caster 를 따라다니며 몸 연기를 뿜기 시작한다.</summary>
        public void Begin(Transform caster)
        {
            _follow = caster;
            _cutIndex = 0;
            FollowNow();
            if (puff != null)
                puff.Play(false);
            SetSmoke(true);
        }

        /// <summary>몸 연기를 켜고 끈다(숨어 있는 동안 끈다).</summary>
        public void SetSmoke(bool on)
        {
            if (bodySmoke == null || IsReleasing)
                return;
            if (on)
                bodySmoke.Play(false);
            else
                bodySmoke.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>지금 몸 자리에서 연기를 한 번 터뜨린다(사라질 때 / 나타날 때).</summary>
        public void Puff()
        {
            FollowNow();
            if (puff != null)
                puff.Emit(puffCount);
        }

        /// <summary>ground(대상 칸 윗면 중심, 월드)에서 베기 원호를 한 번 그린다.</summary>
        public void Cut(Vector3 ground)
        {
            if (cuts == null || cuts.Length == 0)
                return;

            int index = _cutIndex++;
            Transform cut = cuts[index % cuts.Length];
            if (cut == null)
                return;

            Vector3 forward = Vector3.forward;
            Camera cam = Camera.main;
            if (cam != null)
            {
                forward = ground - cam.transform.position;
                forward.y = 0f;
                if (forward.sqrMagnitude < 1e-4f)
                    forward = Vector3.forward;
            }

            cut.SetPositionAndRotation(ground + Vector3.up * cutHeight, Quaternion.LookRotation(forward) * Quaternion.Euler(0f, 0f, cutTilt));
            cut.localScale = new Vector3(index % 2 == 0 ? 1f : -1f, 1f, 1f);
            foreach (ParticleSystem particle in cut.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Clear(false);
                particle.Play(false);
            }
        }

        /// <summary>몸 연기를 멈추고, 남은 연기와 베기가 사라지면 풀로 돌아간다.</summary>
        public void Finish()
        {
            _follow = null;
            Release();
        }

        protected override void OnReleased()
        {
            _follow = null;
        }

        private void LateUpdate()
        {
            FollowNow();
        }

        private void FollowNow()
        {
            if (_follow != null && smoke != null)
                smoke.position = _follow.position;
        }
    }
}
