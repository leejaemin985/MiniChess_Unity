using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MiniChess.Protocol;

namespace MiniChess.Client.Network
{
    /// <summary>
    /// 서버 WebSocket 연결. 받기는 백그라운드에서 하고, 받은 메시지는 큐에 쌓아 메인 스레드(GameSession.Update)가 TryReceive 로 꺼낸다.
    /// Unity API 는 메인 스레드에서만 쓸 수 있으므로 이 클래스는 Unity 객체를 건드리지 않는다.
    /// </summary>
    public sealed class ServerConnection : IDisposable
    {
        private readonly ClientWebSocket _socket = new ClientWebSocket();
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        private readonly ConcurrentQueue<string> _received = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        /// <summary>연결이 끊겼는지(서버가 닫았거나 오류). 끊긴 뒤에도 이미 받은 메시지는 꺼낼 수 있다.</summary>
        public bool IsClosed { get; private set; }

        /// <summary>끊긴 이유(오류일 때). 정상 종료면 null.</summary>
        public string CloseReason { get; private set; }

        /// <summary>접속하고 받기를 시작한다. 실패하면 IsClosed 가 true 가 된다.</summary>
        public async Task ConnectAsync(Uri uri)
        {
            try
            {
                await _socket.ConnectAsync(uri, _cancel.Token);
                _ = Task.Run(ReceiveLoop);
            }
            catch (Exception e) when (e is WebSocketException || e is OperationCanceledException || e is InvalidOperationException)
            {
                Close(e.Message);
            }
        }

        public async Task SendAsync(ClientMessage message)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(ClientProtocolJson.Encode(message));

            await _sendLock.WaitAsync();
            try
            {
                if (_socket.State == WebSocketState.Open)
                    await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cancel.Token);
            }
            catch (Exception e) when (e is WebSocketException || e is OperationCanceledException)
            {
                Close(e.Message);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <summary>받은 메시지 하나. 없거나 해석할 수 없으면 false(해석 실패는 message 가 null 인 true).</summary>
        public bool TryReceive(out ServerMessage message)
        {
            if (!_received.TryDequeue(out string json))
            {
                message = null;
                return false;
            }

            message = ClientProtocolJson.DecodeServer(json);
            return true;
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[16 * 1024];
            var builder = new StringBuilder();

            try
            {
                while (_socket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancel.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    if (!result.EndOfMessage)
                        continue;

                    _received.Enqueue(builder.ToString());
                    builder.Clear();
                }

                Close(null);
            }
            catch (Exception e) when (e is WebSocketException || e is OperationCanceledException || e is ObjectDisposedException)
            {
                Close(e.Message);
            }
        }

        private void Close(string reason)
        {
            if (IsClosed)
                return;

            CloseReason = reason;
            IsClosed = true;
        }

        public void Dispose()
        {
            Close(null);
            _cancel.Cancel();
            _socket.Dispose();
        }
    }
}
