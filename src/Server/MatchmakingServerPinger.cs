using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Rivet.Protocol;

namespace Rivet.Server;

public class MatchmakingServerPinger : IDisposable
{
    private static readonly IPEndPoint MatchmakingEndpoint = new(IPAddress.Parse("202.61.194.29"), 37999);

    private readonly UdpClient _udp = new();
    private readonly Func<string> _getPublicIP;
    private readonly int _port;
    private readonly string _name;
    private readonly int _maxPlayers;
    private readonly string _version;
    private readonly Func<int> _getCurrentPlayers;
    private readonly Func<bool> _getHasPassword;
    private float _timer;

    public MatchmakingServerPinger(
        Func<string> getPublicIP, int port, string name, int maxPlayers, string version,
        Func<int> getCurrentPlayers, Func<bool> getHasPassword)
    {
        _getPublicIP = getPublicIP;
        _port = port;
        _name = name;
        _maxPlayers = maxPlayers;
        _version = version;
        _getCurrentPlayers = getCurrentPlayers;
        _getHasPassword = getHasPassword;
    }

    public void Tick(float dt)
    {
        _timer += dt;
        if (_timer < 10f) return;
        _timer = 0f;

        try
        {
            var msg = new MsgMMGameServerPingToMatchmaking
            {
                SelfPublicIP = _getPublicIP(),
                SelfPublicPort = _port,
                ServerName = _name,
                MaxPlayers = _maxPlayers,
                CurrentPlayers = _getCurrentPlayers(),
                GameVersion = _version,
                MetaInfo1 = "F_0",
                MetaInfo2 = "0",
                HasPassword = _getHasPassword(),
                Description = ""
            };

            var payload = msg.ToBytes();
            var packet = new byte[payload.Length + 3];
            packet[0] = (byte)(payload.Length >> 8);
            packet[1] = (byte)payload.Length;
            packet[2] = 0xFE;
            Buffer.BlockCopy(payload, 0, packet, 3, payload.Length);

            _udp.Send(packet, packet.Length, MatchmakingEndpoint);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Matchmaking] Send error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _udp.Dispose();
    }
}
