using System;
using System.Collections.Concurrent;
using System.IO;
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
    ///   - 형식은 서버와 같은 ProtocolJson 을 쓴다.
    ///   - 연결 확인: ProtocolInfo.PingIntervalSeconds 마다 Ping 을 보내고(서버는 Pong 으로 답한다),
    ///     ProtocolInfo.ConnectionTimeoutSeconds 동안 아무 메시지도 받지 못하면 끊긴 것으로 본다.
    /// </summary>
    public sealed class ServerConnection : IDisposable
    {
        private const int ConnectTimeoutSeconds = 10;

        private readonly ClientWebSocket _socket = new ClientWebSocket();
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        private readonly ConcurrentQueue<string> _received = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        /// <summary>연결이 끊겼는지(서버가 닫았거나 오류). 끊긴 뒤에도 이미 받은 메시지는 꺼낼 수 있다.</summary>
        public bool IsClosed { get; private set; }

        /// <summary>끊긴 이유(오류일 때). 정상 종료면 null.</summary>
        public string CloseReason { get; private set; }

        /// <summary>접속하고 받기와 연결 확인을 시작한다. 실패하면(제한 시간 초과 포함) IsClosed 가 true 가 된다.</summary>
        public async Task ConnectAsync(Uri uri)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cancel.Token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(ConnectTimeoutSeconds));
                try
                {
                    await _socket.ConnectAsync(uri, timeout.Token);
                }
                catch (OperationCanceledException) when (!_cancel.IsCancellationRequested)
                {
                    Close("접속 시간 초과");
                    return;
                }
                catch (Exception e) when (e is WebSocketException || e is OperationCanceledException || e is InvalidOperationException || e is ObjectDisposedException)
                {
                    Close(e.Message);
                    return;
                }
            }

            _ = Task.Run(ReceiveLoop);
            _ = Task.Run(PingLoop);
        }

        public async Task SendAsync(ClientMessage message)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(ProtocolJson.Encode(message));

            await _sendLock.WaitAsync();
            try
            {
                if (_socket.State == WebSocketState.Open)
                    await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cancel.Token);
            }
            catch (Exception e) when (e is WebSocketException || e is OperationCanceledException || e is ObjectDisposedException)
            {
                Close(e.Message);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <summary>받은 메시지 하나. 없으면 false. 해석할 수 없으면 message 가 null 인 true.</summary>
        public bool TryReceive(out ServerMessage message)
        {
            if (!_received.TryDequeue(out string json))
            {
                message = null;
                return false;
            }

            message = ProtocolJson.DecodeServer(json);
            return true;
        }

        /// <summary>
        /// 메시지를 받아 큐에 넣는다. 조각은 바이트로 모았다가 메시지가 끝나면 한 번에 UTF-8 로 읽는다(한글이 조각 경계에서 잘려도 깨지지 않게).
        /// </summary>
        private async Task ReceiveLoop()
        {
            var buffer = new byte[16 * 1024];
            var message = new MemoryStream();

            try
            {
                while (_socket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result;
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cancel.Token))
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(ProtocolInfo.ConnectionTimeoutSeconds));
                        try
                        {
                            result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
                        }
                        catch (OperationCanceledException) when (!_cancel.IsCancellationRequested)
                        {
                            Close("서버 응답 없음");
                            _socket.Abort();
                            return;
                        }
                    }

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    message.Write(buffer, 0, result.Count);
                    if (message.Length > ProtocolInfo.MaxMessageBytes)
                    {
                        Close("서버 메시지가 너무 큼");
                        _socket.Abort();
                        return;
                    }

                    if (!result.EndOfMessage)
                        continue;

                    _received.Enqueue(Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length));
                    message.SetLength(0);
                }

                Close(null);
            }
            catch (Exception e) when (e is WebSocketException || e is OperationCanceledException || e is ObjectDisposedException)
            {
                Close(e.Message);
            }
        }

        /// <summary>연결이 열려 있는 동안 주기적으로 Ping 을 보낸다(서버가 이 연결을 살아 있다고 보게 하고, Pong 으로 서버가 살아 있는지 안다).</summary>
        private async Task PingLoop()
        {
            try
            {
                while (!IsClosed && _socket.State == WebSocketState.Open)
                {
                    await Task.Delay(TimeSpan.FromSeconds(ProtocolInfo.PingIntervalSeconds), _cancel.Token);
                    await SendAsync(new PingMessage());
                }
            }
            catch (OperationCanceledException)
            {
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
