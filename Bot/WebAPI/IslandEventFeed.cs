using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SysBot.ACNHOrders.WebAPI
{
    public class IslandEventInfo
    {
        public long Id { get; set; }
        public string Timestamp { get; set; } = string.Empty; // UTC ISO 8601
        public string LocalTime { get; set; } = string.Empty; // yyyy-MM-dd hh:mm:ss tt
        public string Type { get; set; } = string.Empty;      // "arrival", "departure", "dodo", "crash", "layer", "turnips", "order", "echo"
        public string Message { get; set; } = string.Empty;
        public string? Player { get; set; }
        public string? Island { get; set; }
        public string? Town { get; set; }
        public string? DodoCode { get; set; }
        public ulong? NetworkId { get; set; }
    }

    public class LastArrivalInfo
    {
        public string Player { get; set; } = string.Empty;
        public string Island { get; set; } = string.Empty;
        public string Town { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string LocalTime { get; set; } = string.Empty;
    }

    public class LastDepartureInfo
    {
        public string Player { get; set; } = string.Empty;
        public string Town { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string LocalTime { get; set; } = string.Empty;
    }

    /// <summary>
    /// Thread-safe circular event feed recording all live island activity.
    /// Captures arrivals, departures, dodo refreshes, crashes, and layers
    /// so external clients can consume them via WebAPI without Discord.
    /// </summary>
    public sealed class IslandEventFeed
    {
        private static IslandEventFeed? _instance;
        public static IslandEventFeed Instance => _instance ??= new IslandEventFeed();

        private const int MaxEvents = 200;
        private readonly object _lock = new();
        private readonly LinkedList<IslandEventInfo> _events = new();
        private long _counter = 0;

        public LastArrivalInfo? LastArrival { get; private set; }
        public LastDepartureInfo? LastDeparture { get; private set; }
        public long TotalCount => Interlocked.Read(ref _counter);

        private IslandEventFeed() { }

        public IslandEventInfo RecordEvent(
            string type,
            string message,
            string? player = null,
            string? island = null,
            string? town = null,
            string? dodoCode = null,
            ulong? nid = null)
        {
            var nowUtc = DateTime.UtcNow;
            var nowLocal = DateTime.Now;
            var id = Interlocked.Increment(ref _counter);

            var cleanMsg = message.TrimStart('>', ' ').Trim();

            var evt = new IslandEventInfo
            {
                Id = id,
                Timestamp = nowUtc.ToString("o"),
                LocalTime = nowLocal.ToString("yyyy-MM-dd hh:mm:ss tt"),
                Type = type,
                Message = cleanMsg,
                Player = player,
                Island = island,
                Town = town,
                DodoCode = dodoCode,
                NetworkId = nid
            };

            lock (_lock)
            {
                // Prevent duplicate consecutive entries with identical message within 2 seconds
                if (_events.Last != null && _events.Last.Value.Message == cleanMsg)
                {
                    return _events.Last.Value;
                }

                _events.AddLast(evt);
                while (_events.Count > MaxEvents)
                {
                    _events.RemoveFirst();
                }

                if (type == "arrival" && !string.IsNullOrWhiteSpace(player))
                {
                    LastArrival = new LastArrivalInfo
                    {
                        Player = player,
                        Island = island ?? string.Empty,
                        Town = town ?? string.Empty,
                        Timestamp = evt.Timestamp,
                        LocalTime = evt.LocalTime
                    };
                }
                else if (type == "departure" && !string.IsNullOrWhiteSpace(player))
                {
                    LastDeparture = new LastDepartureInfo
                    {
                        Player = player,
                        Town = town ?? string.Empty,
                        Timestamp = evt.Timestamp,
                        LocalTime = evt.LocalTime
                    };
                }
            }

            return evt;
        }

        public IslandEventInfo RecordArrival(string player, string island, string town, string? dodoCode, string message, ulong? nid = null)
        {
            return RecordEvent("arrival", message, player: player, island: island, town: town, dodoCode: dodoCode, nid: nid);
        }

        public IslandEventInfo RecordDeparture(string player, string town, string message)
        {
            return RecordEvent("departure", message, player: player, town: town);
        }

        public IslandEventInfo RecordDodoUpdate(string dodoCode, string town, string message)
        {
            return RecordEvent("dodo", message, town: town, dodoCode: dodoCode);
        }

        public IslandEventInfo RecordCrash(string town, string message)
        {
            return RecordEvent("crash", message, town: town);
        }

        public IslandEventInfo RecordLayerChange(string layerName, string town, string message)
        {
            return RecordEvent("layer", message, town: town);
        }

        public IslandEventInfo RecordEcho(string message)
        {
            // Classify message type if possible
            string type = "echo";
            if (message.Contains("🛫") || message.Contains("has departed", StringComparison.OrdinalIgnoreCase))
                type = "departure";
            else if (message.Contains("🛬") || message.Contains("is joining", StringComparison.OrdinalIgnoreCase))
                type = "arrival";
            else if (message.Contains("Crash detected", StringComparison.OrdinalIgnoreCase))
                type = "crash";
            else if (message.Contains("Dodo code", StringComparison.OrdinalIgnoreCase))
                type = "dodo";
            else if (message.Contains("item layer", StringComparison.OrdinalIgnoreCase) || message.Contains("loaded with layer", StringComparison.OrdinalIgnoreCase))
                type = "layer";

            return RecordEvent(type, message);
        }

        public IReadOnlyList<IslandEventInfo> GetRecent(int count = 10)
        {
            lock (_lock)
            {
                return _events.Reverse().Take(Math.Max(1, Math.Min(count, MaxEvents))).ToList();
            }
        }

        public IReadOnlyList<IslandEventInfo> GetEvents(int limit = 50, long? sinceId = null, string? type = null)
        {
            lock (_lock)
            {
                IEnumerable<IslandEventInfo> query = _events;

                if (sinceId.HasValue && sinceId.Value > 0)
                {
                    query = query.Where(e => e.Id > sinceId.Value);
                }

                if (!string.IsNullOrWhiteSpace(type))
                {
                    query = query.Where(e => string.Equals(e.Type, type, StringComparison.OrdinalIgnoreCase));
                }

                return query.Reverse().Take(Math.Max(1, Math.Min(limit, MaxEvents))).ToList();
            }
        }
    }
}
