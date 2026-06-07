using System;
using System.Reflection;
using Steamworks;

namespace Rivet.Server;

public class SteamServerManager : IDisposable
{
    private bool _initialized;
    private bool _loggedOn;
    private readonly string _version;
    private string _mapName = "Pulau Mahkota";

    public bool LoggedOn => _loggedOn;

    private static readonly PropertyInfo? _gameDescriptionProp = typeof(SteamServer).GetProperty("GameDescription", BindingFlags.Public | BindingFlags.Static);

    public SteamServerManager(ushort gamePort, ushort queryPort, string serverName, int maxPlayers, bool hasPassword, string steamVersion)
    {
        _version = steamVersion;

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

        RunCallbacks();

        SteamServer.LogOnAnonymous();
        _loggedOn = true;

        Console.WriteLine($"[Steam] Game server advertising on Steam (query port {queryPort}) with v{steamVersion}");
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
        _mapName = mapName;
        SteamServer.ServerName = name;
        SteamServer.MaxPlayers = maxPlayers;
        SteamServer.MapName = mapName;
        SteamServer.Passworded = hasPassword;
    }

    public void UpdatePlayerCount(int currentPlayers)
    {
        if (!_initialized) return;
        string desc = MakeGameDescription(currentPlayers, SteamServer.MaxPlayers);
        SetGameDescription(desc);
    }

    private void SetGameDescription(string value)
    {
        if (_gameDescriptionProp != null)
            _gameDescriptionProp.SetValue(null, value);
    }

    private string MakeGameDescription(int currentPlayers, int maxPlayers)
    {
        return $"{currentPlayers}_{_version}_0:0_{maxPlayers}_Rivet";
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
