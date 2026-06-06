using System;
using System.Collections.Generic;
using System.Text;

namespace Rivet.Protocol;

public abstract class DataObject
{
    private static readonly Dictionary<string, byte[]> CachedTypeBytes = new();
    private byte[]? _typeBytes;
    private Type? _selfType;

    public ushort MsgId => MessageRegistry.GetMsgId(GetType());

    protected DataObject()
    {
        var t = GetType();
        _selfType = t;
        var name = t.Name;
        if (CachedTypeBytes.TryGetValue(name, out var cached))
        {
            _typeBytes = cached;
        }
        else
        {
            var fields = Serialize();
            var bytes = new byte[fields.Count];
            for (int i = 0; i < fields.Count; i++)
                bytes[i] = PrimitiveType.GetDataTypeId(fields[i]);
            CachedTypeBytes[name] = bytes;
            _typeBytes = bytes;
        }
    }

    public byte[] ToBytes()
    {
        var msgId = MsgId;
        var fields = Serialize();

        var bytes = new List<byte>(32);
        bytes.Add((byte)(msgId >> 8));
        bytes.Add((byte)msgId);

        for (int i = 0; i < fields.Count; i++)
            WriteField(bytes, _typeBytes![i], fields[i]);

        return bytes.ToArray();
    }

    public void ParseBytes(byte[] data, int offset)
    {
        var fields = Serialize();
        for (int i = 0; i < _typeBytes!.Length && offset < data.Length; i++)
        {
            object val = null!;
            offset = ReadField(data, offset, _typeBytes[i], out val);
            fields[i] = val;
        }
        Deserialize(fields);
    }

    protected abstract List<object> Serialize();
    protected virtual void Deserialize(List<object> fields) { }

    private static void WriteField(List<byte> buf, byte type, object val)
    {
        switch (type)
        {
            case PrimitiveType.Byte:
                buf.Add((byte)val);
                break;
            case PrimitiveType.ByteArray:
                WriteByteArray(buf, (byte[])val);
                break;
            case PrimitiveType.String:
                WriteString(buf, (string)val);
                break;
            case PrimitiveType.ULong:
                WriteULong(buf, (ulong)val);
                break;
            case PrimitiveType.IntArray:
                WriteIntArray(buf, (int[])val);
                break;
            case PrimitiveType.Bool:
                buf.Add((bool)val ? (byte)1 : (byte)0);
                break;
            case PrimitiveType.BoolArray:
                WriteBoolArray(buf, (bool[])val);
                break;
            case PrimitiveType.Float:
                WriteFloat(buf, (float)val);
                break;
            case PrimitiveType.FloatArray:
                WriteFloatArray(buf, (float[])val);
                break;
            case PrimitiveType.Double:
                WriteDouble(buf, (double)val);
                break;
            case PrimitiveType.DoubleArray:
                WriteDoubleArray(buf, (double[])val);
                break;
            case PrimitiveType.StringArray:
                WriteStringArray(buf, (string[])val);
                break;
            case PrimitiveType.ULongArray:
                WriteULongArray(buf, (ulong[])val);
                break;
            case PrimitiveType.Vector3:
                WriteVec3(buf, (Vec3)val);
                break;
            case PrimitiveType.Vector3Array:
                WriteVec3Array(buf, (Vec3[])val);
                break;
            case PrimitiveType.Int:
                WriteInt(buf, (int)val);
                break;
            case PrimitiveType.Short:
                WriteShort(buf, (short)val);
                break;
            case PrimitiveType.ShortArray:
                WriteShortArray(buf, (short[])val);
                break;
            case PrimitiveType.UShort:
                WriteUShort(buf, (ushort)val);
                break;
            case PrimitiveType.UShortArray:
                WriteUShortArray(buf, (ushort[])val);
                break;
            case PrimitiveType.UInt:
                WriteUInt(buf, (uint)val);
                break;
            case PrimitiveType.UIntArray:
                WriteUIntArray(buf, (uint[])val);
                break;
        }
    }

    private static int ReadField(byte[] data, int offset, byte type, out object val)
    {
        switch (type)
        {
            case PrimitiveType.Byte:
                val = data[offset];
                return offset + 1;
            case PrimitiveType.ByteArray:
                return ReadByteArray(data, offset, out val);
            case PrimitiveType.String:
                return ReadString(data, offset, out val);
            case PrimitiveType.ULong:
                return ReadULong(data, offset, out val);
            case PrimitiveType.IntArray:
                return ReadIntArray(data, offset, out val);
            case PrimitiveType.Bool:
                val = data[offset] == 1;
                return offset + 1;
            case PrimitiveType.BoolArray:
                return ReadBoolArray(data, offset, out val);
            case PrimitiveType.Float:
                return ReadFloat(data, offset, out val);
            case PrimitiveType.FloatArray:
                return ReadFloatArray(data, offset, out val);
            case PrimitiveType.Double:
                return ReadDouble(data, offset, out val);
            case PrimitiveType.DoubleArray:
                return ReadDoubleArray(data, offset, out val);
            case PrimitiveType.StringArray:
                return ReadStringArray(data, offset, out val);
            case PrimitiveType.ULongArray:
                return ReadULongArray(data, offset, out val);
            case PrimitiveType.Vector3:
                return ReadVec3(data, offset, out val);
            case PrimitiveType.Vector3Array:
                return ReadVec3Array(data, offset, out val);
            case PrimitiveType.Int:
                return ReadInt(data, offset, out val);
            case PrimitiveType.Short:
                return ReadShort(data, offset, out val);
            case PrimitiveType.ShortArray:
                return ReadShortArray(data, offset, out val);
            case PrimitiveType.UShort:
                return ReadUShort(data, offset, out val);
            case PrimitiveType.UShortArray:
                return ReadUShortArray(data, offset, out val);
            case PrimitiveType.UInt:
                return ReadUInt(data, offset, out val);
            case PrimitiveType.UIntArray:
                return ReadUIntArray(data, offset, out val);
            default:
                throw new InvalidOperationException($"Unknown type byte: {type}");
        }
    }

    // ---- Write helpers ----

    private static void WriteLength(List<byte> buf, int len)
    {
        buf.Add((byte)(len >> 24));
        buf.Add((byte)(len >> 16));
        buf.Add((byte)(len >> 8));
        buf.Add((byte)len);
    }

    private static int ReadLength(byte[] data, int offset) =>
        (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

    private static void WriteByteArray(List<byte> buf, byte[] data)
    {
        WriteLength(buf, data.Length);
        buf.AddRange(data);
    }

    private static int ReadByteArray(byte[] data, int offset, out object val)
    {
        int len = ReadLength(data, offset);
        var arr = new byte[len];
        Array.Copy(data, offset + 4, arr, 0, len);
        val = arr;
        return offset + 4 + len;
    }

    private static void WriteString(List<byte> buf, string s)
    {
        var utf8 = Encoding.UTF8.GetBytes(s);
        WriteLength(buf, utf8.Length);
        buf.AddRange(utf8);
    }

    private static int ReadString(byte[] data, int offset, out object val)
    {
        int len = ReadLength(data, offset);
        val = Encoding.UTF8.GetString(data, offset + 4, len);
        return offset + 4 + len;
    }

    private static void WriteULong(List<byte> buf, ulong v)
    {
        buf.Add((byte)(v >> 56));
        buf.Add((byte)(v >> 48));
        buf.Add((byte)(v >> 40));
        buf.Add((byte)(v >> 32));
        buf.Add((byte)(v >> 24));
        buf.Add((byte)(v >> 16));
        buf.Add((byte)(v >> 8));
        buf.Add((byte)v);
    }

    private static int ReadULong(byte[] data, int offset, out object val)
    {
        val = ((ulong)data[offset] << 56) | ((ulong)data[offset + 1] << 48) |
              ((ulong)data[offset + 2] << 40) | ((ulong)data[offset + 3] << 32) |
              ((ulong)data[offset + 4] << 24) | ((ulong)data[offset + 5] << 16) |
              ((ulong)data[offset + 6] << 8) | data[offset + 7];
        return offset + 8;
    }

    private static void WriteInt(List<byte> buf, int v)
    {
        buf.Add((byte)(v >> 24));
        buf.Add((byte)(v >> 16));
        buf.Add((byte)(v >> 8));
        buf.Add((byte)v);
    }

    private static int ReadInt(byte[] data, int offset, out object val)
    {
        val = (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        return offset + 4;
    }

    private static void WriteShort(List<byte> buf, short v)
    {
        buf.Add((byte)(v >> 8));
        buf.Add((byte)v);
    }

    private static int ReadShort(byte[] data, int offset, out object val)
    {
        val = (short)((data[offset] << 8) | data[offset + 1]);
        return offset + 2;
    }

    private static void WriteUShort(List<byte> buf, ushort v)
    {
        buf.AddRange(BitConverter.GetBytes(v));
    }

    private static int ReadUShort(byte[] data, int offset, out object val)
    {
        val = BitConverter.ToUInt16(data, offset);
        return offset + 2;
    }

    private static void WriteUInt(List<byte> buf, uint v)
    {
        buf.AddRange(BitConverter.GetBytes(v));
    }

    private static int ReadUInt(byte[] data, int offset, out object val)
    {
        val = BitConverter.ToUInt32(data, offset);
        return offset + 4;
    }

    private static void WriteFloat(List<byte> buf, float v)
    {
        buf.AddRange(BitConverter.GetBytes(v));
    }

    private static int ReadFloat(byte[] data, int offset, out object val)
    {
        val = BitConverter.ToSingle(data, offset);
        return offset + 4;
    }

    private static void WriteDouble(List<byte> buf, double v)
    {
        buf.AddRange(BitConverter.GetBytes(v));
    }

    private static int ReadDouble(byte[] data, int offset, out object val)
    {
        val = BitConverter.ToDouble(data, offset);
        return offset + 8;
    }

    private static void WriteBoolArray(List<byte> buf, bool[] arr)
    {
        WriteLength(buf, arr.Length);
        foreach (var b in arr)
            buf.Add(b ? (byte)1 : (byte)0);
    }

    private static int ReadBoolArray(byte[] data, int offset, out object val)
    {
        int len = ReadLength(data, offset);
        var arr = new bool[len];
        for (int i = 0; i < len; i++)
            arr[i] = data[offset + 4 + i] == 1;
        val = arr;
        return offset + 4 + len;
    }

    private static void WriteIntArray(List<byte> buf, int[] arr)
    {
        WriteLength(buf, arr.Length);
        foreach (var v in arr)
            WriteInt(buf, v);
    }

    private static int ReadIntArray(byte[] data, int offset, out object val)
    {
        int len = ReadLength(data, offset);
        var arr = new int[len];
        for (int i = 0; i < len; i++)
            arr[i] = (data[offset + 4 + i * 4] << 24) | (data[offset + 5 + i * 4] << 16) |
                     (data[offset + 6 + i * 4] << 8) | data[offset + 7 + i * 4];
        val = arr;
        return offset + 4 + len * 4;
    }

    private static void WriteFloatArray(List<byte> buf, float[] arr)
    {
        WriteLength(buf, arr.Length);
        foreach (var v in arr)
            buf.AddRange(BitConverter.GetBytes(v));
    }

    private static int ReadFloatArray(byte[] data, int offset, out object val)
    {
        int len = ReadLength(data, offset);
        var arr = new float[len];
        for (int i = 0; i < len; i++)
            arr[i] = BitConverter.ToSingle(data, offset + 4 + i * 4);
        val = arr;
        return offset + 4 + len * 4;
    }

    private static void WriteDoubleArray(List<byte> buf, double[] arr)
    {
        WriteLength(buf, arr.Length);
        foreach (var v in arr)
            buf.AddRange(BitConverter.GetBytes(v));
    }

    private static int ReadDoubleArray(byte[] data, int offset, out object val)
    {
        int len = ReadLength(data, offset);
        var arr = new double[len];
        for (int i = 0; i < len; i++)
            arr[i] = BitConverter.ToDouble(data, offset + 4 + i * 8);
        val = arr;
        return offset + 4 + len * 8;
    }

    private static void WriteStringArray(List<byte> buf, string[] arr)
    {
        WriteLength(buf, arr.Length);
        foreach (var s in arr)
            WriteString(buf, s);
    }

    private static int ReadStringArray(byte[] data, int offset, out object val)
    {
        int len = ReadLength(data, offset);
        var arr = new string[len];
        int pos = offset + 4;
        for (int i = 0; i < len; i++)
        {
            int slen = ReadLength(data, pos);
            arr[i] = Encoding.UTF8.GetString(data, pos + 4, slen);
            pos += 4 + slen;
        }
        val = arr;
        return pos;
    }

    private static void WriteULongArray(List<byte> buf, ulong[] arr)
    {
        WriteLength(buf, arr.Length);
        foreach (var v in arr)
            buf.AddRange(BitConverter.GetBytes(v));
    }

    private static int ReadULongArray(byte[] data, int offset, out object val)
    {
        int len = ReadLength(data, offset);
        var arr = new ulong[len];
        for (int i = 0; i < len; i++)
            arr[i] = BitConverter.ToUInt64(data, offset + 4 + i * 8);
        val = arr;
        return offset + 4 + len * 8;
    }

    private static void WriteVec3(List<byte> buf, Vec3 v)
    {
        buf.AddRange(BitConverter.GetBytes(v.X));
        buf.AddRange(BitConverter.GetBytes(v.Y));
        buf.AddRange(BitConverter.GetBytes(v.Z));
    }

    private static int ReadVec3(byte[] data, int offset, out object val)
    {
        val = new Vec3(
            BitConverter.ToSingle(data, offset),
            BitConverter.ToSingle(data, offset + 4),
            BitConverter.ToSingle(data, offset + 8));
        return offset + 12;
    }

    private static void WriteVec3Array(List<byte> buf, Vec3[] arr)
    {
        WriteLength(buf, arr.Length);
        foreach (var v in arr)
            WriteVec3(buf, v);
    }

    private static int ReadVec3Array(byte[] data, int offset, out object val)
    {
        int len = ReadLength(data, offset);
        var arr = new Vec3[len];
        for (int i = 0; i < len; i++)
        {
            arr[i] = new Vec3(
                BitConverter.ToSingle(data, offset + 4 + i * 12),
                BitConverter.ToSingle(data, offset + 8 + i * 12),
                BitConverter.ToSingle(data, offset + 12 + i * 12));
        }
        val = arr;
        return offset + 4 + len * 12;
    }

    private static void WriteShortArray(List<byte> buf, short[] arr)
    {
        WriteLength(buf, arr.Length);
        foreach (var v in arr)
            buf.AddRange(BitConverter.GetBytes(v));
    }

    private static int ReadShortArray(byte[] data, int offset, out object val)
    {
        int len = ReadLength(data, offset);
        var arr = new short[len];
        for (int i = 0; i < len; i++)
            arr[i] = BitConverter.ToInt16(data, offset + 4 + i * 2);
        val = arr;
        return offset + 4 + len * 2;
    }

    private static void WriteUShortArray(List<byte> buf, ushort[] arr)
    {
        WriteLength(buf, arr.Length);
        foreach (var v in arr)
            buf.AddRange(BitConverter.GetBytes(v));
    }

    private static int ReadUShortArray(byte[] data, int offset, out object val)
    {
        int len = ReadLength(data, offset);
        var arr = new ushort[len];
        for (int i = 0; i < len; i++)
            arr[i] = BitConverter.ToUInt16(data, offset + 4 + i * 2);
        val = arr;
        return offset + 4 + len * 2;
    }

    private static void WriteUIntArray(List<byte> buf, uint[] arr)
    {
        WriteLength(buf, arr.Length);
        foreach (var v in arr)
            buf.AddRange(BitConverter.GetBytes(v));
    }

    private static int ReadUIntArray(byte[] data, int offset, out object val)
    {
        int len = ReadLength(data, offset);
        var arr = new uint[len];
        for (int i = 0; i < len; i++)
            arr[i] = BitConverter.ToUInt32(data, offset + 4 + i * 4);
        val = arr;
        return offset + 4 + len * 4;
    }
}
