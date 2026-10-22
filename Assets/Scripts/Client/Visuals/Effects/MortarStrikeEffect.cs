using System.Collections.Generic;
using UnityEngine;

namespace MiniChess.Client.Visuals.Effects
{
    /// <summary>
    /// 박격포 착탄(FX_MortarStrike). 범위 중심 위에서 포탄이 떨어지고, impactTime 에 중심 큰 폭발 + 범위 각 칸 작은 불꽃이 터진다.
    /// 낙하 포탄 파티클은 impactTime 동안 바닥에 닿도록 높이/속도/수명을 맞춰 둔다. 폭발 시점은 이 스크립트가 정한다.
    /// </summary>
    public sealed class MortarStrikeEffect : StrikeEffect
    {
        [Tooltip("낙하 포탄(꼬리 포함). 꺼낼 때 재생한다.")]
        [SerializeField] private ParticleSystem shell;
        [Tooltip("중심 큰 폭발의 루트(하위 파티클 포함). 착탄 때 재생한다.")]
        [SerializeField] private ParticleSystem explosion;
        [Tooltip("칸별 작은 불꽃. World 시뮬레이션, 자체 방출 없음. 착탄 때 각 칸 위치에서 직접 방출한다.")]
        [SerializeField] private ParticleSystem cellBurst;

        [Tooltip("꺼낸 뒤 포탄이 바닥에 닿기까지 시간(초).")]
        [SerializeField, Min(0f)] private float impactTime = 0.45f;
        [Tooltip("칸 하나에서 방출하는 불꽃 입자 수.")]
        [SerializeField, Min(1)] private int cellBurstCount = 14;

        private readonly List<Vector3> _cells = new List<Vector3>();
        private float _impactAt;
        private bool _impactPending;

        protected override bool PlayParticlesOnSpawn => false;

        protected override bool IsFinished => !_impactPending && !IsAnyParticleAlive();

        protected override void OnSpawned()
        {
            _impactAt = Time.time + impactTime;
            _impactPending = true;
            if (shell != null)
                shell.Play(true);
        }

        protected override void OnPlaced(IReadOnlyList<Vector3> cells)
        {
            _cells.Clear();
            if (cells != null)
                _cells.AddRange(cells);
        }

        protected override void OnReleased()
        {
            _impactPending = false;
        }

        protected override void Update()
        {
            if (_impactPending && Time.time >= _impactAt)
                Impact();

            base.Update();
        }

        private void Impact()
        {
            _impactPending = false;
            if (explosion != null)
                explosion.Play(true);

            if (cellBurst == null)
                return;

            cellBurst.Play(false);
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            foreach (Vector3 cell in _cells)
            {
                emit.position = cell;
                cellBurst.Emit(emit, cellBurstCount);
            }
        }
    }
}
