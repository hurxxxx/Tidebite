#nullable disable
using System;

namespace Tidebite.Core
{
    public static class OnlineProtocol { public const int Version=11; }
    [Serializable] public sealed class MatchRequest { public int protocol; public string mode="arena"; }
    [Serializable] public sealed class MatchReply
    {
        public string status,roomId,endpoint,ticket,reservationId,message;
        public long expiresAt;
    }
    [Serializable] public sealed class RoomHeartbeat
    {
        public int protocol,humans,capacity;
        public string mode="arena";
        public bool accepting;
        public string[] admissions;
    }
}
