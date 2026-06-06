using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace Rivet.Server;

public class BanManager
{
    private readonly HashSet<string> _steamIdBans = new();
    private readonly HashSet<string> _ipBans = new();
    private readonly string _path;

    public BanManager(string path = "bans.txt")
    {
        _path = path;
        Load();
    }

    public bool IsBanned(IPAddress ip, ulong steamId)
    {
        if (_ipBans.Contains(ip.ToString()))
            return true;
        if (steamId > 0 && _steamIdBans.Contains(steamId.ToString()))
            return true;
        return false;
    }

    public void BanSteamId(ulong steamId)
    {
        if (steamId == 0) return;
        _steamIdBans.Add(steamId.ToString());
        Save();
    }

    public void BanIP(IPAddress ip)
    {
        _ipBans.Add(ip.ToString());
        Save();
    }

    public bool Unban(string value)
    {
        bool removed = _steamIdBans.Remove(value) || _ipBans.Remove(value);
        if (removed) Save();
        return removed;
    }

    public string[] ListBans()
    {
        var list = new List<string>();
        foreach (var s in _steamIdBans)
            list.Add($"SteamID: {s}");
        foreach (var ip in _ipBans)
            list.Add($"IP: {ip}");
        return list.ToArray();
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        foreach (var line in File.ReadAllLines(_path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            if (trimmed.StartsWith("s:"))
                _steamIdBans.Add(trimmed[2..]);
            else if (trimmed.StartsWith("i:"))
                _ipBans.Add(trimmed[2..]);
        }
    }

    private void Save()
    {
        using var w = new StreamWriter(_path);
        w.WriteLine("# Rivet bans - s:steamid or i:ip");
        foreach (var s in _steamIdBans)
            w.WriteLine($"s:{s}");
        foreach (var ip in _ipBans)
            w.WriteLine($"i:{ip}");
    }
}
