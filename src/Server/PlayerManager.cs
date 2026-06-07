using System;
using System.Collections.Generic;
using System.Net;

namespace Rivet.Server;

public class Player
{
    public byte PlayerID { get; set; }
    public string PlayerName { get; set; } = "";
    public ulong CSteamID { get; set; }
    public IPEndPoint EndPoint { get; set; } = null!;
    public float Ping { get; set; }
    public float NoMessageFor { get; set; }
    public float PingExcusedFor { get; set; }
    public bool IsConnected { get; set; } = true;
    public float LastInputTime { get; set; }
}

public class PlayerManager
{
    private readonly List<Player> _players = new();
    private int _lastAssignedID = -1;
    private readonly float _disconnectTimeout;
    private float _time;

    public IReadOnlyList<Player> Players => _players;
    public int PlayerCount => _players.Count;
    public event Action<Player>? OnPlayerConnected;
    public event Action<Player>? OnPlayerDisconnected;

    public PlayerManager(float disconnectTimeout = 10f)
    {
        _disconnectTimeout = disconnectTimeout;
    }

    public Player? GetPlayerByID(byte id)
    {
        for (int i = 0; i < _players.Count; i++)
            if (_players[i].PlayerID == id && _players[i].IsConnected)
                return _players[i];
        return null;
    }

    public Player? GetPlayerByEndPoint(IPEndPoint ep)
    {
        for (int i = 0; i < _players.Count; i++)
            if (_players[i].EndPoint.Equals(ep) && _players[i].IsConnected)
                return _players[i];
        return null;
    }

    public bool IsIPConnected(IPAddress ip)
    {
        for (int i = 0; i < _players.Count; i++)
            if (_players[i].IsConnected && _players[i].EndPoint.Address.Equals(ip))
                return true;
        return false;
    }

    public Player AddPlayer(IPEndPoint ep, string name, ulong steamID, byte forcedSelfID)
    {
        byte id = forcedSelfID != byte.MaxValue && IsIDFree(forcedSelfID)
            ? forcedSelfID
            : GetNextFreeID();

        var player = new Player
        {
            PlayerID = id,
            PlayerName = name,
            CSteamID = steamID,
            EndPoint = ep,
            IsConnected = true
        };
        _players.Add(player);
        OnPlayerConnected?.Invoke(player);
        return player;
    }

    public void RemovePlayer(byte playerID)
    {
        for (int i = 0; i < _players.Count; i++)
        {
            if (_players[i].PlayerID == playerID && _players[i].IsConnected)
            {
                var player = _players[i];
                _players.RemoveAt(i);
                OnPlayerDisconnected?.Invoke(player);
                break;
            }
        }
    }

    public void Tick(float dt)
    {
        _time += dt;
        for (int i = _players.Count - 1; i >= 0; i--)
        {
            var p = _players[i];
            if (p.PingExcusedFor > 0)
                p.PingExcusedFor -= dt;
            else
            {
                p.NoMessageFor += dt;
                if (p.NoMessageFor >= _disconnectTimeout)
                    RemovePlayer(p.PlayerID);
            }
        }
    }

    public void ResetTimeout(byte playerID)
    {
        var p = GetPlayerByID(playerID);
        if (p != null)
        {
            p.NoMessageFor = 0;
            p.PingExcusedFor = 0;
        }
    }

    public bool IsServerFull(int maxPlayers) => _players.Count >= maxPlayers;

    private byte GetNextFreeID()
    {
        for (int i = 1; i < byte.MaxValue; i++)
        {
            byte id = (byte)((_lastAssignedID + i) % byte.MaxValue);
            if (id == 0) continue;
            if (IsIDFree(id))
            {
                _lastAssignedID = id;
                return id;
            }
        }
        return byte.MaxValue;
    }

    private bool IsIDFree(byte id)
    {
        for (int i = 0; i < _players.Count; i++)
            if (_players[i].PlayerID == id && _players[i].IsConnected)
                return false;
        return true;
    }
}
