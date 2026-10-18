using System;
using MiniChess.Core.Common;
using MiniChess.Core.Events;
using MiniChess.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace MiniChess.Client.Network
{
    /// <summary>
    /// 서버와 같은 메시지 JSON 형식(서버 ProtocolJson 과 짝): { "Type": "Move", "Payload": { ... } },
    /// enum 은 이름 문자열, Position 은 { "X": 0, "Y": 0 }.
    /// </summary>
    public static class ClientProtocolJson
    {
        private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            Converters = { new PositionConverter(), new StringEnumConverter() },
        });

        public static string Encode(ClientMessage message)
        {
            var envelope = new JObject
            {
                ["Type"] = MessageTypes.NameOf(message.GetType()),
                ["Payload"] = JObject.FromObject(message, Serializer),
            };
            return envelope.ToString(Formatting.None);
        }

        /// <summary>서버 메시지를 읽는다. 모르는 종류이거나 형식이 잘못되었으면 null.</summary>
        public static ServerMessage DecodeServer(string json)
        {
            try
            {
                JObject envelope = JObject.Parse(json);
                Type type = MessageTypes.Find((string)envelope["Type"]);
                if (type == null || !typeof(ServerMessage).IsAssignableFrom(type) || !(envelope["Payload"] is JObject payload))
                    return null;

                return (ServerMessage)payload.ToObject(type, Serializer);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>CommandAppliedMessage 의 명령 본문을 명령 메시지로 읽는다. 읽을 수 없으면 null.</summary>
        public static CommandMessage ReadCommand(CommandAppliedMessage applied)
        {
            Type type = MessageTypes.Find(applied.CommandType);
            if (type == null || !typeof(CommandMessage).IsAssignableFrom(type) || !(applied.Command is JObject body))
                return null;

            return (CommandMessage)body.ToObject(type, Serializer);
        }

        /// <summary>이벤트 봉투의 본문을 코어 이벤트로 읽는다. 읽을 수 없으면 null.</summary>
        public static GameEvent ReadEvent(EventEnvelope envelope)
        {
            Type type = EventTypes.Find(envelope.Type);
            if (type == null || !(envelope.Payload is JObject body))
                return null;

            return (GameEvent)body.ToObject(type, Serializer);
        }

        /// <summary>Position(읽기 전용 구조체)과 Position? 를 { "X", "Y" } 로 쓰고 읽는다.</summary>
        private sealed class PositionConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(Position) || objectType == typeof(Position?);

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                    return null;

                JObject value = JObject.Load(reader);
                return new Position((int)value["X"], (int)value["Y"]);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }

                var position = (Position)value;
                writer.WriteStartObject();
                writer.WritePropertyName("X");
                writer.WriteValue(position.X);
                writer.WritePropertyName("Y");
                writer.WriteValue(position.Y);
                writer.WriteEndObject();
            }
        }
    }
}
