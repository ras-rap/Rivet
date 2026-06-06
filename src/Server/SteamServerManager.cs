using System;
using Steamworks;

namespace Rivet.Server;

public class SteamServerManager : IDisposable
{
    private bool _initialized;
    private bool _loggedOn;

    public bool LoggedOn => _loggedOn;

    public SteamServerManager(ushort gamePort, ushort queryPort, string serverName, int maxPlayers, bool hasPassword)
    {
        try
        {
            var init = new SteamServerInit
            {
                DedicatedServer = true,
                VersionString = "0.1.0.0",
                ModDir = "street_mc",
                GameDescription = "Street MC",
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
        SteamServer.MapName = "Pulau Mahkota";
        SteamServer.Passworded = hasPassword;
        SteamServer.AutomaticHeartbeats = true;

        RunCallbacks();

        SteamServer.LogOnAnonymous();
        _loggedOn = true;

        Console.WriteLine($"[Steam] Game server advertising on Steam (query port {queryPort})");
    }

    public void Tick()
    {
        if (!_initialized) return;
        RunCallbacks();
    }

    private static void RunCallbacks()
    {
        SteamServer.RunCallbacks();
    }

    public void UpdateServerDetails(string name, int maxPlayers, string mapName, bool hasPassword)
    {
        if (!_initialized) return;
        SteamServer.ServerName = name;
        SteamServer.MaxPlayers = maxPlayers;
        SteamServer.MapName = mapName;
        SteamServer.Passworded = hasPassword;
    }

    public void Dispose()
    {
        if (_loggedOn)
        {
            SteamServer.LogOff();
            _loggedOn = false;
        }
        _initialized = false;
    }
}
