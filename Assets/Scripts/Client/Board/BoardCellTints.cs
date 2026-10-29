using MiniChess.Core.Common;
using UnityEngine;

namespace MiniChess.Client.Board
{
    /// <summary>
    /// 칸별 색(지형 색조 + 장판 덧칠)을 보드 크기의 작은 텍스처 하나에 담아 돌 셰이더(StoneSurface)가 칸 좌표로 읽게 한다.
    /// 렌더러마다 MaterialPropertyBlock 을 걸면 SRP Batcher 가 꺼지므로, 판석/벽은 머티리얼을 공유하고 색만 여기서 바꾼다.
    /// 텍스처 크기 W × 2H: 아래 절반(y = 0 ~ H-1) = 지형 색조(곱), 위 절반(y = H ~ 2H-1) = 덧칠(rgb, a = 세기).
    /// 바뀐 프레임에만 GPU 로 올린다(Apply). 보드는 하나뿐이라 셰이더 전역 값으로 연결한다.
    /// </summary>
    internal sealed class BoardCellTints
    {
        private static readonly int DataId = Shader.PropertyToID("_BoardCellData");
        private static readonly int CountId = Shader.PropertyToID("_BoardCellCount");

        private readonly int _width;
        private readonly int _height;
        private readonly Texture2D _texture;
        private readonly Color32[] _pixels;
        private bool _dirty;

        public BoardCellTints(int width, int height)
        {
            _width = width;
            _height = height;
            _texture = new Texture2D(width, height * 2, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                name = "BoardCellTints",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            _pixels = new Color32[width * height * 2];
            for (int i = 0; i < width * height; i++)
                _pixels[i] = new Color32(255, 255, 255, 255);
            _dirty = true;

            Shader.SetGlobalTexture(DataId, _texture);
            Shader.SetGlobalVector(CountId, new Vector4(width, height, 0f, 0f));
        }

        public void Set(Position position, Color terrain, Color overlay)
        {
            if (position.X < 0 || position.X >= _width || position.Y < 0 || position.Y >= _height)
                return;

            int index = position.Y * _width + position.X;
            _pixels[index] = terrain;
            _pixels[index + _width * _height] = overlay;
            _dirty = true;
        }

        /// <summary>바뀐 색을 GPU 로 올린다(한 프레임에 한 번).</summary>
        public void Apply()
        {
            if (!_dirty)
                return;

            _texture.SetPixels32(_pixels);
            _texture.Apply(updateMipmaps: false);
            _dirty = false;
        }

        public void Dispose()
        {
            Shader.SetGlobalVector(CountId, Vector4.zero);
            Object.Destroy(_texture);
        }
    }
}
