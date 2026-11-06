using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MiniChess.Core.Common;
using MiniChess.Core.Controller;
using MiniChess.Core.Snapshots;

namespace MiniChess.Bot.Search
{
    /// <summary>
    /// 판의 64비트 해시. 행동 순서만 다르고 결과가 같은 판을 한 번만 읽는 데 쓴다.
    /// Core 의 스냅샷(GameController.GetSnapshot)을 리플렉션으로 통째로 훑어 해시하므로, Core 에 새 메커닉이 생겨
    /// 스냅샷에 값이 늘어도 이 코드를 고칠 필요가 없다(화면에 보여야 하는 상태는 모두 스냅샷에 들어간다).
    /// 이벤트 순번처럼 같은 판이라도 순서에 따라 달라지는 값은 뺀다. Id 처럼 만든 순서에 따라 달라지는 값 때문에
    /// 같은 판이 다른 해시가 될 수는 있지만(덜 합칠 뿐), 다른 판이 같은 해시가 되지는 않는다(64비트 충돌 제외).
    /// </summary>
    internal static class StateHasher
    {
        /// <summary>같은 판이라도 도달 순서에 따라 달라져 해시에서 빼는 스냅샷 멤버.</summary>
        private static readonly HashSet<string> Ignored = new HashSet<string>
        {
            nameof(GameSnapshot.LastEventSequence),
        };

        private static readonly Dictionary<Type, Func<object, object>[]> Members = new Dictionary<Type, Func<object, object>[]>();

        /// <summary>스냅샷 안의 객체 중첩 상한. 넘으면 순환 참조로 보고 예외(해시가 조용히 틀리지 않게).</summary>
        private const int MaxDepth = 32;

        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong Hash(GameController game, Team viewer)
        {
            long started = Engine.SearchProfile.Begin();
            object snapshot = game.GetSnapshot(viewer);
            Engine.SearchProfile.End(Engine.ProfileStep.Snapshot, started);

            started = Engine.SearchProfile.Begin();
            ulong hash = Offset;
            Add(ref hash, snapshot, 0);
            Engine.SearchProfile.End(Engine.ProfileStep.Hash, started);
            return hash;
        }

        private static void Add(ref ulong hash, object value, int depth)
        {
            if (depth > MaxDepth)
                throw new InvalidOperationException($"스냅샷 중첩이 {MaxDepth} 단계를 넘었다(순환 참조?): {value?.GetType()}");

            switch (value)
            {
                case null:
                    Mix(ref hash, 0x9E3779B97F4A7C15UL);
                    return;
                case string text:
                    Mix(ref hash, (ulong)text.Length);
                    foreach (char c in text)
                        Mix(ref hash, c);
                    return;
                case bool flag:
                    Mix(ref hash, flag ? 1UL : 2UL);
                    return;
                case int number:
                    Mix(ref hash, (ulong)(long)number);
                    return;
                case Enum enumValue:
                    Mix(ref hash, (ulong)Convert.ToInt64(enumValue));
                    return;
                case IDictionary dictionary:
                {
                    // 사전은 항목 순서가 보장되지 않으므로 순서에 무관하게 합친다.
                    ulong combined = 0;
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        ulong item = Offset;
                        Add(ref item, entry.Key, depth + 1);
                        Add(ref item, entry.Value, depth + 1);
                        combined += item * 0xBF58476D1CE4E5B9UL;
                    }

                    Mix(ref hash, (ulong)dictionary.Count);
                    Mix(ref hash, combined);
                    return;
                }
                case IEnumerable sequence:
                {
                    ulong count = 0;
                    foreach (object item in sequence)
                    {
                        Add(ref hash, item, depth + 1);
                        count++;
                    }

                    Mix(ref hash, count);
                    return;
                }
            }

            Type type = value.GetType();
            if (type.IsPrimitive)
            {
                Mix(ref hash, (ulong)Convert.ToInt64(value));
                return;
            }

            foreach (Func<object, object> member in MembersOf(type))
                Add(ref hash, member(value), depth + 1);
        }

        /// <summary>공개 인스턴스 속성과 필드(Position 처럼 필드로 값을 두는 타입이 있다), 이름 순.</summary>
        private static Func<object, object>[] MembersOf(Type type)
        {
            lock (Members)
            {
                if (Members.TryGetValue(type, out Func<object, object>[] cached))
                    return cached;

                var list = new List<(string Name, Func<object, object> Get)>();
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (property.GetIndexParameters().Length == 0 && !Ignored.Contains(property.Name))
                        list.Add((property.Name, property.GetValue));
                }

                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!Ignored.Contains(field.Name))
                        list.Add((field.Name, field.GetValue));
                }

                if (list.Count == 0)
                    throw new InvalidOperationException($"해시할 공개 멤버가 없는 타입: {type}");

                // 리플렉션이 돌려주는 순서는 보장되지 않으므로 이름 순으로 고정한다.
                list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
                Func<object, object>[] members = list.ConvertAll(member => member.Get).ToArray();
                Members[type] = members;
                return members;
            }
        }

        private static void Mix(ref ulong hash, ulong value)
        {
            hash ^= value;
            hash *= Prime;
            hash ^= hash >> 29;
        }
    }
}
