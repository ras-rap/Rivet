using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Rivet.Network;
using Rivet.Protocol;

namespace Rivet.Server;

public class GameServer : IDisposable
{
    private readonly PacketProtocol _proto;
    private readonly PlayerManager _players;
    private readonly Config _config;
    private readonly PreConnectServer? _preConnect;
    private readonly SteamServerManager? _steam;
    private readonly ApiServer? _api;
    private readonly MatchmakingServerPinger _matchmakingPinger;
    private readonly BanManager _bans = new();
    private readonly LogWriter _log = new();
    private readonly HttpClient _http = new();
    private float _idleCheckTimer;
    private float _mapRotationTimer;
    private int _mapRotationIndex;
    private bool _voteActive;
    private int _voteTargetIsland;
    private float _voteEndTime;
    private readonly Dictionary<byte, bool> _votes = new();
    private float _time;
    private float _playerListTimer;
    private float _serverInfoTimer;
    private float _islandInfoTimer;
    private float _globalTime;
    private bool _hasPassword;
    private int _currentIslandID = -801448567; // Pulau Mahkota // Pulau Mahkota
    private readonly Dictionary<byte, CarData> _carData = new();
    private readonly Dictionary<byte, PlayerCarLoadState> _carLoadStates = new();
    private readonly Dictionary<byte, bool> _playerReadyStates = new();
    private readonly Dictionary<byte, SpawnPose> _playerSpawns = new();
    private float _readyListTimer;
    private float _carDataStateTimer;
    private float _rigSyncTimer;
    private float _settingsTimer;
    private float _carListTimer;
    private byte[] _serverSettings = null!;
    private readonly Dictionary<byte, PlayerState> _playerStates = new();
    private const float CrashVelocityThreshold = 15f;
    private const float CrashVelocityDropRatio = 0.4f;

    private class PlayerState
    {
        public float LastVelMagnitude;
        public float PreviousVelMagnitude;
        public float Health = 100f;
        public float LastCrashTime;
        public float PlayTime;
        public float LastActivityTime;
        public float PosX, PosY, PosZ;
        public float RotX, RotY, RotZ;
        public float VelX, VelY, VelZ;
        public float[] PosXArr = [], PosYArr = [], PosZArr = [];
        public float[] RotXArr = [], RotYArr = [], RotZArr = [];
        public float[] VelXArr = [], VelYArr = [], VelZArr = [];
        public byte[] TransformIDs = [];
    }

    private class SpawnPose
    {
        public float PosX, PosY, PosZ;
        public float RotX, RotY, RotZ;
    }

    private class PlayerCarLoadState
    {
        public string CarFileName = "";
        public int BaguetteBytesLen;
        public int CCCBytesLen;
        public byte[] BaguetteData = [];
        public byte[] CCCData = [];
        public bool DataComplete;
    }

    private class CarData
    {
        public byte[] Data = [];
        public int HashCode;
        public byte PlatformPositionIndex = byte.MaxValue;
        public int LastBaguetteLen = -1;
    }

    private readonly struct IncomingMsg
    {
        public readonly IPEndPoint Source;
        public readonly ushort MsgId;
        public readonly byte[] Payload;
        public IncomingMsg(IPEndPoint s, ushort id, byte[] p) { Source = s; MsgId = id; Payload = p; }
    }
    private readonly ConcurrentQueue<IncomingMsg> _incoming = new();

    private readonly Dictionary<ushort, Action<IPEndPoint, DataObject>> _handlers = new();

    public GameServer(Config config)
    {
        _config = config;
        _proto = new PacketProtocol(config.Port);
        _players = new PlayerManager(10f);
        _hasPassword = !string.IsNullOrEmpty(config.Password);
        _preConnect = new PreConnectServer(config.PreConnectPort);
        _steam = new SteamServerManager((ushort)config.Port, config.SteamQueryPort, config.ServerName, config.MaxPlayers, _hasPassword, config.SteamVersion, _proto.SendRaw);
        _matchmakingPinger = new MatchmakingServerPinger(
            () => _steam?.PublicIP ?? "0.0.0.0",
            config.Port,
            config.ServerName,
            config.MaxPlayers,
            config.SteamVersion,
            () => _players.PlayerCount,
            () => _hasPassword);
        _api = new ApiServer(this, config, _log);

        _proto.OnRawPacket += (ep, data) => _steam?.HandleRawPacket(ep, data);

        Register<ConnectMsg>(MsgId.ConnectMsg, HandleConnect);
        Register<DisconnectMsg>(MsgId.DisconnectMsg, HandleDisconnect);
        Register<PingMsgToServer>(MsgId.PingMsgToServer, HandlePing);
        Register<InputsMsg>(MsgId.InputsMsg, HandleInputs);
        Register<MsgTransformSyncToServer>(MsgId.MsgTransformSyncToServer, HandleTransformSync);
        Register<ChatToServerMsg>(MsgId.ChatToServerMsg, HandleChat);
        Register<MsgCheckPasswordToServer>(MsgId.MsgCheckPasswordToServer, HandlePasswordCheck);
        Register<MsgCurrentIslandToServer>(MsgId.MsgCurrentIslandToServer, HandleIslandChange);
        Register<MsgMultiplayerGameStateToServer>(MsgId.MsgMultiplayerGameStateToServer, HandleGameStateChange);
        Register<MsgFirstPersonInfoToServer>(MsgId.MsgFirstPersonInfoToServer, HandleFirstPersonInfo);
        Register<MultiplayerGeneralInfoMsgToServer>(MsgId.MultiplayerGeneralInfoMsgToServer, HandleGeneralInfo);
        Register<MsgCharacterBytesToServer>(MsgId.MsgCharacterBytesToServer, HandleCharacterBytes);
        Register<MsgExcusePingToServer>(MsgId.MsgExcusePingToServer, HandleExcusePing);
        Register<MsgGameMenuStateToServer>(MsgId.MsgGameMenuStateToServer, HandleGameMenuState);
        Register<MsgCarSyncerGarageToServer>(MsgId.MsgCarSyncerGarageToServer, HandleCarSyncerGarage);
        Register<MsgSpawnPointPoseToServer>(MsgId.MsgSpawnPointPoseToServer, HandleSpawnPointPose);
        Register<MsgCarDataStateOfSelfToServer>(MsgId.MsgCarDataStateOfSelfToServer, HandleCarDataStateOfSelf);
        Register<MsgCarDataToServer>(MsgId.MsgCarDataToServer, HandleCarDataToServer);
        Register<MsgRequestCarDataToServer>(MsgId.MsgRequestCarDataToServer, HandleRequestCarData);
        Register<ReverseMsgToServer>(MsgId.ReverseMsgToServer, HandleReverse);
        Register<ShiftGearServerMsg>(MsgId.ShiftGearServerMsg, HandleShiftGear);
        Register<MsgReadyupToServer>(MsgId.MsgReadyupToServer, HandleReadyup);
        Register<RepairMsg>(MsgId.RepairMsg, HandleRepair);
        Register<MsgDestroyPartsToServer>(MsgId.MsgDestroyPartsToServer, HandleDestroyParts);
        Register<SelfCarStateMsg>(MsgId.SelfCarStateMsg, HandleSelfCarState);
        Register<MsgPerformHornToServer>(MsgId.MsgPerformHornToServer, HandlePerformHorn);
        Register<MsgSelfReadyToServer>(MsgId.MsgSelfReadyToServer, HandleSelfReady);
        Register<MsgCommandToServer>(MsgId.MsgCommandToServer, HandleCommandMsg);
        Register<SetSpawnPointMsgToServer>(MsgId.SetSpawnPointMsgToServer, HandleSetSpawnPoint);

        _serverSettings = MakeDefaultServerSettings();
        _proto.OnMessage += OnRawMessage;
        _players.OnPlayerConnected += OnPlayerConnected;
        _players.OnPlayerDisconnected += OnPlayerDisconnected;
    }

    private void Register<T>(ushort id, Action<IPEndPoint, T> handler) where T : DataObject, new()
    {
        _handlers[id] = (ep, obj) => handler(ep, (T)obj);
    }

    private void OnRawMessage(IPEndPoint source, byte[] raw)
    {
        if (raw.Length < 2) return;
        ushort msgId = (ushort)((raw[0] << 8) | raw[1]);
        var payload = new byte[raw.Length - 2];
        Array.Copy(raw, 2, payload, 0, payload.Length);
        _incoming.Enqueue(new IncomingMsg(source, msgId, payload));
    }

    private void OnPlayerConnected(Player p)
    {
        _log.Info($"Player {p.PlayerName} (ID {p.PlayerID}) connected from {p.EndPoint}");

        if (_bans.IsBanned(p.EndPoint.Address, p.CSteamID))
        {
            _log.Warn($"Rejected banned player {p.PlayerName} ({p.EndPoint.Address})");
            _players.RemovePlayer(p.PlayerID);
            return;
        }

        Send(p.EndPoint, true, new MsgSetPlayerHostToClient
        {
            PlayerID = p.PlayerID,
            CSteamIDHost = p.CSteamID
        });

        Send(p.EndPoint, true, new MsgIsPlainCSharpToClient { IsPlainCSharpServer = true });

        Send(p.EndPoint, true, new MsgServerSettingsToClient { SerializedServerSettings = _serverSettings });

        Send(p.EndPoint, false, new MsgMultiplayerGameStateInfoToClient { MultiplayerGameState = 0 });
        Send(p.EndPoint, false, new MsgCurrentGameModeToClient { CurrentGameModeID = 0 });

        // Send island messages reliably so they are delivered before clickInitialIsland fires
        Send(p.EndPoint, true, new MsgCurrentIslandToClient { IslandUniqueID = _currentIslandID });
        Send(p.EndPoint, true, new MsgIslandConfigToClient { IslandConfigUniqueID = _currentIslandID });

        // Join message
        if (!string.IsNullOrEmpty(_config.JoinMessage))
            SendChat(p.EndPoint, _config.JoinMessage);

        _steam?.UpdatePlayerCount(_players.PlayerCount);
        _ = SendWebhookAsync($"{p.PlayerName} joined the server");
    }

    private void OnPlayerDisconnected(Player p)
    {
        _log.Info($"Player {p.PlayerName} (ID {p.PlayerID}) disconnected");
        if (_playerStates.TryGetValue(p.PlayerID, out var discPs))
            _log.Info($"Player {p.PlayerName} stats - playtime: {discPs.PlayTime:F0}s");
        _steam?.UpdatePlayerCount(_players.PlayerCount);
        _ = SendWebhookAsync($"{p.PlayerName} left the server");
    }

    private readonly ConcurrentQueue<string> _consoleCmds = new();

    public void Run()
    {
        Console.WriteLine($"Rivet v0.1");
        Console.WriteLine($"Port: {_config.Port}  Max players: {_config.MaxPlayers}" +
                          (_hasPassword ? $"  Password: {_config.Password}" : ""));
        _api?.Start();
        Console.WriteLine("Type 'help' for commands.");

        // Background thread for console input
        var consoleThread = new System.Threading.Thread(() =>
        {
            while (true)
            {
                var line = System.Console.ReadLine() ?? "";
                _consoleCmds.Enqueue(line);
            }
        })
        { IsBackground = true, Name = "Console-Input" };
        consoleThread.Start();

        var lastTime = DateTime.UtcNow;
        while (true)
        {
            var now = DateTime.UtcNow;
            var dt = (float)(now - lastTime).TotalSeconds;
            if (dt > 0.1f) dt = 0.016f;
            lastTime = now;

            Tick(dt);

            // Process console commands
            while (_consoleCmds.TryDequeue(out var line))
                HandleConsoleCommand(line);

            System.Threading.Thread.Sleep(10);
        }
    }

    public void Tick(float dt)
    {
        _time += dt;
        _globalTime += dt;
        if (_globalTime > 1000f) _globalTime -= 1000f;

        // Poll network
        _proto.Poll();
        _steam?.Tick();
        _matchmakingPinger.Tick(dt);

        // Process incoming messages
        while (_incoming.TryDequeue(out var msg))
        {
            var obj = MessageRegistry.Create(msg.MsgId);
            if (obj == null)
            {
                Console.WriteLine($"[Debug] Unhandled message ID {msg.MsgId} ({msg.Payload.Length} bytes: {BitConverter.ToString(msg.Payload).Replace("-", " ")[..Math.Min(60, msg.Payload.Length * 3 - 1)]})");
                continue;
            }
            obj.ParseBytes(msg.Payload, 0);

            if (_handlers.TryGetValue(msg.MsgId, out var handler))
                handler(msg.Source, obj);
        }

        // Tick player timeouts
        _players.Tick(dt);

        // Periodic broadcasts
        _playerListTimer += dt;
        if (_playerListTimer >= 2f)
        {
            _playerListTimer -= 2f;
            BroadcastPlayerList();
            BroadcastGlobalTime();
        }

        _serverInfoTimer += dt;
        if (_serverInfoTimer >= 3f)
        {
            _serverInfoTimer -= 3f;
            BroadcastServerInfo();
        }

        _islandInfoTimer += dt;
        if (_islandInfoTimer >= 0.5f)
        {
            _islandInfoTimer -= 0.5f;
            BroadcastIslandInfo();
            Broadcast(false, new MsgCurrentGameModeToClient { CurrentGameModeID = 0 });
            Broadcast(false, new MsgMultiplayerGameStateInfoToClient { MultiplayerGameState = 0 });
            BroadcastCarData();
        }

        _readyListTimer += dt;
        if (_readyListTimer >= 2f)
        {
            _readyListTimer -= 2f;
            BroadcastReadyStates();
        }

        _settingsTimer += dt;
        if (_settingsTimer >= 5f)
        {
            _settingsTimer -= 5f;
            BroadcastServerSettings();
        }

        _carDataStateTimer += dt;
        if (_carDataStateTimer >= 1f)
        {
            _carDataStateTimer -= 1f;
            BroadcastCarDataStateAllPlayers();
        }

        _carListTimer += dt;
        if (_carListTimer >= 5f)
        {
            _carListTimer -= 5f;
            BroadcastAllPlayerCars();
        }

        // Rigidbody sync (AllRigsInfoMsg) - sends transform data for all players
        // Original RigsSyncerServer runs at syncFrequency * 0.5 = ~0.15s
        _rigSyncTimer += dt;
        if (_rigSyncTimer >= 0.15f)
        {
            _rigSyncTimer -= 0.15f;
            BroadcastAllRigsInfo();
        }

        // Crash detection via velocity monitoring
        foreach (var p in _players.Players)
        {
            if (!_playerStates.TryGetValue(p.PlayerID, out var ps))
                continue;

            float prevMag = ps.PreviousVelMagnitude;
            float curMag = ps.LastVelMagnitude;

            // Check if velocity dropped sharply (crash)
            if (prevMag > CrashVelocityThreshold && curMag < prevMag * CrashVelocityDropRatio && _time - ps.LastCrashTime > 2f)
            {
                float damage = (prevMag / 50f) * 25f;
                ps.Health = MathF.Max(0, ps.Health - damage);
                ps.LastCrashTime = _time;

                _log.Info($"Player {p.PlayerID} crash: {prevMag:F0}→{curMag:F0}, health {ps.Health:F0}%");

                SendChat(p.EndPoint, $"CRASH! -{damage:F0}% HP ({ps.Health:F0}% remaining)");

                if (ps.Health <= 0)
                {
                    _log.Info($"Player {p.PlayerID} wrecked - respawning");
                    Send(p.EndPoint, true, new RepairClientMsg
                    {
                        PlayerID = p.PlayerID,
                        IsRepairInsteadOfReset = false
                    });
                    ps.Health = 100f;
                }
            }
        }

        // Play time tracking
        foreach (var p in _players.Players)
        {
            if (_playerStates.TryGetValue(p.PlayerID, out var ptPs))
                ptPs.PlayTime += dt;
        }

        // Idle kick
        _idleCheckTimer += dt;
        if (_idleCheckTimer >= 5f && _config.IdleKickMinutes > 0)
        {
            _idleCheckTimer -= 5f;
            float idleTimeout = _config.IdleKickMinutes * 60f;
            foreach (var p in _players.Players.ToArray())
            {
                if (_playerStates.TryGetValue(p.PlayerID, out var idlePs) && _time - idlePs.LastActivityTime > idleTimeout)
                {
                    _log.Info($"Kicking idle player {p.PlayerName}");
                    SendChat(p.EndPoint, $"Kicked for being idle {_config.IdleKickMinutes} minutes");
                    _players.RemovePlayer(p.PlayerID);
                    BroadcastPlayerList();
                }
            }
        }

        // Map rotation
        if (_config.MapRotationIslands.Length > 0 && _config.MapRotationIntervalMinutes > 0)
        {
            _mapRotationTimer += dt;
            if (_mapRotationTimer >= _config.MapRotationIntervalMinutes * 60f)
            {
                _mapRotationTimer = 0;
                _mapRotationIndex = (_mapRotationIndex + 1) % _config.MapRotationIslands.Length;
                _currentIslandID = _config.MapRotationIslands[_mapRotationIndex];
                BroadcastIslandInfo();
                _log.Info($"Map rotated to island ID {_currentIslandID}");
                Broadcast(false, new ChatToClientMsg { PlayerID = byte.MaxValue, Message = $"[Server] Map rotated to island ID {_currentIslandID}" });
                _ = SendWebhookAsync($"Map rotated to island `{_currentIslandID}`");
            }
        }

        // Vote expiry
        if (_voteActive && _time >= _voteEndTime)
            ResolveVote();
    }

    // --- Message handlers ---

    private void HandleConnect(IPEndPoint ep, ConnectMsg msg)
    {
        Console.WriteLine($"[Connect] {msg.Playername} SteamID={msg.CSteamID} ForcedID={msg.ForcedSelfID} from {ep}");

        if (_players.GetPlayerByEndPoint(ep) != null)
        {
            Send(ep, true, new ConnectAnswer { PlayerID = byte.MaxValue, ConnectSuccessfull = false, ConnectionFailReason = 2 });
            return;
        }

        // Reject duplicate connection from same IP (different port = retry)
        if (_players.IsIPConnected(ep.Address))
        {
            // Client is retrying on a new port — update their endpoint
            var existing = _players.GetPlayerByEndPoint(ep);
            if (existing == null)
            {
                for (int i = 0; i < _players.Players.Count; i++)
                {
                    if (_players.Players[i].IsConnected && _players.Players[i].EndPoint.Address.Equals(ep.Address))
                    {
                        _players.Players[i].EndPoint = ep;
                        Send(ep, true, new ConnectAnswer { PlayerID = _players.Players[i].PlayerID, ConnectSuccessfull = true, ConnectionFailReason = byte.MaxValue });
                        return;
                    }
                }
            }
            Send(ep, true, new ConnectAnswer { PlayerID = byte.MaxValue, ConnectSuccessfull = false, ConnectionFailReason = 2 });
            return;
        }

        if (_players.IsServerFull(_config.MaxPlayers))
        {
            Send(ep, true, new ConnectAnswer { PlayerID = byte.MaxValue, ConnectSuccessfull = false, ConnectionFailReason = 0 });
            return;
        }

        var player = _players.AddPlayer(ep, msg.Playername, msg.CSteamID, msg.ForcedSelfID);
        Send(ep, true, new ConnectAnswer
        {
            PlayerID = player.PlayerID,
            ConnectSuccessfull = true,
            ConnectionFailReason = byte.MaxValue
        });
        BroadcastPlayerList();
    }

    private void HandleDisconnect(IPEndPoint ep, DisconnectMsg msg)
    {
        _players.RemovePlayer(msg.PlayerID);
        BroadcastPlayerList();
    }

    private void HandlePing(IPEndPoint ep, PingMsgToServer msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player != null)
        {
            player.Ping = msg.EstimatedSelfPing;
            player.NoMessageFor = 0;
            player.PingExcusedFor = 0;
            if (!player.EndPoint.Equals(ep))
                player.EndPoint = ep;
        }
        Send(ep, false, new PingMsg());
    }

    private void HandleInputs(IPEndPoint ep, InputsMsg msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;
        _players.ResetTimeout(msg.PlayerID);

        var all = BuildAllInputs(msg);
        foreach (var other in _players.Players)
            if (other.PlayerID != msg.PlayerID)
                Send(other.EndPoint, false, all);
    }

    private static AllInputsMsg BuildAllInputs(InputsMsg msg) => new()
    {
        PlayerIDs = [msg.PlayerID],
        StickLeftX = [msg.Axis0], StickLeftY = [msg.Axis1],
        StickRightX = [msg.Axis2], StickRightY = [msg.Axis3],
        TriggerLeft = [msg.Axis4], TriggerRight = [msg.Axis5],
        Axis0Raw = [msg.Axis0Raw], Axis1Raw = [msg.Axis1Raw],
        Axis2Raw = [msg.Axis2Raw], Axis3Raw = [msg.Axis3Raw],
        Axis4Raw = [msg.Axis4Raw], Axis5Raw = [msg.Axis5Raw],
        InputMap = [msg.InputMap], InputMapToggle = [msg.InputMapToggle]
    };

    private void HandleTransformSync(IPEndPoint ep, MsgTransformSyncToServer msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;
        _players.ResetTimeout(msg.PlayerID);

        if (!_playerStates.TryGetValue(msg.PlayerID, out var ps))
        {
            ps = new PlayerState();
            _playerStates[msg.PlayerID] = ps;
        }

        if (msg.PosX.Length > 0) ps.PosX = msg.PosX[0];
        if (msg.PosY.Length > 0) ps.PosY = msg.PosY[0];
        if (msg.PosZ.Length > 0) ps.PosZ = msg.PosZ[0];
        if (msg.RotX.Length > 0) ps.RotX = msg.RotX[0];
        if (msg.RotY.Length > 0) ps.RotY = msg.RotY[0];
        if (msg.RotZ.Length > 0) ps.RotZ = msg.RotZ[0];
        if (msg.VelX.Length > 0) ps.VelX = msg.VelX[0];
        if (msg.VelY.Length > 0) ps.VelY = msg.VelY[0];
        if (msg.VelZ.Length > 0) ps.VelZ = msg.VelZ[0];

        // Track velocity for crash detection
        if (msg.VelX.Length > 0 && msg.VelY.Length > 0 && msg.VelZ.Length > 0)
        {
            float vx = msg.VelX[0], vy = msg.VelY[0], vz = msg.VelZ[0];
            float mag = MathF.Sqrt(vx * vx + vy * vy + vz * vz);
            ps.PreviousVelMagnitude = ps.LastVelMagnitude;
            ps.LastVelMagnitude = mag;
            ps.LastActivityTime = _time;
        }

        // Store full transform arrays for AllRigsInfoMsg broadcast
        ps.TransformIDs = msg.IDs;
        ps.PosXArr = msg.PosX; ps.PosYArr = msg.PosY; ps.PosZArr = msg.PosZ;
        ps.RotXArr = msg.RotX; ps.RotYArr = msg.RotY; ps.RotZArr = msg.RotZ;
        ps.VelXArr = msg.VelX; ps.VelYArr = msg.VelY; ps.VelZArr = msg.VelZ;

        // Keep instant relay for MsgTransformSyncToClient (legacy, may be unused by client for remote cars)
        var relay = new MsgTransformSyncToClient
        {
            PlayerIDs = [msg.PlayerID],
            IDs = msg.IDs,
            PosX = msg.PosX, PosY = msg.PosY, PosZ = msg.PosZ,
            RotX = msg.RotX, RotY = msg.RotY, RotZ = msg.RotZ,
            VelX = msg.VelX, VelY = msg.VelY, VelZ = msg.VelZ
        };

        foreach (var other in _players.Players)
            if (other.PlayerID != msg.PlayerID)
                Send(other.EndPoint, false, relay);
    }

    private void HandleChat(IPEndPoint ep, ChatToServerMsg msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;

        var text = msg.Message.Trim();
        if (text.Length == 0) return;

        _ = SendWebhookAsync($"`{player.PlayerName}`: {text}");

        if (text.StartsWith("!"))
        {
            HandleCommand(player, text);
            return;
        }

        Broadcast(false, new ChatToClientMsg { PlayerID = msg.PlayerID, Message = text });
    }

    private void HandlePasswordCheck(IPEndPoint ep, MsgCheckPasswordToServer msg)
    {
        bool correct = !_hasPassword || msg.Password == _config.Password;
        Send(ep, true, new MsgCheckPasswordToClient
        {
            IsPasswordCorrect = correct,
            IsNobleConnectServer = false
        });
    }

    private void HandleIslandChange(IPEndPoint ep, MsgCurrentIslandToServer msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;
        if (msg.IslandUniqueID == 0) return;
        _currentIslandID = msg.IslandUniqueID;
        BroadcastIslandInfo();
    }

    private void HandleGameStateChange(IPEndPoint _ep, MsgMultiplayerGameStateToServer msg)
    {
        Broadcast(false, new MsgMultiplayerGameStateInfoToClient
        {
            MultiplayerGameState = msg.MultiplayerGameState
        });
    }

    private void BroadcastIslandInfo()
    {
        if (_currentIslandID == 0) return;
        Broadcast(true, new MsgCurrentIslandToClient { IslandUniqueID = _currentIslandID });
        Broadcast(true, new MsgIslandConfigToClient { IslandConfigUniqueID = _currentIslandID });
    }

    private void HandleFirstPersonInfo(IPEndPoint ep, MsgFirstPersonInfoToServer msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;
        _players.ResetTimeout(msg.PlayerID);

        if (!_playerStates.TryGetValue(msg.PlayerID, out var ps))
        {
            ps = new PlayerState();
            _playerStates[msg.PlayerID] = ps;
        }

        ps.PosX = msg.Position.X;
        ps.PosY = msg.Position.Y;
        ps.PosZ = msg.Position.Z;
        ps.RotX = msg.Rotation.X;
        ps.RotY = msg.Rotation.Y;
        ps.RotZ = msg.Rotation.Z;
        ps.LastActivityTime = _time;

        var relay = new MsgFirstPersonInfoToClients
        {
            PlayerIDs = [msg.PlayerID],
            Positions = [msg.Position],
            Rotations = [msg.Rotation],
            IsRoamingInFirstPersonMode = [msg.IsRoamingInFirstPersonMode],
            CharacterBytes = msg.CharacterBytes,
            PlayerIDWithCharacterBytes = msg.PlayerID
        };
        foreach (var other in _players.Players)
            if (other.PlayerID != msg.PlayerID)
                Send(other.EndPoint, false, relay);
    }

    private void HandleGeneralInfo(IPEndPoint ep, MultiplayerGeneralInfoMsgToServer msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;

        var relay = new MultiplayerGeneralInfoMsgToClient
        {
            PlayerIDs = [msg.PlayerID],
            IngameMenuStateBytes = [msg.IngameMenuStateByte],
            Elos = [msg.Elo],
            XPs = [msg.XP],
            BronzeMedals = [msg.BronzeMedals],
            SilverMedals = [msg.SilverMedals],
            GoldMedals = [msg.GoldMedals],
            MultiplayerWins = [msg.MultiplayerWins],
            DistancesDriven = [msg.DistanceDriven],
            PartsBuilt = [msg.PartsBuilt]
        };
        foreach (var other in _players.Players)
            if (other.PlayerID != msg.PlayerID)
                Send(other.EndPoint, false, relay);
    }

    private void HandleCharacterBytes(IPEndPoint ep, MsgCharacterBytesToServer msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;

        var relay = new MsgCharacterBytesToClient
        {
            PlayerID = msg.PlayerID,
            CharacterBytes = msg.CharacterBytes,
            BuildingPlatformUniqueID = msg.BuildingPlatformUniqueID
        };
        foreach (var other in _players.Players)
            if (other.PlayerID != msg.PlayerID)
                Send(other.EndPoint, false, relay);
    }

    private void HandleExcusePing(IPEndPoint ep, MsgExcusePingToServer msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player != null)
            _players.ResetTimeout(msg.PlayerID);
    }

    private void HandleGameMenuState(IPEndPoint ep, MsgGameMenuStateToServer msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;

        var relay = new MsgGameMenuStateToClient
        {
            PlayerIDs = [msg.PlayerID],
            GameMenuStates = [msg.GameMenuState]
        };
        foreach (var other in _players.Players)
            if (other.PlayerID != msg.PlayerID)
                Send(other.EndPoint, false, relay);
    }

    private void HandleCarSyncerGarage(IPEndPoint ep, MsgCarSyncerGarageToServer msg)
    {
        if (!_carData.TryGetValue(msg.PlayerID, out var cd))
        {
            cd = new CarData();
            _carData[msg.PlayerID] = cd;
        }

        if (cd.LastBaguetteLen != msg.BaguetteBytesLen || cd.HashCode != msg.HashCode)
        {
            cd.Data = new byte[msg.BaguetteBytesLen];
            cd.HashCode = msg.HashCode;
            cd.LastBaguetteLen = msg.BaguetteBytesLen;
        }

        if (msg.BytesArrayIndex + msg.Bytes.Length <= cd.Data.Length)
        {
            Array.Copy(msg.Bytes, 0, cd.Data, msg.BytesArrayIndex, msg.Bytes.Length);
        }
    }

    private void BroadcastCarData()
    {
        foreach (var kvp in _carData)
        {
            byte pid = kvp.Key;
            var cd = kvp.Value;
            if (cd.Data.Length == 0) continue;

            // Send in chunks matching the original protocol
            int maxChunk = 490;
            int offset = 0;
            while (offset < cd.Data.Length)
            {
                int chunkSize = Math.Min(maxChunk, cd.Data.Length - offset);
                var chunk = new byte[chunkSize];
                Array.Copy(cd.Data, offset, chunk, 0, chunkSize);

                var outMsg = new MsgCarSyncerGarageToClient
                {
                    PlayerID = pid,
                    BaguetteBytesLen = cd.Data.Length,
                    BytesArrayIndex = offset,
                    Bytes = chunk,
                    HashCode = cd.HashCode,
                    PlatformPositionIndex = cd.PlatformPositionIndex
                };

                foreach (var p in _players.Players)
                    Send(p.EndPoint, false, outMsg);

                offset += chunkSize;
            }
        }
    }

    // --- Car spawn / load handlers ---

    private void HandleSpawnPointPose(IPEndPoint ep, MsgSpawnPointPoseToServer msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;
        _players.ResetTimeout(msg.PlayerID);

        _playerSpawns[msg.PlayerID] = new SpawnPose
        {
            PosX = msg.SpawnPosX, PosY = msg.SpawnPosY, PosZ = msg.SpawnPosZ,
            RotX = msg.SpawnRotX, RotY = msg.SpawnRotY, RotZ = msg.SpawnRotZ
        };

    }

    private void HandleCarDataStateOfSelf(IPEndPoint ep, MsgCarDataStateOfSelfToServer msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;
        _players.ResetTimeout(msg.PlayerID);

        int bagLen = msg.BaguetteBytesLen;
        int cccLen = msg.CCCBytesLen;
        bool noData = bagLen <= 0;

        var state = new PlayerCarLoadState
        {
            CarFileName = msg.CarFileName,
            BaguetteBytesLen = bagLen,
            CCCBytesLen = cccLen,
            BaguetteData = new byte[bagLen > 0 ? bagLen : 0],
            CCCData = new byte[cccLen > 0 ? cccLen : 0],
            DataComplete = noData
        };
        _carLoadStates[msg.PlayerID] = state;

        // Request first baguette chunk
        if (bagLen > 0)
        {
            Send(ep, false, new MsgRequestCarDataToClient
            {
                PlayerID = msg.PlayerID,
                PlayerIDOfWhomCarIsRequested = msg.PlayerID,
                IsBaguetteFile = true,
                CarFileName = msg.CarFileName,
                BaguetteBytesLen = bagLen,
                CCCBytesLen = cccLen,
                BytesArrayIndex = 0
            });
        }

        Send(player.EndPoint, false, new MsgCarsLoadingStateToClient
        {
            PlayerIDs = [msg.PlayerID],
            LoadingPercentages = [100f]
        });

        _playerReadyStates[msg.PlayerID] = true;
        BroadcastReadyStates();
    }

    private void HandleCarDataToServer(IPEndPoint ep, MsgCarDataToServer msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;
        _players.ResetTimeout(msg.PlayerID);

        if (!_carLoadStates.TryGetValue(msg.PlayerID, out var state))
            return;

        bool isBag = msg.IsBaguetteFile;
        int pkgIdx = msg.BytesArrayIndex;
        int byteOffset = pkgIdx * 480;
        byte[] dest = isBag ? state.BaguetteData : state.CCCData;
        int totalLen = isBag ? state.BaguetteBytesLen : state.CCCBytesLen;

        if (byteOffset + msg.Bytes.Length > dest.Length)
            return;

        msg.Bytes.CopyTo(dest, byteOffset);

        // Forward this chunk to all other players immediately
        foreach (var p in _players.Players)
        {
            if (p.PlayerID == msg.PlayerID) continue;
            Send(p.EndPoint, false, new MsgCarDataToClient
            {
                PlayerID = msg.PlayerID,
                IsBaguetteFile = isBag,
                CarFileName = state.CarFileName,
                BaguetteBytesLen = state.BaguetteBytesLen,
                CCCBytesLen = state.CCCBytesLen,
                BytesArrayIndex = pkgIdx,
                Bytes = msg.Bytes,
                HashCode = msg.HashCode
            });
        }

        // Check if we need more chunks
        int nextPkg = pkgIdx + 1;
        if (nextPkg * 480 < totalLen)
        {
            Send(ep, false, new MsgRequestCarDataToClient
            {
                PlayerID = msg.PlayerID,
                PlayerIDOfWhomCarIsRequested = msg.PlayerID,
                IsBaguetteFile = isBag,
                CarFileName = state.CarFileName,
                BaguetteBytesLen = state.BaguetteBytesLen,
                CCCBytesLen = state.CCCBytesLen,
                BytesArrayIndex = nextPkg
            });
        }
        else if (isBag && state.CCCBytesLen > 0)
        {
            // Baguette done — start requesting CCC
            Send(ep, false, new MsgRequestCarDataToClient
            {
                PlayerID = msg.PlayerID,
                PlayerIDOfWhomCarIsRequested = msg.PlayerID,
                IsBaguetteFile = false,
                CarFileName = state.CarFileName,
                BaguetteBytesLen = state.BaguetteBytesLen,
                CCCBytesLen = state.CCCBytesLen,
                BytesArrayIndex = 0
            });
        }
        else
        {
            state.DataComplete = true;
        }
    }

    private void HandleRequestCarData(IPEndPoint ep, MsgRequestCarDataToServer msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;
        _players.ResetTimeout(msg.PlayerID);

        byte targetPid = msg.PlayerIDOfWhomCarIsRequested;

        if (!_carLoadStates.TryGetValue(targetPid, out var state))
        {
            // No car data known for this player — send empty to let client proceed
            Send(player.EndPoint, false, new MsgCarDataToClient
            {
                PlayerID = targetPid,
                IsBaguetteFile = false,
                CarFileName = "",
                BaguetteBytesLen = -1,
                CCCBytesLen = -1,
                BytesArrayIndex = -1,
                Bytes = [],
                HashCode = 0
            });
        }
        else if (state.DataComplete)
        {
            // Forward all chunks to the requesting client
            int bagPkgs = state.BaguetteBytesLen > 0 ? (state.BaguetteBytesLen - 1) / 480 + 1 : 0;
            int cccPkgs = state.CCCBytesLen > 0 ? (state.CCCBytesLen - 1) / 480 + 1 : 0;

            for (int i = 0; i < bagPkgs; i++)
            {
                int off = i * 480;
                int len = Math.Min(480, state.BaguetteBytesLen - off);
                var chunk = new byte[len];
                Array.Copy(state.BaguetteData, off, chunk, 0, len);
                Send(player.EndPoint, false, new MsgCarDataToClient
                {
                    PlayerID = targetPid,
                    IsBaguetteFile = true,
                    CarFileName = state.CarFileName,
                    BaguetteBytesLen = state.BaguetteBytesLen,
                    CCCBytesLen = state.CCCBytesLen,
                    BytesArrayIndex = i,
                    Bytes = chunk,
                    HashCode = 0
                });
            }
            for (int i = 0; i < cccPkgs; i++)
            {
                int off = i * 480;
                int len = Math.Min(480, state.CCCBytesLen - off);
                var chunk = new byte[len];
                Array.Copy(state.CCCData, off, chunk, 0, len);
                Send(player.EndPoint, false, new MsgCarDataToClient
                {
                    PlayerID = targetPid,
                    IsBaguetteFile = false,
                    CarFileName = state.CarFileName,
                    BaguetteBytesLen = state.BaguetteBytesLen,
                    CCCBytesLen = state.CCCBytesLen,
                    BytesArrayIndex = i,
                    Bytes = chunk,
                    HashCode = 0
                });
            }
        }
        else
        {
            // Data not complete yet — forward request to car owner
            var targetPlayer = _players.GetPlayerByID(targetPid);
            if (targetPlayer != null)
            {
                Send(targetPlayer.EndPoint, false, new MsgRequestCarDataToClient
                {
                    PlayerID = targetPid,
                    PlayerIDOfWhomCarIsRequested = targetPid,
                    IsBaguetteFile = msg.IsBaguetteFile,
                    CarFileName = msg.CarFileName,
                    BaguetteBytesLen = msg.BaguetteBytesLen,
                    CCCBytesLen = msg.CCCBytesLen,
                    BytesArrayIndex = msg.BytesArrayIndex
                });
            }
        }

        var loadingMsg = new MsgCarsLoadingStateToClient
        {
            PlayerIDs = [targetPid],
            LoadingPercentages = [100f]
        };
        Send(player.EndPoint, false, loadingMsg);

        _playerReadyStates[msg.PlayerID] = true;
        BroadcastReadyStates();
    }

    private void HandleReverse(IPEndPoint ep, ReverseMsgToServer msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;
        _players.ResetTimeout(msg.PlayerID);

        var relay = new ReverseMsgToClient
        {
            PlayerIDs = [msg.PlayerID],
            AreReversing = [msg.IsReversing]
        };
        foreach (var other in _players.Players)
            if (other.PlayerID != msg.PlayerID)
                Send(other.EndPoint, false, relay);
    }

    private void HandleShiftGear(IPEndPoint ep, ShiftGearServerMsg msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;
        _players.ResetTimeout(msg.PlayerID);

        var relay = new ShiftGearClientMsg
        {
            PlayerIDs = [msg.PlayerID],
            IsManuallyShifting = [msg.IsManuallyShifting],
            Gear = [msg.Gear]
        };
        foreach (var other in _players.Players)
            if (other.PlayerID != msg.PlayerID)
                Send(other.EndPoint, false, relay);
    }

    private void HandleReadyup(IPEndPoint ep, MsgReadyupToServer msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;
        _players.ResetTimeout(msg.PlayerID);

        _playerReadyStates[msg.PlayerID] = msg.IsPlayerReady;

        // Broadcast player ready state
        BroadcastReadyStates();
    }

    private void BroadcastReadyStates()
    {
        var allPlayers = _players.Players;
        var ids = new byte[allPlayers.Count];
        var readyStates = new bool[allPlayers.Count];
        for (int i = 0; i < allPlayers.Count; i++)
        {
            ids[i] = allPlayers[i].PlayerID;
            readyStates[i] = _playerReadyStates.GetValueOrDefault(allPlayers[i].PlayerID, false);
        }

        Broadcast(false, new MsgPlayersReadyListToClient
        {
            PlayerIDs = ids,
            PlayersReadyStates = readyStates,
            WaitTimeAllAreReadyCounter = -1f
        });

        // Send MsgAllPlayersThatAreReadyToClient with IDs of ready players
        var readyIds = new List<byte>();
        foreach (var p in allPlayers)
            if (_playerReadyStates.GetValueOrDefault(p.PlayerID, false))
                readyIds.Add(p.PlayerID);

        Broadcast(false, new MsgAllPlayersThatAreReadyToClient
        {
            PlayerIDsReady = readyIds.ToArray()
        });
    }

    private void HandleRepair(IPEndPoint ep, RepairMsg msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;
        _players.ResetTimeout(msg.PlayerID);

        Broadcast(true, new RepairClientMsg
        {
            PlayerID = msg.PlayerID,
            IsRepairInsteadOfReset = msg.IsRepairInsteadOfReset
        });
    }

    private void HandleDestroyParts(IPEndPoint ep, MsgDestroyPartsToServer msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;
        _players.ResetTimeout(msg.PlayerID);

        // Broadcast DestroyedPartsMsg (ID 11) - the message the client actually listens for
        Broadcast(false, new DestroyedPartsMsg
        {
            PlayerID = msg.PlayerID,
            InstPartIDs = msg.DestroyedPartsIDs,
            ImpactVector = new Vec3(0, 0, 0),
            PlayerIDCollidingWith = byte.MaxValue,
            DamagePosWorld = new Vec3(0, 0, 0)
        });
    }

    private void HandleSelfCarState(IPEndPoint ep, SelfCarStateMsg msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;
        _players.ResetTimeout(msg.PlayerID);

        // CarStateMode: 0=RESET_GARAGE, 1=REPAIR, 2=RESET_TRACK
        if (msg.CarStateMode == 0 || msg.CarStateMode == 1)
        {
            // Player reset/repaired their car - restore health
            if (_playerStates.TryGetValue(msg.PlayerID, out var ps))
            {
                ps.Health = 100f;
            }
        }
    }

    private void HandlePerformHorn(IPEndPoint ep, MsgPerformHornToServer msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;
        _players.ResetTimeout(msg.PlayerID);

        var relay = new MsgPerformHornToClient
        {
            PlayerID = msg.PlayerID,
            SoundIndex = msg.SoundIndex,
            PartInstID = msg.PartInstID,
            WorldPosition = msg.WorldPosition
        };
        foreach (var other in _players.Players)
            if (other.PlayerID != msg.PlayerID)
                Send(other.EndPoint, false, relay);
    }

    private void HandleSelfReady(IPEndPoint ep, MsgSelfReadyToServer msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;
        _players.ResetTimeout(msg.PlayerID);

        _playerReadyStates[msg.PlayerID] = msg.IsSelfReady;
        BroadcastReadyStates();
    }

    private static void HandleCommandMsg(IPEndPoint _ep, MsgCommandToServer _msg) { }

    private void HandleSetSpawnPoint(IPEndPoint ep, SetSpawnPointMsgToServer msg)
    {
        var player = _players.GetPlayerByID(msg.PlayerID);
        if (player == null) return;
        _players.ResetTimeout(msg.PlayerID);

        _playerSpawns[msg.PlayerID] = new SpawnPose
        {
            PosX = 0, PosY = 0, PosZ = 0,
            RotX = 0, RotY = 0, RotZ = 0
        };

        var relay = new SetSpawnPointMsgToClient
        {
            PlayerID = msg.PlayerID,
            SpawnPointIndex = msg.SpawnPointIndex,
            FreeDriveSpawnPointUniqueID = msg.FreeDriveSpawnPointUniqueID
        };
        Broadcast(false, relay);
    }

    private void HandleCommand(Player player, string cmd)
    {
        var parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        var args = parts[1..];
        var cmdName = parts[0].ToLowerInvariant();

        bool isAdmin = _config.AdminSteamIds.Contains(player.CSteamID);

        switch (cmdName)
        {
            case "!help":
                SendChat(player.EndPoint, "Commands: !help !players !ping !votemap !yes !no !break !damage !status" +
                    (isAdmin ? " !kick !ban !unban !slay !say !setname !setmap !reload" : ""));
                break;

            case "!players":
                var list = string.Join(", ", _players.Players.Select(p => $"#{p.PlayerID} {p.PlayerName}"));
                SendChat(player.EndPoint, $"Players ({_players.PlayerCount}): {list}");
                break;

            case "!ping":
                SendChat(player.EndPoint, $"Your ping: {player.Ping:F0}ms");
                break;

            case "!votemap":
                if (_voteActive)
                {
                    SendChat(player.EndPoint, "A vote is already in progress");
                    break;
                }
                if (args.Length == 0)
                {
                    var mapList = string.Join(", ", _config.VotableMaps.Select((m, i) => $"{i}:{m.Name}"));
                    if (string.IsNullOrEmpty(mapList))
                        mapList = "No votable maps configured";
                    SendChat(player.EndPoint, $"Maps: {mapList}");
                    break;
                }
                var mapEntry = ResolveMap(args[0]);
                if (mapEntry == null)
                {
                    SendChat(player.EndPoint, "Map not found. Use !votemap to list maps");
                    break;
                }
                _voteActive = true;
                _voteTargetIsland = mapEntry.Id;
                _voteEndTime = _time + 30f;
                _votes.Clear();
                _votes[player.PlayerID] = true;
                var msg = $"Vote: Change map to {mapEntry.Name}? Type !yes or !no (30s)";
                Broadcast(false, new ChatToClientMsg { PlayerID = byte.MaxValue, Message = msg });
                break;

            case "!yes":
                if (_voteActive)
                {
                    _votes[player.PlayerID] = true;
                    SendChat(player.EndPoint, "Voted yes");
                }
                else
                    SendChat(player.EndPoint, "No active vote");
                break;

            case "!no":
                if (_voteActive)
                {
                    _votes[player.PlayerID] = false;
                    SendChat(player.EndPoint, "Voted no");
                }
                else
                    SendChat(player.EndPoint, "No active vote");
                break;

            case "!damage":
                if (args.Length > 0 && float.TryParse(args[0], out var dmg))
                {
                    ApplyDamageFactor(dmg);
                    SendChat(player.EndPoint, $"CarsDamageFactor set to {dmg}");
                }
                else
                {
                    var current = BitConverter.ToSingle(_serverSettings, 0);
                    SendChat(player.EndPoint, $"Current CarsDamageFactor: {current}");
                }
                break;

            case "!break":
                {
                    SendChat(player.EndPoint, "Breaking your car!");
                    Send(player.EndPoint, true, new RepairClientMsg
                    {
                        PlayerID = player.PlayerID,
                        IsRepairInsteadOfReset = false
                    });
                    if (_playerStates.TryGetValue(player.PlayerID, out var breakPs))
                        breakPs.Health = 100f;
                }
                break;

            case "!kick":
                if (!isAdmin) { SendChat(player.EndPoint, "Admin required"); break; }
                if (args.Length > 0 && byte.TryParse(args[0], out var kid))
                {
                    var target = _players.GetPlayerByID(kid);
                    if (target != null)
                    {
                        _log.Info($"Admin {player.PlayerName} kicked {target.PlayerName}");
                        _ = SendWebhookAsync($"**{player.PlayerName}** kicked **{target.PlayerName}**");
                        SendChat(target.EndPoint, "Kicked by admin");
                        _players.RemovePlayer(kid);
                        BroadcastPlayerList();
                    }
                }
                break;

            case "!ban":
                if (!isAdmin) { SendChat(player.EndPoint, "Admin required"); break; }
                if (args.Length > 0)
                {
                    if (byte.TryParse(args[0], out var banId))
                    {
                        var target = _players.GetPlayerByID(banId);
                        if (target != null)
                        {
                            _bans.BanSteamId(target.CSteamID);
                            _bans.BanIP(target.EndPoint.Address);
                            _log.Info($"Admin {player.PlayerName} banned {target.PlayerName}");
                            _ = SendWebhookAsync($"**{player.PlayerName}** banned **{target.PlayerName}**");
                            SendChat(target.EndPoint, "You have been banned");
                            _players.RemovePlayer(banId);
                            BroadcastPlayerList();
                            SendChat(player.EndPoint, $"Banned {target.PlayerName}");
                        }
                    }
                }
                break;

            case "!unban":
                if (!isAdmin) { SendChat(player.EndPoint, "Admin required"); break; }
                if (args.Length > 0 && _bans.Unban(args[0]))
                    SendChat(player.EndPoint, $"Unbanned {args[0]}");
                else
                    SendChat(player.EndPoint, $"Not found: {args[0]}");
                break;

            case "!slay":
                if (!isAdmin) { SendChat(player.EndPoint, "Admin required"); break; }
                if (args.Length > 0 && byte.TryParse(args[0], out var slayId))
                {
                    var target = _players.GetPlayerByID(slayId);
                    if (target != null)
                    {
                        _log.Info($"Admin {player.PlayerName} slayed {target.PlayerName}");
                        _ = SendWebhookAsync($"**{player.PlayerName}** slayed **{target.PlayerName}**");
                        Send(target.EndPoint, true, new RepairClientMsg
                        {
                            PlayerID = slayId,
                            IsRepairInsteadOfReset = false
                        });
                    }
                }
                break;

            case "!say":
                if (!isAdmin) { SendChat(player.EndPoint, "Admin required"); break; }
                if (args.Length > 0)
                {
                    var text = string.Join(" ", args);
                    _log.Info($"Admin {player.PlayerName}: {text}");
                    _ = SendWebhookAsync($"**{player.PlayerName}**: *{text}*");
                    Broadcast(false, new ChatToClientMsg { PlayerID = byte.MaxValue, Message = $"[Admin] {text}" });
                }
                break;

            case "!setname":
                if (!isAdmin) { SendChat(player.EndPoint, "Admin required"); break; }
                _config.ServerName = string.Join(" ", args);
                _steam?.UpdateServerDetails(_config.ServerName, _config.MaxPlayers, "", _hasPassword);
                Broadcast(false, new ChatToClientMsg { PlayerID = byte.MaxValue, Message = $"Server name: {_config.ServerName}" });
                break;

            case "!setmap":
                if (!isAdmin) { SendChat(player.EndPoint, "Admin required"); break; }
                if (args.Length > 0)
                {
                    var setMap = ResolveMap(args[0]);
                    if (setMap != null)
                    {
                        _currentIslandID = setMap.Id;
                        BroadcastIslandInfo();
                        Broadcast(false, new ChatToClientMsg { PlayerID = byte.MaxValue, Message = $"[Server] Map changed to {setMap.Name}" });
                        _ = SendWebhookAsync($"**{player.PlayerName}** changed map to `{setMap.Name}`");
                    }
                    else
                    {
                        var mapList = string.Join(", ", _config.VotableMaps.Select((m, i) => $"{i}:{m.Name}"));
                        if (string.IsNullOrEmpty(mapList))
                            mapList = "PULAU_MAHKOTA=-801448567, SUPERSONIC=-839216305, RACETRACK=-487119212, OFFROAD=-396930304";
                        SendChat(player.EndPoint, $"Maps: {mapList}");
                    }
                }
                break;

            case "!reload":
                if (!isAdmin) { SendChat(player.EndPoint, "Admin required"); break; }
                _config.Save();
                SendChat(player.EndPoint, "Config saved");
                break;

            case "!status":
                SendChat(player.EndPoint, $"Players: {_players.PlayerCount}/{_config.MaxPlayers}  Map: {_currentIslandID}  Uptime: {_time:F0}s");
                break;

            default:
                SendChat(player.EndPoint, $"Unknown: {parts[0]}. Type !help");
                break;
        }
    }

    private MapEntry? ResolveMap(string input)
    {
        // Try by index in VotableMaps
        if (int.TryParse(input, out var idx) && idx >= 0 && idx < _config.VotableMaps.Length)
            return _config.VotableMaps[idx];

        // Try by name (case-insensitive)
        foreach (var m in _config.VotableMaps)
        {
            if (string.Equals(m.Name, input, StringComparison.OrdinalIgnoreCase))
                return m;
        }

        // Try by ID
        if (int.TryParse(input, out var id))
        {
            foreach (var m in _config.VotableMaps)
            {
                if (m.Id == id)
                    return m;
            }
        }

        return null;
    }

    // --- Console commands ---

    private void HandleConsoleCommand(string line)
    {
        line = line.Trim();
        if (line.Length == 0) return;
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        switch (parts[0].ToLowerInvariant())
        {
            case "help":
                Console.WriteLine("Commands: help, players, say, kick, ban <id>, unban <val>, bans, setname, setmap, damage, status, save, reload, quit");
                break;
            case "players":
                Console.WriteLine($"Players ({_players.PlayerCount}):");
                foreach (var p in _players.Players)
                    Console.WriteLine($"  #{p.PlayerID} {p.PlayerName} ping={p.Ping:F0}ms {p.EndPoint}");
                break;
            case "say":
                if (parts.Length > 1)
                {
                    var text = string.Join(" ", parts[1..]);
                    _log.Info($"[Server] {text}");
                    Broadcast(false, new ChatToClientMsg { PlayerID = byte.MaxValue, Message = $"[Server] {text}" });
                }
                break;
            case "kick":
                if (parts.Length > 1 && byte.TryParse(parts[1], out var kid))
                {
                    var target = _players.GetPlayerByID(kid);
                    if (target != null)
                    {
                        _log.Info($"Kicking {target.PlayerName}");
                        _players.RemovePlayer(kid);
                        BroadcastPlayerList();
                    }
                }
                break;
            case "ban":
                if (parts.Length > 1 && byte.TryParse(parts[1], out var banId))
                {
                    var target = _players.GetPlayerByID(banId);
                    if (target != null)
                    {
                        _bans.BanSteamId(target.CSteamID);
                        _bans.BanIP(target.EndPoint.Address);
                        _log.Info($"Banned {target.PlayerName}");
                        _players.RemovePlayer(banId);
                        BroadcastPlayerList();
                    }
                }
                break;
            case "unban":
                if (parts.Length > 1 && _bans.Unban(parts[1]))
                    Console.WriteLine($"Unbanned {parts[1]}");
                else
                    Console.WriteLine($"Not found: {parts[1]}");
                break;
            case "bans":
                foreach (var b in _bans.ListBans())
                    Console.WriteLine($"  {b}");
                break;
            case "damage":
                if (parts.Length > 1 && float.TryParse(parts[1], out var dmgVal))
                {
                    ApplyDamageFactor(dmgVal);
                    Console.WriteLine($"CarsDamageFactor set to {dmgVal}");
                }
                else
                {
                    var current = BitConverter.ToSingle(_serverSettings, 0);
                    Console.WriteLine($"Current CarsDamageFactor: {current}");
                }
                break;
            case "status":
                Console.WriteLine($"Uptime: {_time:F0}s  Players: {_players.PlayerCount}/{_config.MaxPlayers}  Map ID: {_currentIslandID}");
                break;
            case "setname":
                if (parts.Length > 1)
                {
                    _config.ServerName = string.Join(" ", parts[1..]);
                    _steam?.UpdateServerDetails(_config.ServerName, _config.MaxPlayers, "", _hasPassword);
                    _log.Info($"Server name: {_config.ServerName}");
                }
                break;
            case "setmap":
                if (parts.Length > 1 && int.TryParse(parts[1], out var mapId))
                {
                    _currentIslandID = mapId;
                    BroadcastIslandInfo();
                    _log.Info($"Map changed to ID {mapId}");
                }
                else
                    Console.WriteLine("Known maps: PULAU_MAHKOTA=55524842, SUPERSONIC=-2137810770");
                break;
            case "save":
            case "reload":
                _config.Save();
                Console.WriteLine("Config saved");
                break;
            case "quit":
            case "exit":
                Console.WriteLine("Shutting down...");
                Environment.Exit(0);
                break;
            default:
                Console.WriteLine($"Unknown: {parts[0]}");
                break;
        }
    }

    // --- Send helpers ---

    public void Send(IPEndPoint target, bool reliable, DataObject msg)
    {
        var bytes = msg.ToBytes();
        if (reliable)
            _proto.SendReliable(target, bytes);
        else
            _proto.SendUnreliable(target, bytes);
    }

    public void Broadcast(bool reliable, DataObject msg)
    {
        var bytes = msg.ToBytes();
        foreach (var p in _players.Players)
        {
            if (reliable)
                _proto.SendReliable(p.EndPoint, bytes);
            else
                _proto.SendUnreliable(p.EndPoint, bytes);
        }
    }

    public void SendChat(IPEndPoint target, string text)
    {
        Send(target, false, new ChatToClientMsg { PlayerID = byte.MaxValue, Message = text });
    }

    private void BroadcastPlayerList()
    {
        var pl = _players.Players;
        var ids = new byte[pl.Count];
        var names = new string[pl.Count];
        var sids = new ulong[pl.Count];
        var pings = new float[pl.Count];
        for (int i = 0; i < pl.Count; i++)
        {
            ids[i] = pl[i].PlayerID;
            names[i] = pl[i].PlayerName;
            sids[i] = pl[i].CSteamID;
            pings[i] = pl[i].Ping;
        }
        Broadcast(false, new PlayerList { PlayerIDs = ids, PlayerNames = names, CSteamIDs = sids, Pings = pings });
    }

    private void BroadcastServerInfo()
    {
        Broadcast(false, new ServerInfoMsg
        {
            MaxPlayers = (ushort)_config.MaxPlayers,
            ServerName = _config.ServerName,
            Password = _hasPassword ? _config.Password : ""
        });
    }

    private void BroadcastGlobalTime()
    {
        Broadcast(false, new MsgGlobalTime { GlobalTime = _globalTime });
    }

    private byte[] MakeDefaultServerSettings()
    {
        var bytes = new byte[17];
        BitConverter.GetBytes(1.0f).CopyTo(bytes, 0);       // CarsDamageFactor (float, LE)
        bytes[4] = 0;                                       // AllowedCarClass index
        bytes[5] = 1;                                       // IsOtherPlayersSpectatingAllowed
        BitConverter.GetBytes(1000).CopyTo(bytes, 6);       // MaxAmountOfParts (int, LE)
        bytes[10] = (byte)_config.MaxPlayers;                    // MaxAmountOfPlayers
        BitConverter.GetBytes(0).CopyTo(bytes, 11);         // GameMode (int, LE)
        BitConverter.GetBytes((ushort)0).CopyTo(bytes, 15); // TimeInSecondsBetweenRaces (ushort, LE)
        return bytes;
    }

    private void BroadcastServerSettings()
    {
        Broadcast(true, new MsgServerSettingsToClient { SerializedServerSettings = _serverSettings });
    }

    private void ApplyDamageFactor(float factor)
    {
        BitConverter.GetBytes(factor).CopyTo(_serverSettings, 0);
        _log.Info($"CarsDamageFactor set to {factor}");
        _ = SendWebhookAsync($"Damage factor set to `{factor}`");
        BroadcastServerSettings();
    }

    private void ResolveVote()
    {
        _voteActive = false;
        int yesCount = 0, noCount = 0;
        foreach (var kvp in _votes)
        {
            if (kvp.Value) yesCount++; else noCount++;
        }
        int total = _players.PlayerCount;
        if (total > 0 && yesCount > total / 2)
        {
            _currentIslandID = _voteTargetIsland;
            BroadcastIslandInfo();
            _log.Info($"Vote passed: map changed to {_voteTargetIsland}");
            Broadcast(false, new ChatToClientMsg { PlayerID = byte.MaxValue, Message = $"Vote passed! Map changed to ID {_voteTargetIsland}" });
            _ = SendWebhookAsync($"Vote passed! Map changed to `{_voteTargetIsland}`");
        }
        else
        {
            Broadcast(false, new ChatToClientMsg { PlayerID = byte.MaxValue, Message = $"Vote failed ({yesCount} yes, {noCount} no)" });
        }
        _votes.Clear();
    }

    private async Task SendWebhookAsync(string message)
    {
        if (string.IsNullOrEmpty(_config.DiscordWebhookUrl)) return;
        try
        {
            var payload = JsonSerializer.Serialize(new { content = message });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            _ = await _http.PostAsync(_config.DiscordWebhookUrl, content);
        }
        catch { }
    }

    private void BroadcastCarDataStateAllPlayers()
    {
        var pl = _players.Players;
        var ids = new List<byte>();
        var names = new List<string>();
        var baguetteLens = new List<int>();
        var cccLens = new List<int>();
        var upToDate = new List<bool>();

        foreach (var p in pl)
        {
            ids.Add(p.PlayerID);
            if (_carLoadStates.TryGetValue(p.PlayerID, out var state))
            {
                names.Add(state.CarFileName);
                baguetteLens.Add(state.BaguetteBytesLen);
                cccLens.Add(state.CCCBytesLen);
                upToDate.Add(state.DataComplete);
            }
            else
            {
                names.Add("");
                baguetteLens.Add(-1);
                cccLens.Add(-1);
                upToDate.Add(false);
            }
        }

        Broadcast(false, new MsgCarDataStateAllPlayersToClient
        {
            PlayerIDs = ids.ToArray(),
            CarFileNames = names.ToArray(),
            BaguetteBytesLen = baguetteLens.ToArray(),
            CCCBytesLen = cccLens.ToArray(),
            IsServerDataUpToDate = upToDate.ToArray()
        });
    }

    private void BroadcastAllRigsInfo()
    {
        if (_players.PlayerCount < 2) return;

        var players = _players.Players;
        float globalTime = _globalTime;

        // Build per-player transform collections
        var allPlayerIDs = new List<byte>();
        var allRigIDs = new List<ushort>();
        var allPositions = new List<Vec3>();
        var allRotations = new List<Vec3>();
        var allVelocities = new List<Vec3>();
        var allAngVelocities = new List<Vec3>();

        foreach (var p in players)
        {
            if (!_playerStates.TryGetValue(p.PlayerID, out var ps))
                continue;
            int count = ps.TransformIDs.Length;
            for (int i = 0; i < count; i++)
            {
                allPlayerIDs.Add(p.PlayerID);
                allRigIDs.Add((ushort)i);
                allPositions.Add(new Vec3(
                    i < ps.PosXArr.Length ? ps.PosXArr[i] : ps.PosX,
                    i < ps.PosYArr.Length ? ps.PosYArr[i] : ps.PosY,
                    i < ps.PosZArr.Length ? ps.PosZArr[i] : ps.PosZ));
                allRotations.Add(new Vec3(
                    i < ps.RotXArr.Length ? ps.RotXArr[i] : ps.RotX,
                    i < ps.RotYArr.Length ? ps.RotYArr[i] : ps.RotY,
                    i < ps.RotZArr.Length ? ps.RotZArr[i] : ps.RotZ));
                allVelocities.Add(new Vec3(
                    i < ps.VelXArr.Length ? ps.VelXArr[i] : ps.VelX,
                    i < ps.VelYArr.Length ? ps.VelYArr[i] : ps.VelY,
                    i < ps.VelZArr.Length ? ps.VelZArr[i] : ps.VelZ));
                allAngVelocities.Add(new Vec3(0, 0, 0));
            }
        }

        // Send to each client, excluding their own data (original server filters this)
        var ownIDs = new HashSet<byte>();
        foreach (var target in players)
        {
            ownIDs.Clear();
            ownIDs.Add(target.PlayerID);

            var pIDs = new List<byte>();
            var rIDs = new List<ushort>();
            var pos = new List<Vec3>();
            var rot = new List<Vec3>();
            var vel = new List<Vec3>();
            var ang = new List<Vec3>();

            for (int i = 0; i < allPlayerIDs.Count; i++)
            {
                if (ownIDs.Contains(allPlayerIDs[i]))
                    continue;
                pIDs.Add(allPlayerIDs[i]);
                rIDs.Add(allRigIDs[i]);
                pos.Add(allPositions[i]);
                rot.Add(allRotations[i]);
                vel.Add(allVelocities[i]);
                ang.Add(allAngVelocities[i]);
            }

            if (pIDs.Count == 0) continue;

            Send(target.EndPoint, false, new AllRigsInfoMsg
            {
                PlayerIDs = pIDs.ToArray(),
                RigidbodyIDs = rIDs.ToArray(),
                Positions = pos.ToArray(),
                Rotations = rot.ToArray(),
                Velocities = vel.ToArray(),
                AngularVelocities = ang.ToArray(),
                GlobalTime = globalTime
            });
        }
    }

    private void BroadcastAllPlayerCars()
    {
        var pl = _players.Players;
        var ids = new byte[pl.Count];
        var names = new string[pl.Count];
        for (int i = 0; i < pl.Count; i++)
        {
            ids[i] = pl[i].PlayerID;
            if (_carLoadStates.TryGetValue(pl[i].PlayerID, out var state))
                names[i] = state.CarFileName;
            else
                names[i] = "";
        }

        Broadcast(false, new AllPlayerCars
        {
            PlayerIDs = ids,
            CarFileNames = names
        });
    }

    public void Dispose()
    {
        _api?.Dispose();
        _steam?.Dispose();
        _matchmakingPinger.Dispose();
        _preConnect?.Dispose();
        _proto.Dispose();
        _http.Dispose();
        _log.Dispose();
    }

    // --- Public API methods ---

    public object ApiGetStats() => new
    {
        players = _players.PlayerCount,
        maxPlayers = _config.MaxPlayers,
        serverName = _config.ServerName,
        mapId = _currentIslandID,
        uptime = _time,
        hasPassword = _hasPassword
    };

    public object ApiGetPlayers() => _players.Players.Select(p => new
    {
        id = p.PlayerID,
        name = p.PlayerName,
        steamId = p.CSteamID,
        ping = p.Ping
    }).ToList();

    public object ApiGetPlayerPositions() => _players.Players.ToArray().Select(p =>
    {
        _playerStates.TryGetValue(p.PlayerID, out var ps);
        return new
        {
            id = p.PlayerID,
            pos = new { x = ps?.PosX ?? 0, y = ps?.PosY ?? 0, z = ps?.PosZ ?? 0 },
            rot = new { x = ps?.RotX ?? 0, y = ps?.RotY ?? 0, z = ps?.RotZ ?? 0 },
            vel = new { x = ps?.VelX ?? 0, y = ps?.VelY ?? 0, z = ps?.VelZ ?? 0 }
        };
    }).ToList();

    public object ApiGetConfig() => new
    {
        port = _config.Port,
        maxPlayers = _config.MaxPlayers,
        serverName = _config.ServerName,
        hasPassword = _hasPassword,
        steamQueryPort = _config.SteamQueryPort,
        idleKickMinutes = _config.IdleKickMinutes,
        mapRotationIntervalMinutes = _config.MapRotationIntervalMinutes,
        mapRotationIslands = _config.MapRotationIslands,
        votableMaps = _config.VotableMaps,
        joinMessage = _config.JoinMessage
    };

    public void ApiKick(byte playerId)
    {
        var target = _players.GetPlayerByID(playerId);
        if (target != null)
        {
            _log.Info($"[API] Kicked {target.PlayerName}");
            _players.RemovePlayer(playerId);
            BroadcastPlayerList();
        }
    }

    public void ApiBan(byte playerId)
    {
        var target = _players.GetPlayerByID(playerId);
        if (target != null)
        {
            _bans.BanSteamId(target.CSteamID);
            _bans.BanIP(target.EndPoint.Address);
            _log.Info($"[API] Banned {target.PlayerName}");
            _players.RemovePlayer(playerId);
            BroadcastPlayerList();
        }
    }

    public void ApiSlay(byte playerId)
    {
        var target = _players.GetPlayerByID(playerId);
        if (target != null)
        {
            Send(target.EndPoint, true, new RepairClientMsg
            {
                PlayerID = playerId,
                IsRepairInsteadOfReset = false
            });
        }
    }

    public void ApiSetMap(int mapId)
    {
        _currentIslandID = mapId;
        BroadcastIslandInfo();
        _log.Info($"[API] Map changed to {mapId}");
    }

    public void ApiSay(string message)
    {
        _log.Info($"[API] Say: {message}");
        Broadcast(false, new ChatToClientMsg { PlayerID = byte.MaxValue, Message = $"[Server] {message}" });
    }

    public void ApiSetName(string name)
    {
        _config.ServerName = name;
        _steam?.UpdateServerDetails(_config.ServerName, _config.MaxPlayers, "", _hasPassword);
    }

    public void ApiDamage(float factor)
    {
        ApplyDamageFactor(factor);
    }
}
