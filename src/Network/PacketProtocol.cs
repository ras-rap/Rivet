using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace Rivet.Network;

public class PacketProtocol : IDisposable
{
    private const byte TYPE_UNRELIABLE = 0xFE;
    private const byte TYPE_LARGE_FRAGMENT = 0xFA;
    private const byte TYPE_ACK = 0xFC;
    private const byte TYPE_RELIABLE_MARKER = 0xFF;
    private const byte TYPE_ACK_RESPONSE_MARKER2 = 0xFE;
    private const byte TYPE_LARGE_RELIABLE_MARKER2 = 0xFF;
    private const byte TYPE_LARGE_RELIABLE_MARKER2_ALT = 0xFD;

    private readonly UdpTransport _transport;
    private int _nextAckNumber;
    private readonly Dictionary<IPEndPoint, int> _lastAckReceived = new();
    private readonly ConcurrentQueue<Action> _mainThreadActions = new();

    public event Action<IPEndPoint, byte[]>? OnMessage;
    public event Action<IPEndPoint, int>? OnAckReceived;

    public PacketProtocol(int port)
    {
        _transport = new UdpTransport(port);
    }

    public void Poll()
    {
        while (_transport.TryReceive(out var pkt))
            ProcessPacket(pkt.Source, pkt.Data);
    }

    private void ProcessPacket(IPEndPoint source, byte[] data)
    {
        int offset = 0;
        while (offset < data.Length)
        {
            if (offset + 3 > data.Length) break;

            int payloadLen = (data[offset] << 8) | data[offset + 1];
            byte typeByte = data[offset + 2];
            int chunkSize = payloadLen + 3;

            if (typeByte == TYPE_UNRELIABLE)
            {
                if (offset + 3 + payloadLen <= data.Length)
                {
                    var payload = new byte[payloadLen];
                    Array.Copy(data, offset + 3, payload, 0, payloadLen);
                    OnMessage?.Invoke(source, payload);
                }
            }
            else if (typeByte == TYPE_RELIABLE_MARKER && offset + 4 <= data.Length && data[offset + 3] == TYPE_RELIABLE_MARKER)
            {
                // Reliable message: [2 len=contentSize][0xFF][0xFF][4 ack][content...]
                if (offset + 8 + payloadLen <= data.Length)
                {
                    int ack = (data[offset + 4] << 24) | (data[offset + 5] << 16) |
                              (data[offset + 6] << 8) | data[offset + 7];
                    if (payloadLen > 0)
                    {
                        var payload = new byte[payloadLen];
                        Array.Copy(data, offset + 8, payload, 0, payloadLen);
                        OnMessage?.Invoke(source, payload);
                    }
                    // Send ACK response: [2 len=6][0xFF][0xFE][4 ack]
                    var ackResp = new byte[8]
                    {
                        0, 6, TYPE_RELIABLE_MARKER, TYPE_ACK_RESPONSE_MARKER2,
                        (byte)(ack >> 24), (byte)(ack >> 16), (byte)(ack >> 8), (byte)ack
                    };
                    _transport.Send(source, ackResp);
                }
            }
            else if (typeByte == TYPE_RELIABLE_MARKER && offset + 4 <= data.Length && data[offset + 3] == TYPE_ACK)
            {
                // ACK: [2 len][0xFF][0xFC][4 ack]
                if (offset + 8 <= data.Length)
                {
                    int ack = (data[offset + 4] << 24) | (data[offset + 5] << 16) |
                              (data[offset + 6] << 8) | data[offset + 7];
                    OnAckReceived?.Invoke(source, ack);
                }
            }
            else if (typeByte == TYPE_LARGE_FRAGMENT)
            {
                // Large fragment: [2 len][0xFA][payload]
                if (offset + 3 + payloadLen <= data.Length)
                {
                    var payload = new byte[payloadLen];
                    Array.Copy(data, offset + 3, payload, 0, payloadLen);
                    OnMessage?.Invoke(source, payload);
                }
            }

            offset += chunkSize;
        }
    }

    public int SendUnreliable(IPEndPoint target, byte[] data)
    {
        var pkt = new byte[data.Length + 3];
        pkt[0] = (byte)(data.Length >> 8);
        pkt[1] = (byte)data.Length;
        pkt[2] = TYPE_UNRELIABLE;
        Array.Copy(data, 0, pkt, 3, data.Length);
        _transport.Send(target, pkt);
        return -1;
    }

    public int SendReliable(IPEndPoint target, byte[] data)
    {
        int ack = Interlocked.Increment(ref _nextAckNumber);
        var pkt = new byte[data.Length + 8];
        // length field = content size only (NOT including 0xFF 0xFF + ACK)
        pkt[0] = (byte)(data.Length >> 8);
        pkt[1] = (byte)data.Length;
        pkt[2] = TYPE_RELIABLE_MARKER;
        pkt[3] = TYPE_RELIABLE_MARKER;
        pkt[4] = (byte)(ack >> 24);
        pkt[5] = (byte)(ack >> 16);
        pkt[6] = (byte)(ack >> 8);
        pkt[7] = (byte)ack;
        Array.Copy(data, 0, pkt, 8, data.Length);
        _transport.Send(target, pkt);
        return ack;
    }

    public void SendRaw(IPEndPoint target, byte[] data) => _transport.Send(target, data);

    public void Dispose() => _transport.Dispose();
}
