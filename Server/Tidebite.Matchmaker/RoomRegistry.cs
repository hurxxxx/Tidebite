using System.Text;
using Tidebite.Core;

namespace Tidebite.Matchmaker;

public sealed class RoomDefinition
{
    public string id="",endpoint="",key="";
}
public sealed class RoomRegistry
{
    private sealed class Room
    {
        public required RoomDefinition Definition;
        public RoomHeartbeat? Heartbeat;
        public long Seen;
    }
    private sealed record Reservation(string Room,string Ticket,long Expires);
    private readonly object sync=new();
    private readonly Dictionary<string,Room> rooms=new();
    private readonly Dictionary<string,Reservation> reservations=new();
    private readonly Func<long> clock;
    public RoomRegistry(IEnumerable<RoomDefinition> definitions,Func<long>? clock=null)
    {
        this.clock=clock??(()=>DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        foreach(var d in definitions)
        {
            if(!AdmissionTickets.ValidRoom(d.id)||Encoding.UTF8.GetByteCount(d.key)<32||
                !Uri.TryCreate("udp://"+d.endpoint,UriKind.Absolute,out var uri)||uri.Port<1||uri.Port>65535||uri.AbsolutePath!="/"||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment)||!string.IsNullOrEmpty(uri.UserInfo)||string.IsNullOrEmpty(uri.Host)||d.key.StartsWith("REPLACE_",StringComparison.Ordinal))
                throw new ArgumentException("Invalid room configuration: "+d.id);
            rooms.Add(d.id,new Room{Definition=d});
        }
        if(rooms.Count<1||rooms.Count>128)throw new ArgumentException("Configure between 1 and 128 rooms.");
    }
    public bool Heartbeat(string id,string key,RoomHeartbeat heartbeat)
    {
        lock(sync)
        {
            if(!rooms.TryGetValue(id,out var room)||!AdmissionTickets.Equal(Encoding.UTF8.GetBytes(room.Definition.key),Encoding.UTF8.GetBytes(key)))return false;
            if(heartbeat.capacity<1||heartbeat.capacity>64||heartbeat.humans<0||heartbeat.humans>heartbeat.capacity||heartbeat.mode!="arena"||heartbeat.admissions?.Length>64)return false;
            room.Heartbeat=heartbeat;room.Seen=clock();Prune();
            foreach(string admission in heartbeat.admissions??Array.Empty<string>())
                if(reservations.TryGetValue(admission,out var reservation)&&reservation.Room==id)reservations.Remove(admission);
            return true;
        }
    }
    public MatchReply Match(MatchRequest request)
    {
        lock(sync)
        {
            if(request.protocol!=OnlineProtocol.Version)return new MatchReply{status="version",message="게임을 최신 버전으로 업데이트해 주세요."};
            if(request.mode!="arena")return new MatchReply{status="invalid",message="지원하지 않는 게임 모드입니다."};
            Prune();long now=clock();
            var selected=rooms.Values.Where(r=>r.Heartbeat!=null&&r.Heartbeat.protocol==request.protocol&&now-r.Seen<12&&Used(r)<r.Heartbeat.capacity)
                .OrderByDescending(r=>r.Heartbeat!.accepting).ThenByDescending(Used).ThenBy(r=>r.Definition.id).FirstOrDefault();
            if(selected==null)return new MatchReply{status="busy",message="지금 입장할 바다가 없습니다. 잠시 후 다시 시도해 주세요."};
            string nonce=Guid.NewGuid().ToString("N");long expiry=now+45;
            string ticket=AdmissionTickets.Create(selected.Definition.key,selected.Definition.id,nonce,expiry,request.protocol);
            reservations.Add(nonce,new Reservation(selected.Definition.id,ticket,expiry));
            return new MatchReply{status="assigned",roomId=selected.Definition.id,endpoint=selected.Definition.endpoint,ticket=ticket,reservationId=nonce,expiresAt=expiry};
        }
    }
    public bool Cancel(string id,string ticket)
    {
        lock(sync)
        {
            if(!reservations.TryGetValue(id,out var reservation))return true;
            if(!AdmissionTickets.Equal(Encoding.UTF8.GetBytes(ticket),Encoding.UTF8.GetBytes(reservation.Ticket)))return false;
            reservations.Remove(id);return true;
        }
    }
    private int Used(Room room)=>room.Heartbeat!.humans+reservations.Values.Count(r=>r.Room==room.Definition.id);
    private void Prune()
    {long now=clock();foreach(string id in reservations.Where(p=>p.Value.Expires<=now).Select(p=>p.Key).ToArray())reservations.Remove(id);}
}
