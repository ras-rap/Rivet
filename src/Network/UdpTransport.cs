using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Rivet.Network;

public record struct Packet(IPEndPoint Source, byte[] Data);

public class UdpTransport : IDisposable
{
    private readonly Socket _socket;
    private readonly Thread _recvThread;
    private readonly ConcurrentQueue<Packet> _incoming = new();
    private volatile bool _running;

    public int Port { get; }

    public UdpTransport(int port)
    {
        Port = port;
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _socket.Bind(new IPEndPoint(IPAddress.Any, port));
        _running = true;
        _recvThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "UDP-Recv" };
        _recvThread.Start();
    }

    private void ReceiveLoop()
    {
        var buf = new byte[65536];
        var ep = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            try
            {
                EndPoint remote = ep;
                var len = _socket.ReceiveFrom(buf, ref remote);
                if (!_running) break;
                var data = new byte[len];
                Array.Copy(buf, data, len);
                _incoming.Enqueue(new Packet((IPEndPoint)remote, data));
            }
            catch (ObjectDisposedException) { break; }
            catch (Exception)
            {
                if (!_running) break;
            }
        }
    }

    public bool TryReceive(out Packet pkt) => _incoming.TryDequeue(out pkt);

    public void Send(IPEndPoint target, byte[] data)
    {
        try { _socket.SendTo(data, target); }
        catch { /* ignore send errors */ }
    }

    public void Dispose()
    {
        _running = false;
        try { _socket?.Close(); } catch { }
        _recvThread.Join(1000);
    }
}
