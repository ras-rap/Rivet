using System;
using System.Collections.Generic;
using System.Net;
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
    private readonly Action<IPEndPoint, byte[]> _sendResponse;
    private readonly Dictionary<string, DateTime> _rateLimit = new();

    private static readonly PropertyInfo? _gameDescriptionProp = typeof(SteamServer).GetProperty("GameDescription", BindingFlags.Public | BindingFlags.Static);

    public bool LoggedOn => _loggedOn;
    public bool Connected => _connected;

    public SteamServerManager(ushort gamePort, ushort queryPort, string serverName, int maxPlayers, bool hasPassword, string steamVersion, Action<IPEndPoint, byte[]> sendResponse)
    {
        _version = steamVersion;
        _serverName = serverName;
        _maxPlayers = maxPlayers;
        _hasPassword = hasPassword;
        _gamePort = gamePort;
        _queryPort = queryPort;
        _sendResponse = sendResponse;

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
            init = init.WithQueryShareGamePort();

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
        Console.WriteLine($"[Steam] Using GameSocketShare mode — A2S queries handled on game port {gamePort}");

        RunCallbacks();

        Console.WriteLine($"[Steam] Calling LogOnAnonymous...");
        SteamServer.LogOnAnonymous();

        Console.WriteLine($"[Steam] Server initialized on port {gamePort} (query shared) v{steamVersion}");
    }

    public void HandleRawPacket(IPEndPoint source, byte[] data)
    {
        if (!_initialized) return;
        if (data.Length < 4) return;
        if (data[0] != 0xFF || data[1] != 0xFF || data[2] != 0xFF || data[3] != 0xFF)
            return;

        var key = source.ToString();
        var now = DateTime.UtcNow;
        if (_rateLimit.TryGetValue(key, out var last) && (now - last).TotalMilliseconds < 1)
            return;
        _rateLimit[key] = now;

        SteamServer.HandleIncomingPacket(data, data.Length,
            BitConverter.ToUInt32(source.Address.GetAddressBytes(), 0),
            (ushort)source.Port);
    }

    public void Tick()
    {
        if (!_initialized) return;

        RunCallbacks();

        while (SteamServer.GetOutgoingPacket(out var packet))
        {
            try
            {
                var target = new IPEndPoint(new IPAddress(BitConverter.GetBytes(packet.Address)), packet.Port);
                _sendResponse(target, new Span<byte>(packet.Data, 0, packet.Size).ToArray());
            }
            catch (Exception)
            {
                break;
            }
        }

        _statusLogTimer += 0.016f;
        if (_statusLogTimer >= 30f)
        {
            _statusLogTimer = 0f;
            Console.WriteLine($"[Steam] Status — LoggedOn={SteamServer.LoggedOn}, Connected={_connected}, Players={SteamServer.MaxPlayers - SteamServer.BotCount}/{SteamServer.MaxPlayers}, Map={SteamServer.MapName}");
        }
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
        _rateLimit.Clear();

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
