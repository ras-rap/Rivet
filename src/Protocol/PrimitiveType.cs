using System;
using System.Collections.Generic;
using System.Text;

namespace Rivet.Protocol;

public static class PrimitiveType
{
    public const byte Byte = 0;
    public const byte ByteArray = 1;
    public const byte String = 2;
    public const byte ULong = 3;
    public const byte IntArray = 4;
    public const byte Bool = 5;
    public const byte BoolArray = 6;
    public const byte Float = 7;
    public const byte FloatArray = 8;
    public const byte Double = 9;
    public const byte DoubleArray = 10;
    public const byte StringArray = 11;
    public const byte ULongArray = 12;
    public const byte Vector3 = 13;
    public const byte Vector3Array = 14;
    public const byte Int = 15;
    public const byte Short = 16;
    public const byte ShortArray = 17;
    public const byte UShort = 18;
    public const byte UShortArray = 19;
    public const byte UInt = 20;
    public const byte UIntArray = 21;

    public static byte GetDataTypeId(object obj)
    {
        return obj switch
        {
            byte => Byte,
            byte[] => ByteArray,
            string => String,
            ulong => ULong,
            int[] => IntArray,
            bool => Bool,
            bool[] => BoolArray,
            float => Float,
            float[] => FloatArray,
            double => Double,
            double[] => DoubleArray,
            string[] => StringArray,
            ulong[] => ULongArray,
            Vec3 => Vector3,
            Vec3[] => Vector3Array,
            int => Int,
            short => Short,
            short[] => ShortArray,
            ushort => UShort,
            ushort[] => UShortArray,
            uint => UInt,
            uint[] => UIntArray,
            _ => byte.MaxValue
        };
    }

    public static bool HasFixedSize(byte typeId)
    {
        return typeId is Byte or ULong or Bool or Float or Double or Vector3 or Int or Short or UShort or UInt;
    }
}

public struct Vec3
{
    public float X, Y, Z;

    public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }

    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
}
