// 웹 빌드용 브라우저 WebSocket 브리지(ServerConnection 이 쓴다).
// 소켓마다 받은 메시지를 큐에 쌓아 두고, C# 이 매 프레임 꺼내 간다(콜백으로 C# 를 부르지 않는다).
// 상태: 0 접속 중, 1 열림, 2 닫힘.
var WebSocketBridge = {
  $sockets: {},
  $nextSocketId: 1,

  WebSocketBridge_Open: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    var id = nextSocketId++;
    var entry = { state: 0, queue: [], error: null };
    sockets[id] = entry;
    try {
      var socket = new WebSocket(url);
      entry.socket = socket;
      socket.onopen = function () { entry.state = 1; };
      socket.onmessage = function (e) { if (typeof e.data === 'string') entry.queue.push(e.data); };
      socket.onerror = function () { entry.error = entry.error || '연결 오류'; };
      socket.onclose = function (e) {
        entry.state = 2;
        if (!e.wasClean && !entry.error) entry.error = '연결이 끊김(' + e.code + ')';
      };
    } catch (e) {
      entry.state = 2;
      entry.error = String(e && e.message ? e.message : e);
    }
    return id;
  },

  WebSocketBridge_State: function (id) {
    var entry = sockets[id];
    return entry ? entry.state : 2;
  },

  WebSocketBridge_Send: function (id, textPtr) {
    var entry = sockets[id];
    if (!entry || entry.state !== 1) return 0;
    entry.socket.send(UTF8ToString(textPtr));
    return 1;
  },

  // 다음 메시지(없으면 null). 반환한 문자열 메모리는 C# 마샬러가 해제한다.
  WebSocketBridge_Receive: function (id) {
    var entry = sockets[id];
    if (!entry || entry.queue.length === 0) return null;
    var text = entry.queue.shift();
    var size = lengthBytesUTF8(text) + 1;
    var buffer = _malloc(size);
    stringToUTF8(text, buffer, size);
    return buffer;
  },

  WebSocketBridge_Error: function (id) {
    var entry = sockets[id];
    if (!entry || !entry.error) return null;
    var size = lengthBytesUTF8(entry.error) + 1;
    var buffer = _malloc(size);
    stringToUTF8(entry.error, buffer, size);
    return buffer;
  },

  WebSocketBridge_Close: function (id) {
    var entry = sockets[id];
    if (!entry) return;
    try { if (entry.socket && entry.state !== 2) entry.socket.close(); } catch (e) {}
    delete sockets[id];
  },

  // 페이지 주소의 ?name= 값(없으면 null). 웹 빌드의 기본 서버 주소용.
  WebSocketBridge_QueryParam: function (namePtr) {
    var value = new URLSearchParams(window.location.search).get(UTF8ToString(namePtr));
    if (value === null) return null;
    var size = lengthBytesUTF8(value) + 1;
    var buffer = _malloc(size);
    stringToUTF8(value, buffer, size);
    return buffer;
  },
};

autoAddDeps(WebSocketBridge, '$sockets');
autoAddDeps(WebSocketBridge, '$nextSocketId');
mergeInto(LibraryManager.library, WebSocketBridge);
