using System;
using System.Reflection;
using Steamworks;

namespace Rivet.Server;

public class SteamServerManager : IDisposable
{
    private bool _initialized;
    private bool _loggedOn;
    private bool _connected;
    private readonly string _version;
    private string _mapName = "Pulau Mahkota";
    private float _statusLogTimer;
    private readonly ushort _gamePort;
    private readonly ushort _queryPort;

    private static readonly PropertyInfo? _gameDescriptionProp = typeof(SteamServer).GetProperty("GameDescription", BindingFlags.Public | BindingFlags.Static);

    public bool LoggedOn => _loggedOn;
    public bool Connected => _connected;

    public SteamServerManager(ushort gamePort, ushort queryPort, string serverName, int maxPlayers, bool hasPassword, string steamVersion)
    {
        _version = steamVersion;
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

        SteamServer.ServerName = serverName;
        SteamServer.MaxPlayers = maxPlayers;
        SteamServer.BotCount = 0;
        SteamServer.MapName = _mapName;
        SteamServer.Passworded = hasPassword;
        SteamServer.AutomaticHeartbeats = true;

        Console.WriteLine($"[Steam] Server name=\"{serverName}\", maxPlayers={maxPlayers}, map={_mapName}");

        RunCallbacks();

        Console.WriteLine($"[Steam] Calling LogOnAnonymous...");
        SteamServer.LogOnAnonymous();

        Console.WriteLine($"[Steam] Server initialized on port {gamePort} (query {queryPort}) v{steamVersion}");
    }

    public void Tick()
    {
        if (!_initialized) return;
        RunCallbacks();

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
        Console.WriteLine($"[Steam] SUCCESS: Connected to Steam master server — server should appear in browser");
        Console.WriteLine($"[Steam]   Name=\"{SteamServer.ServerName}\", Map={SteamServer.MapName}, MaxPlayers={SteamServer.MaxPlayers}");
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
        Console.WriteLine($"[Steam]   Check: AppId 1279510 is correct, UDP {_queryPort} is reachable, Steam client is running");
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

    private string MakeGameDescription(int currentPlayers, int maxPlayers)
    {
        return $"{currentPlayers}_{_version}_0:0_{maxPlayers}_Rivet";
    }

    public void Dispose()
    {
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
