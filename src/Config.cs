using System;
using System.IO;
using System.Text.Json;

namespace Rivet;

public class Config
{
    public int Port { get; set; } = 25000;
    public int PreConnectPort { get; set; } = 25001;
    public int MaxPlayers { get; set; } = 9999;
    public string ServerName { get; set; } = "Rivet Server";
    public string Password { get; set; } = "";
    public ushort SteamQueryPort { get; set; } = 27011;
    public ulong HostCSteamID { get; set; }
    public bool UseNobleConnect { get; set; }
    public ulong[] AdminSteamIds { get; set; } = [];
    public MapEntry[] VotableMaps { get; set; } = [];
    public int IdleKickMinutes { get; set; } = 10;
    public int ApiPort { get; set; } = 8080;
    public string ApiKey { get; set; } = "";
    public int MapRotationIntervalMinutes { get; set; } = 15;
    public int[] MapRotationIslands { get; set; } = [-801448567, -839216305, -487119212, -396930304];
    public string DiscordWebhookUrl { get; set; } = "";
    public string JoinMessage { get; set; } = "Welcome to Rivet!";
    public string SteamVersion { get; set; } = "V0.3.11.1";
    public string ConfigPath { get; set; } = "rivet.json";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, JsonOpts);
        File.WriteAllText(ConfigPath, json);
    }

    public static Config LoadOrDefault(string path = "rivet.json")
    {
        Config cfg;
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                cfg = JsonSerializer.Deserialize<Config>(json) ?? new Config();
                cfg.ConfigPath = path;
            }
            catch
            {
                Console.WriteLine($"[Config] Failed to parse {path}, using defaults");
                cfg = new Config { ConfigPath = path };
            }
        }
        else
        {
            cfg = new Config { ConfigPath = path };
            cfg.Save();
        }
        return cfg;
    }

    public void ApplyArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "-config":
                    if (i + 1 < args.Length)
                        ConfigPath = args[++i];
                    break;
                case "-port":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var port))
                        Port = port;
                    break;
                case "-maxplayers":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var max))
                        MaxPlayers = max;
                    break;
                case "-servername":
                    if (i + 1 < args.Length)
                        ServerName = args[++i].Replace("[<SPACE>]", " ");
                    break;
                case "-password":
                    if (i + 1 < args.Length)
                        Password = args[++i].Replace("[<SPACE>]", " ");
                    break;
                case "-hostcsteamid":
                    if (i + 1 < args.Length && ulong.TryParse(args[++i], out var sid))
                        HostCSteamID = sid;
                    break;
                case "-steamqueryport":
                    if (i + 1 < args.Length && ushort.TryParse(args[++i], out var sqp))
                        SteamQueryPort = sqp;
                    break;
                case "-nobleconnect":
                    if (i + 1 < args.Length && bool.TryParse(args[++i], out var nc))
                        UseNobleConnect = nc;
                    break;
                case "-webhook":
                    if (i + 1 < args.Length)
                        DiscordWebhookUrl = args[++i];
                    break;
                case "-joinmessage":
                    if (i + 1 < args.Length)
                        JoinMessage = args[++i].Replace("[<SPACE>]", " ");
                    break;
                case "-idlekick":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var kick))
                        IdleKickMinutes = kick;
                    break;
                case "-apiport":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var apiPort))
                        ApiPort = apiPort;
                    break;
                case "-apikey":
                    if (i + 1 < args.Length)
                        ApiKey = args[++i];
                    break;
            }
        }

        // Env var overrides
        if (int.TryParse(Environment.GetEnvironmentVariable("SV_PORT"), out var envPort))
            Port = envPort;
        if (int.TryParse(Environment.GetEnvironmentVariable("SV_MAXPLAYERS"), out var envMax))
            MaxPlayers = envMax;
        var envName = Environment.GetEnvironmentVariable("SV_NAME");
        if (!string.IsNullOrEmpty(envName))
            ServerName = envName;
        var envPass = Environment.GetEnvironmentVariable("SV_PASSWORD");
        if (!string.IsNullOrEmpty(envPass))
            Password = envPass;
        if (ushort.TryParse(Environment.GetEnvironmentVariable("SV_STEAM_QUERY_PORT"), out var envSqp))
            SteamQueryPort = envSqp;
        var envWebhook = Environment.GetEnvironmentVariable("SV_WEBHOOK");
        if (!string.IsNullOrEmpty(envWebhook))
            DiscordWebhookUrl = envWebhook;
        var envJoin = Environment.GetEnvironmentVariable("SV_JOIN_MESSAGE");
        if (!string.IsNullOrEmpty(envJoin))
            JoinMessage = envJoin;
        if (int.TryParse(Environment.GetEnvironmentVariable("SV_IDLE_KICK"), out var envKick))
            IdleKickMinutes = envKick;
        if (int.TryParse(Environment.GetEnvironmentVariable("SV_API_PORT"), out var envApiPort))
            ApiPort = envApiPort;
        var envApiKey = Environment.GetEnvironmentVariable("SV_API_KEY");
        if (!string.IsNullOrEmpty(envApiKey))
            ApiKey = envApiKey;
    }
}
