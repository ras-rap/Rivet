using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Steamworks;
using Steamworks.Data;

namespace Rivet.Server;

public class SteamServerManager : IDisposable
{
    private bool _initialized;
    private bool _loggedOn;
    private bool _connected;
    private readonly string _version;
    private readonly string _serverName;
    private readonly int _maxPlayers;
    private readonly bool _hasPassword;
    private string _mapName = "Pulau Mahkota";
    private float _statusLogTimer;
    private readonly ushort _gamePort;
    private readonly ushort _queryPort;
    private System.Net.Sockets.Socket? _querySocket;
    private readonly byte[] _recvBuffer = new byte[4096];
    private readonly Dictionary<IPEndPoint, DateTime> _rateLimit = new();

    private static readonly PropertyInfo? _gameDescriptionProp = typeof(SteamServer).GetProperty("GameDescription", BindingFlags.Public | BindingFlags.Static);

    public bool LoggedOn => _loggedOn;
    public bool Connected => _connected;

    public SteamServerManager(ushort gamePort, ushort queryPort, string serverName, int maxPlayers, bool hasPassword, string steamVersion)
    {
        _version = steamVersion;
        _serverName = serverName;
        _maxPlayers = maxPlayers;
        _hasPassword = hasPassword;
        _gamePort = gamePort;
        _queryPort = queryPort;

        SteamServer.OnSteamServersConnected += OnSteamServersConnected;
        SteamServer.OnSteamServersDisconnected += OnSteamServersDisconnected;
        SteamServer.OnSteamServerConnectFailure += OnSteamServerConnectFailure;

        try
        {
            var init = new SteamServerInit
            {
                DedicatedServer = true,
                VersionString = steamVersion,
                ModDir = "street_mc",
                GameDescription = MakeGameDescription(0, maxPlayers),
                GamePort = gamePort,
                QueryPort = queryPort
            };

            SteamServer.Init((AppId)1279510u, init, false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Steam] Failed to initialize Steam Game Server: {ex.Message}");
            return;
        }

        _initialized = true;

        SteamServer.AdvertiseServer = true;

        Console.WriteLine($"[Steam] Server name=\"{_serverName}\", maxPlayers={_maxPlayers}, map={_mapName}");
        Console.WriteLine($"[Steam] AdvertiseServer set to true");

        // On Linux, Steam may not create the query socket automatically.
        // We create our own socket on the query port and manually forward
        // A2S server queries to/from Steam.
        try
        {
            _querySocket = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _querySocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _querySocket.Bind(new IPEndPoint(IPAddress.Any, queryPort));
            _querySocket.Blocking = false;
            Console.WriteLine($"[Steam] Query socket bound to UDP {queryPort}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Steam] WARNING: Could not bind query socket on UDP {queryPort}: {ex.Message}");
            Console.WriteLine($"[Steam]   Server may not appear in Steam server browser");
        }

        RunCallbacks();

        Console.WriteLine($"[Steam] Calling LogOnAnonymous...");
        SteamServer.LogOnAnonymous();

        Console.WriteLine($"[Steam] Server initialized on port {gamePort} (query {queryPort}) v{steamVersion}");
    }

    public void Tick()
    {
        if (!_initialized) return;

        PollQuerySocket();
        RunCallbacks();

        _statusLogTimer += 0.016f;
        if (_statusLogTimer >= 30f)
        {
            _statusLogTimer = 0f;
            Console.WriteLine($"[Steam] Status — LoggedOn={SteamServer.LoggedOn}, Connected={_connected}, Players={SteamServer.MaxPlayers - SteamServer.BotCount}/{SteamServer.MaxPlayers}, Map={SteamServer.MapName}");

            // Check if query socket is responsive
            if (_querySocket != null && _querySocket.IsBound)
                Console.WriteLine($"[Steam]   Query socket OK on UDP {_queryPort}, PublicIp={SteamServer.PublicIp}");
        }
    }

    private void PollQuerySocket()
    {
        if (_querySocket == null || !_querySocket.IsBound)
            return;

        while (_querySocket.Poll(0, SelectMode.SelectRead))
        {
            try
            {
                EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                int len = _querySocket.ReceiveFrom(_recvBuffer, 0, _recvBuffer.Length, SocketFlags.None, ref sender);
                if (len <= 0) continue;

                var ep = (IPEndPoint)sender;

                RateLimit(ep);

                SteamServer.HandleIncomingPacket(_recvBuffer, len, BitConverter.ToUInt32(ep.Address.GetAddressBytes(), 0), (ushort)ep.Port);
            }
            catch (Exception)
            {
                break;
            }
        }

        while (SteamServer.GetOutgoingPacket(out var packet))
        {
            try
            {
                var target = new IPEndPoint(new IPAddress(BitConverter.GetBytes(packet.Address)), packet.Port);
                _querySocket.SendTo(packet.Data, 0, packet.Size, SocketFlags.None, target);
            }
            catch (Exception)
            {
                break;
            }
        }
    }

    private void RateLimit(IPEndPoint ep)
    {
        var now = DateTime.UtcNow;
        if (_rateLimit.TryGetValue(ep, out var last))
        {
            if ((now - last).TotalMilliseconds < 100)
                return;
        }
        _rateLimit[ep] = now;
    }

    private static void RunCallbacks()
    {
        SteamServer.RunCallbacks();
    }

    private void OnSteamServersConnected()
    {
        _connected = true;
        _loggedOn = true;

        SteamServer.ServerName = _serverName;
        SteamServer.MaxPlayers = _maxPlayers;
        SteamServer.MapName = _mapName;
        SteamServer.Passworded = _hasPassword;
        SteamServer.BotCount = 0;

        Console.WriteLine($"[Steam] SUCCESS: Connected to Steam master server");
        Console.WriteLine($"[Steam]   SteamId={SteamServer.SteamId}, Name=\"{SteamServer.ServerName}\", Map={SteamServer.MapName}, MaxPlayers={SteamServer.MaxPlayers}");
        Console.WriteLine($"[Steam]   PublicIp={SteamServer.PublicIp}, QueryPort={_queryPort}");
    }

    private void OnSteamServersDisconnected(Result result)
    {
        _connected = false;
        Console.WriteLine($"[Steam] Disconnected from Steam master server (result={result})");
    }

    private void OnSteamServerConnectFailure(Result result, bool stillRetrying)
    {
        _connected = false;
        Console.WriteLine($"[Steam] FAILED: Could not connect to Steam master server (result={result}, stillRetrying={stillRetrying})");
    }

    public void UpdateServerDetails(string name, int maxPlayers, string mapName, bool hasPassword)
    {
        if (!_initialized) return;
        _mapName = mapName;
        SteamServer.ServerName = name;
        SteamServer.MaxPlayers = maxPlayers;
        SteamServer.MapName = mapName;
        SteamServer.Passworded = hasPassword;
        Console.WriteLine($"[Steam] Details updated: name=\"{name}\", max={maxPlayers}, map=\"{mapName}\", pass={hasPassword}");
    }

    public void UpdatePlayerCount(int currentPlayers)
    {
        if (!_initialized) return;
        string desc = MakeGameDescription(currentPlayers, SteamServer.MaxPlayers);
        if (_gameDescriptionProp != null)
            _gameDescriptionProp.SetValue(null, desc);
    }

    private static string MakeGameDescription(int currentPlayers, int maxPlayers)
    {
        return $"{currentPlayers}_{maxPlayers}_0:0_{maxPlayers}_Rivet";
    }

    public void Dispose()
    {
        if (_querySocket != null)
        {
            try { _querySocket.Close(); } catch { }
            _querySocket = null;
        }

        if (_initialized)
        {
            if (SteamServer.LoggedOn)
            {
                Console.WriteLine($"[Steam] Logging off...");
                SteamServer.LogOff();
            }
            _initialized = false;
            _loggedOn = false;
            _connected = false;
            Console.WriteLine($"[Steam] Shut down");
        }
    }
}
