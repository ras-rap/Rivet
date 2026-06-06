using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Rivet.Server;

public class PreConnectServer : IDisposable
{
    private readonly Socket _socket;
    private readonly Thread _thread;
    private volatile bool _running;
    private readonly List<(IPAddress ip, int port, int id)> _queue = new();
    private readonly object _lock = new();
    private readonly int _port;

    public PreConnectServer(int port = 25001)
    {
        _port = port;
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _socket.Bind(new IPEndPoint(IPAddress.Any, port));
        _running = true;
        _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "PreConnect" };
        _thread.Start();
    }

    private void ReceiveLoop()
    {
        var buf = new byte[1024];
        var ep = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            try
            {
                EndPoint remote = ep;
                var len = _socket.ReceiveFrom(buf, ref remote);
                if (!_running || len < 2) break;

                var rEp = (IPEndPoint)remote;
                if (buf[0] == 0x00 && buf[1] == 0x00 && len >= 6)
                {
                    // Join queue: [0x00][0x00][4-byte queueClientID BE]
                    int queueId = (buf[2] << 24) | (buf[3] << 16) | (buf[4] << 8) | buf[5];
                    lock (_lock)
                    {
                        _queue.Add((rEp.Address, rEp.Port, queueId));
                    }
                    // Respond: [0x00][0x03][1-byte queueSize]
                    int qSize;
                    lock (_lock) { qSize = _queue.Count; }
                    byte[] resp = [0x00, 0x03, (byte)qSize];
                    _socket.SendTo(resp, remote);
                }
                else if (buf[0] == 0x00 && buf[1] == 0x02)
                {
                    // Confirming connection: [0x00][0x02]
                    lock (_lock)
                    {
                        _queue.RemoveAll(w => w.ip.Equals(rEp.Address) && w.port == rEp.Port);
                    }
                }
                else if (buf[0] == 0x00 && buf[1] == 0x04 && len >= 6)
                {
                    // Watchdog reset: [0x00][0x04][4-byte queueID BE]
                    // We don't track watchdog timeouts in MVP, just remove from queue
                    lock (_lock)
                    {
                        _queue.RemoveAll(w => w.ip.Equals(rEp.Address) && w.port == rEp.Port);
                    }
                }
            }
            catch (ObjectDisposedException) { break; }
            catch { if (!_running) break; }
        }
    }

    public void Dispose()
    {
        _running = false;
        try { _socket?.Close(); } catch { }
        _thread.Join(1000);
    }
}
