using System;
using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using Tidebite.Core;
using UnityEngine;

namespace Tidebite
{
    public struct JoinMessage : IBroadcast { public int Protocol;public byte Family,Skin; }
    public struct WelcomeMessage : IBroadcast { public int Slot; public string Config; public string Error; }
    public struct InputMessage : IBroadcast
    {
        public uint Sequence, Bites, Dashes;
        public float X, Y;
    }
    public struct RespawnMessage : IBroadcast { public byte Request; }
    public struct EvolutionMessage:IBroadcast { public byte Choice; }
    public struct EffectsMessage : IBroadcast { public WorldEvent[] Events; }
    // Bounded chunks leave room for food bits, round state and worst-case packed counters.
    public struct FishWire
    {
        public byte Id, Flags, Brain, Species,Combo,Rival,Skin;
        public byte Hunter,Armor,Feeder;
        public int Generation, Score,RivalGeneration,RoundScore;
        public ushort Kills,RoundKills;
        public float X, Y, VX, VY, Heading, Mass, Health;
        public ushort Bite, Dash, Protection, Respawn, BiteActive, DashActive, Hurt, Regen,Dodge,ComboTime,RivalTime,Ward;
        public static FishWire From(FishState f) => new FishWire {
            Id=(byte)f.Id, Flags=(byte)((f.Occupied?1:0)|(f.Alive?2:0)|(f.IsBot?4:0)|(f.BiteRemaining>0?8:0)|(f.DashRemaining>0?16:0)|(f.WaitingForRound?32:0)),
            Species=f.Species,Skin=f.Skin,Health=f.Health,Hurt=Millis(f.HurtProtection),Regen=Millis(f.RegenDelay),Brain=(byte)f.BrainMode,Generation=f.Generation,Score=f.Score,Kills=(ushort)Math.Min(f.Kills,65535),
            X=f.Position.X,Y=f.Position.Y,VX=f.Velocity.X,VY=f.Velocity.Y,Heading=f.Heading,Mass=f.Mass,
            Bite=Millis(f.BiteCooldown),Dash=Millis(f.DashCooldown),Protection=Millis(f.Protection),Respawn=Millis(f.RespawnRemaining),
            BiteActive=Millis(f.BiteRemaining),DashActive=Millis(f.DashRemaining),
            Dodge=Millis(f.DodgeRemaining),Combo=f.Combo,ComboTime=Millis(f.ComboRemaining),Rival=(byte)(f.RivalId+1),RivalGeneration=f.RivalGeneration,RivalTime=Millis(f.RivalRemaining),
            Hunter=f.Hunter,Armor=f.Armor,Feeder=f.Feeder,Ward=Millis(f.WardCooldown),RoundScore=f.RoundScore,RoundKills=f.RoundKills
        };
        private static ushort Millis(float f) => (ushort)Mathf.Clamp(f*1000,0,65535);
        public FishState State() => new FishState {
            Species=Species,Skin=Skin,Health=Health,HurtProtection=Hurt*.001f,RegenDelay=Regen*.001f,Id=Id,Generation=Generation,Score=Score,Kills=Kills,Occupied=(Flags&1)!=0,Alive=(Flags&2)!=0,IsBot=(Flags&4)!=0,
            BrainMode=(BotMode)Brain,Position=new V2(X,Y),PreviousPosition=new V2(X,Y),Velocity=new V2(VX,VY),Heading=Heading,Mass=Mass,
            BiteCooldown=Bite*.001f,DashCooldown=Dash*.001f,Protection=Protection*.001f,RespawnRemaining=Respawn*.001f,
            BiteRemaining=BiteActive*.001f,DashRemaining=DashActive*.001f,DodgeRemaining=Dodge*.001f,Combo=Combo,ComboRemaining=ComboTime*.001f,RivalId=Rival-1,RivalGeneration=RivalGeneration,RivalRemaining=RivalTime*.001f,
            Hunter=Hunter,Armor=Armor,Feeder=Feeder,WardCooldown=Ward*.001f,RoundScore=RoundScore,RoundKills=RoundKills,WaitingForRound=(Flags&32)!=0
        };
    }
    public struct SnapshotMessage : IBroadcast
    {
        public int Tick;
        public uint Ack;
        public ulong Visible;
        public byte Count;
        public ulong[] FoodMask;
        public FishWire[] Fish;
        public RoundInfo Round;
    }

    // Both modes use the exact same motor and predation rules. Only this class owns network state.
    public sealed class NetworkSession : MonoBehaviour, IGameSession
    {
        public const int Protocol = 7;
        public const int SnapshotFishCount=8;
        public FishState[] Fish { get; private set; }
        public FoodState[] Food { get; private set; }
        public int LocalId { get; private set; } = -1;
        public bool IsOnline => true;
        public bool Ready { get; private set; }
        public int Tick=>IsServer?ServerWorld.Tick:lastTick;
        public RoundInfo Round{get;private set;}
        public string Status { get; private set; } = "접속 중…";
        public event Action<WorldEvent> Event;
        public World ServerWorld { get; private set; }
        public int ReceivedSnapshots { get; private set; }
        public int Reconciliations { get; private set; }
        public bool IsServer { get; private set; }
        private NetworkManager manager;
        private GameConfig config;
        public GameConfig Settings => config;
        private float accumulator, startedAt, lastSnapshotAt;
        private bool disposed;
        private FishCommand pending;
        private FishState previousLocal;
        private uint sequence, bites, dashes;
        private byte family,skin;
        private int lastTick;
        private readonly int[] fishTicks = new int[GameConfig.FishCapacity];
        private readonly FishState[] remotePrevious = new FishState[GameConfig.FishCapacity];
        private readonly FishState[] remoteTarget = new FishState[GameConfig.FishCapacity];
        private readonly float[] remoteAt = new float[GameConfig.FishCapacity];
        private readonly PendingInput[] history = new PendingInput[128];
        private readonly Dictionary<int, Peer> peers = new Dictionary<int, Peer>();
        private readonly List<WorldEvent> effects = new List<WorldEvent>(256);
        private readonly ulong[] foodMask=new ulong[GameConfig.FoodCapacity/64];
        private struct PendingInput { public uint Sequence; public FishCommand Command; }
        private sealed class Peer
        {
            public NetworkConnection Connection;
            public int Slot;
            public uint Sequence, Bites, Dashes, AppliedBites, AppliedDashes;
            public FishCommand Command;
            public float ReceivedAt;
            public readonly FishWire[] Chunk=new FishWire[SnapshotFishCount];
        }

        public static NetworkSession Start(GameConfig tuning, string endpoint, bool server,byte family=0,byte skin=0)
        {
            ParseEndpoint(endpoint,out string host,out ushort port);
            var go = new GameObject(server?"Ocean server":"Ocean connection");
            go.SetActive(false);
            var session = go.AddComponent<NetworkSession>();
            session.config=tuning; session.IsServer=server; session.startedAt=Time.realtimeSinceStartup;
            session.family=family;session.skin=skin;
            session.Fish=new FishState[tuning.MaxFish]; session.Food=new World(tuning).Food;
            session.manager=go.AddComponent<NetworkManager>();
            session.manager.SpawnablePrefabs=ScriptableObject.CreateInstance<FishNet.Managing.Object.SinglePrefabObjects>();
            go.SetActive(true);
            session.manager.TimeManager.SetTickRate(30);
            session.manager.TransportManager.Transport.SetMaximumClients(tuning.MaxFish+4);
            if(server)
            {
                session.ServerWorld=new World(tuning);session.ServerWorld.FillPopulation();
                session.Fish=session.ServerWorld.Fish;session.Food=session.ServerWorld.Food;
                session.manager.ServerManager.RegisterBroadcast<JoinMessage>(session.Join);
                session.manager.ServerManager.RegisterBroadcast<InputMessage>(session.Input);
                session.manager.ServerManager.RegisterBroadcast<RespawnMessage>(session.RequestRespawn);
                session.manager.ServerManager.RegisterBroadcast<EvolutionMessage>(session.RequestEvolution);
                session.manager.ServerManager.OnRemoteConnectionState+=session.RemoteState;
                session.manager.ServerManager.OnServerConnectionState+=session.ServerState;
                session.manager.ServerManager.StartConnection(port);
            }
            else
            {
                session.manager.ClientManager.RegisterBroadcast<WelcomeMessage>(session.Welcome);
                session.manager.ClientManager.RegisterBroadcast<SnapshotMessage>(session.Snapshot);
                session.manager.ClientManager.RegisterBroadcast<EffectsMessage>(session.Effects);
                session.manager.ClientManager.OnAuthenticated+=session.Authenticated;
                session.manager.ClientManager.OnClientConnectionState+=session.ClientState;
                session.manager.ClientManager.StartConnection(host,port);
            }
            return session;
        }

        public void SimulateNetwork(long latencyMs,double packetLoss)
        {
            if(!Debug.isDebugBuild)throw new InvalidOperationException("Latency simulation requires a Development build.");
            var simulator=manager.TransportManager.LatencySimulator;
            simulator.SetLatency(Math.Max(0,latencyMs));simulator.SetPacketLoss(Math.Max(0,Math.Min(.5,packetLoss)));simulator.SetEnabled(true);
            Debug.Log($"TIDEBITE NETWORK_SIMULATION latencyMs={latencyMs} loss={packetLoss}");
        }

        public static void ParseEndpoint(string endpoint,out string host,out ushort port)
        {
            host=string.IsNullOrWhiteSpace(endpoint)?"127.0.0.1":endpoint.Trim();port=7777;
            // Bracketed IPv6 is accepted, as are a DNS name and IPv4.
            if(host.StartsWith("["))
            {
                int end=host.IndexOf(']');if(end<0)throw new FormatException("서버 주소를 확인해 주세요.");
                string suffix=host.Substring(end+1);string address=host.Substring(1,end-1);
                if(suffix.Length>0 && (!suffix.StartsWith(":") || !ushort.TryParse(suffix.Substring(1),out port) || port==0))throw new FormatException("포트는 1~65535입니다.");
                host=address;
            }
            else if(host.IndexOf(':')==host.LastIndexOf(':') && host.Contains(":"))
            {
                int colon=host.LastIndexOf(':');
                if(!ushort.TryParse(host.Substring(colon+1),out port)||port==0)throw new FormatException("포트는 1~65535입니다.");
                host=host.Substring(0,colon);
            }
            if(string.IsNullOrWhiteSpace(host)||host.Contains("/")||host.Contains(" "))throw new FormatException("IP 또는 도메인:포트를 입력해 주세요.");
        }

        private void ServerState(ServerConnectionStateArgs args)
        {
            Ready=args.ConnectionState==LocalConnectionState.Started;
            Status=Ready?"SERVER · UDP 7777":"서버 중지";
            if(Ready)Debug.Log("TIDEBITE SERVER_READY protocol="+Protocol);
        }
        private void Authenticated() => manager.ClientManager.Broadcast(new JoinMessage{Protocol=Protocol,Family=family,Skin=skin});
        private void ClientState(ClientConnectionStateArgs args)
        {
            if(args.ConnectionState==LocalConnectionState.Stopped){Ready=false;if(!disposed)Status="서버 연결이 종료되었습니다. 주소와 서버 상태를 확인해 주세요.";}
        }
        private void Join(NetworkConnection connection,JoinMessage message,Channel channel)
        {
            if(peers.ContainsKey(connection.ClientId))return;
            if(message.Protocol!=Protocol){Reject(connection,"클라이언트와 서버 버전이 다릅니다.");return;}
            int slot=ServerWorld.AddFish(false);
            if(slot<0){Reject(connection,"바다가 가득 찼습니다. 잠시 후 다시 접속해 주세요.");return;}
            ServerWorld.Fish[slot].Species=(byte)Math.Min(1,(int)message.Family);ServerWorld.Fish[slot].Skin=(byte)Math.Min(3,(int)message.Skin);
            peers.Add(connection.ClientId,new Peer{Connection=connection,Slot=slot,ReceivedAt=Time.realtimeSinceStartup});
            manager.ServerManager.Broadcast(connection,new WelcomeMessage{Slot=slot,Config=JsonUtility.ToJson(config)});
            Debug.Log($"TIDEBITE JOIN client={connection.ClientId} slot={slot} humans={peers.Count}");
        }
        private void Reject(NetworkConnection connection,string error)
        {
            manager.ServerManager.Broadcast(connection,new WelcomeMessage{Slot=-1,Error=error});
            connection.Disconnect(false);
        }
        private void RemoteState(NetworkConnection connection,RemoteConnectionStateArgs args)
        {
            if(args.ConnectionState!=RemoteConnectionState.Stopped||!peers.TryGetValue(connection.ClientId,out var peer))return;
            ServerWorld.RemoveFish(peer.Slot);peers.Remove(connection.ClientId);ServerWorld.FillPopulation();
            Debug.Log($"TIDEBITE LEAVE client={connection.ClientId} humans={peers.Count}");
        }
        private void Input(NetworkConnection connection,InputMessage message,Channel channel)
        {
            if(!peers.TryGetValue(connection.ClientId,out var peer)||message.Sequence<=peer.Sequence)return;
            // Clients send direction and action counters only. No position, mass, target or time step is trusted.
            peer.Sequence=message.Sequence;
            peer.Command=new FishCommand(new V2(message.X,message.Y)).Sanitized();
            peer.Bites=Math.Max(peer.Bites,message.Bites);peer.Dashes=Math.Max(peer.Dashes,message.Dashes);
            peer.ReceivedAt=Time.realtimeSinceStartup;
        }
        private void RequestRespawn(NetworkConnection connection,RespawnMessage message,Channel channel)
        { if(peers.TryGetValue(connection.ClientId,out var peer))ServerWorld.Respawn(peer.Slot); }
        private void RequestEvolution(NetworkConnection connection,EvolutionMessage message,Channel channel)
        {if(peers.TryGetValue(connection.ClientId,out var peer)&&ServerWorld.ChooseAdaptation(peer.Slot,(Adaptation)message.Choice))effects.Add(new WorldEvent{Kind=WorldEventKind.Evolve,Actor=peer.Slot,Target=-1,Position=Fish[peer.Slot].Position,Value=message.Choice});}
        private void Welcome(WelcomeMessage message,Channel channel)
        {
            if(message.Slot<0){Status=message.Error;Ready=false;return;}
            config=JsonUtility.FromJson<GameConfig>(message.Config);config.Validate();
            LocalId=message.Slot;Fish=new FishState[config.MaxFish];Food=new World(config).Food;Ready=true;lastSnapshotAt=Time.realtimeSinceStartup;
            Status="ONLINE OCEAN";Debug.Log("TIDEBITE CLIENT_READY slot="+LocalId);
        }
        private void Snapshot(SnapshotMessage message,Channel channel)
        {
            if(!Ready || message.Fish==null || message.Tick<lastTick)return;
            ReceivedSnapshots++;lastSnapshotAt=Time.realtimeSinceStartup;
            if(message.Tick>lastTick)
            {
                lastTick=message.Tick;
                Round=message.Round;
                for(int i=0;i<Fish.Length;i++)if((message.Visible&(1UL<<i))==0)Fish[i].Occupied=false;
                for(int f=0;f<Food.Length;f++)
                {
                    ulong mask=message.FoodMask!=null&&f/64<message.FoodMask.Length?message.FoodMask[f/64]:0;
                    Food[f].RespawnRemaining=(mask&(1UL<<(f%64)))!=0?0:1;
                }
                OceanHabitats.PlaceGold(Food,config,Round.Elapsed);
            }
            for(int index=0;index<Math.Min(message.Count,message.Fish.Length);index++)
            {
                var wire=message.Fish[index];
                int id=wire.Id;if(id>=Fish.Length||fishTicks[id]>=message.Tick)continue;
                fishTicks[id]=message.Tick;var state=wire.State();
                if(id==LocalId)
                {
                    if(state.Generation!=Fish[id].Generation || !state.Alive)Array.Clear(history,0,history.Length);
                    // Reconcile to server truth, then replay only unacknowledged local inputs.
                    uint first=Math.Max(message.Ack+1,sequence>127?sequence-127:1);
                    for(uint n=first;n<=sequence;n++)
                    {
                        var h=history[n%128];if(h.Sequence==n && state.Alive)
                        {if(h.Command.Bite&&state.BiteCooldown<=config.TickDelta)BiteAssist.Apply(ref state,Fish,config);FishMotor.Step(ref state,h.Command,config,config.TickDelta);}
                    }
                    if(V2.Distance(Fish[id].Position,state.Position)>.4f)Reconciliations++;
                    Fish[id]=state;
                    previousLocal=state;
                    previousLocal.Position=state.PreviousPosition;
                }
                else
                {
                    remotePrevious[id]=Fish[id].Generation==state.Generation?Fish[id]:state;
                    remoteTarget[id]=state;remoteAt[id]=Time.realtimeSinceStartup;Fish[id]=state;
                }
            }
        }
        private void Effects(EffectsMessage message,Channel channel)
        {
            if(message.Events==null)return;
            foreach(var e in message.Events)
                if(e.Actor!=LocalId || (e.Kind!=WorldEventKind.Bite && e.Kind!=WorldEventKind.Dash))Event?.Invoke(e);
        }
        public void Submit(FishCommand command)
        {pending.Move=command.Move;pending.Bite|=command.Bite;pending.Dash|=command.Dash;}
        public void Advance(float elapsed)
        {
            if(disposed)return;
            if(!IsServer && !Ready && Time.realtimeSinceStartup-startedAt>12 && Status=="접속 중…")
            {Status="접속 시간이 초과되었습니다. 서버 주소와 UDP 포트를 확인해 주세요.";manager.ClientManager.StopConnection();}
            if(!Ready)return;
            if(!IsServer && Time.realtimeSinceStartup-lastSnapshotAt>8)
            {Ready=false;Status="연결이 끊겼습니다. 홈에서 다시 접속해 주세요.";manager.ClientManager.StopConnection();return;}
            accumulator=Math.Min(accumulator+elapsed,.2f);
            while(accumulator>=config.TickDelta)
            {
                if(IsServer)ServerTick();else ClientTick();
                accumulator-=config.TickDelta;
            }
            if(!IsServer)for(int i=0;i<Fish.Length;i++)
            {
                if(i==LocalId||!Fish[i].Occupied)continue;
                float t=Mathf.Clamp01((Time.realtimeSinceStartup-remoteAt[i])/(config.TickDelta*2));
                Fish[i].Position=V2.Lerp(remotePrevious[i].Position,remoteTarget[i].Position,t);
                Fish[i].Heading=remotePrevious[i].Heading+Math2.DeltaAngle(remotePrevious[i].Heading,remoteTarget[i].Heading)*t;
            }
        }
        private void ClientTick()
        {
            if(LocalId<0)return;
            sequence++;var command=pending.Sanitized();
            if(command.Bite)bites++;if(command.Dash)dashes++;
            history[sequence%128]=new PendingInput{Sequence=sequence,Command=command};
            if(Fish[LocalId].Alive&&Round.Phase!=RoundPhase.Results)
            {
                previousLocal=Fish[LocalId];
                var local=Fish[LocalId];
                if(command.Bite&&local.BiteCooldown<=config.TickDelta)BiteAssist.Apply(ref Fish[LocalId],Fish,config);
                if(command.Bite&&local.BiteCooldown<=config.TickDelta)Event?.Invoke(new WorldEvent{Kind=WorldEventKind.Bite,Actor=LocalId,Position=local.Position});
                if(command.Dash&&local.DashCooldown<=config.TickDelta)Event?.Invoke(new WorldEvent{Kind=WorldEventKind.Dash,Actor=LocalId,Position=local.Position});
                FishMotor.Step(ref Fish[LocalId],command,config,config.TickDelta);
            }
            manager.ClientManager.Broadcast(new InputMessage{Sequence=sequence,Bites=bites,Dashes=dashes,X=command.Move.X,Y=command.Move.Y},Channel.Unreliable);
            pending.Bite=pending.Dash=false;
        }
        private void ServerTick()
        {
            foreach(var peer in peers.Values)
            {
                var command=Time.realtimeSinceStartup-peer.ReceivedAt<.3f?peer.Command:default;
                command.Bite=peer.Bites>peer.AppliedBites;command.Dash=peer.Dashes>peer.AppliedDashes;
                peer.AppliedBites=peer.Bites;peer.AppliedDashes=peer.Dashes;
                ServerWorld.SetInput(peer.Slot,command);
            }
            ServerWorld.Step();
            Round=ServerWorld.Round;
            for(int e=0;e<ServerWorld.EventCount;e++)effects.Add(ServerWorld.Events[e]);
            if(ServerWorld.Tick%2!=0)return;
            foreach(var peer in peers.Values)SendSnapshot(peer);
            if(effects.Count>0 && peers.Count>0)
                manager.ServerManager.Broadcast(new EffectsMessage{Events=effects.ToArray()},true,Channel.Reliable);
            effects.Clear();
        }
        private void SendSnapshot(Peer peer)
        {
            var round=ServerWorld.Round;round.LocalRank=ServerWorld.Rank(peer.Slot);
            var packet=new SnapshotMessage{Tick=ServerWorld.Tick,Ack=peer.Sequence,FoodMask=foodMask,Round=round};
            Array.Clear(foodMask,0,foodMask.Length);
            V2 origin=Fish[peer.Slot].Position;
            for(int i=0;i<Fish.Length;i++)if(Fish[i].Occupied && (i==peer.Slot || i==round.KingId || V2.Distance(Fish[i].Position,origin)<48&&OceanHabitats.CanSee(Fish[peer.Slot],Fish[i],config)))packet.Visible|=1UL<<i;
            for(int f=0;f<Food.Length;f++)if(Food[f].Active)
            {foodMask[f/64]|=1UL<<(f%64);}
            int count=0;packet.Fish=peer.Chunk;
            for(int i=0;i<Fish.Length;i++)if((packet.Visible&(1UL<<i))!=0)
            {
                peer.Chunk[count++]=FishWire.From(Fish[i]);
                if(count==SnapshotFishCount){packet.Count=(byte)count;manager.ServerManager.Broadcast(peer.Connection,packet,true,Channel.Unreliable);count=0;}
            }
            if(count>0){packet.Count=(byte)count;manager.ServerManager.Broadcast(peer.Connection,packet,true,Channel.Unreliable);}
        }
        public void Respawn(){if(Ready&&!IsServer)manager.ClientManager.Broadcast(new RespawnMessage{Request=1});}
        public void ChooseEvolution(Adaptation choice)
        {if(Ready&&!IsServer)manager.ClientManager.Broadcast(new EvolutionMessage{Choice=(byte)choice});}
        public FishState RenderFish(int id)
        {
            // Remote fish are already interpolated between snapshots in Advance.
            return id==LocalId?FishPresentation.Interpolate(previousLocal,Fish[id],accumulator/config.TickDelta):Fish[id];
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;Ready=false;Event=null;
            if(this==null)return;
            if(manager!=null){manager.ClientManager.StopConnection();manager.ServerManager.StopConnection(true);}
            Destroy(gameObject);
        }
    }
}
