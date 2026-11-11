// Minimal QR code encoder: byte mode, error correction L, versions 1 to 4 (up to 78
// bytes, which covers the https://host:port/?token=password links this app shows).
// Versions 1 to 4 with ECC L are a single Reed-Solomon block, so there is no
// interleaving to get wrong. Mask 0 is used: legal, and scanners read it fine.
//
// The algorithm follows the standard (ISO/IEC 18004); the structure mirrors Nayuki's
// reference implementation: build the function pattern mask, place the data with the
// two column zigzag, then apply the mask.
using System;

namespace VoxlDisplay
{
    internal sealed class Qr
    {
        // data codewords and ECC codewords per version, error correction L
        private static readonly int[] DataCodewords = { 19, 34, 55, 80 };
        private static readonly int[] EccCodewords = { 7, 10, 15, 20 };

        public static byte[] LastCodewords;
        public static bool[,] Encode(string text)
        {
            byte[] payload = System.Text.Encoding.UTF8.GetBytes(text);
            int version = 0;
            for (int v = 1; v <= 4; v++)
            {
                if (payload.Length + 2 <= DataCodewords[v - 1]) { version = v; break; }
            }
            if (version == 0) return null;

            int dataCount = DataCodewords[version - 1];
            byte[] data = new byte[dataCount];
            var bits = new BitWriter(data);
            bits.Write(0x4, 4);              // byte mode
            bits.Write(payload.Length, 8);   // length, 8 bits for versions 1 to 9
            for (int i = 0; i < payload.Length; i++) bits.Write(payload[i], 8);
            bits.PadToByte();
            bits.Finish(0xEC, 0x11);

            byte[] ecc = ReedSolomon(data, EccCodewords[version - 1]);
            byte[] all = new byte[dataCount + ecc.Length];
            Array.Copy(data, all, dataCount);
            Array.Copy(ecc, 0, all, dataCount, ecc.Length);

            LastCodewords = all;
            return Build(version, all);
        }

        private sealed class BitWriter
        {
            private readonly byte[] _out;
            private int _bit;
            public BitWriter(byte[] output) { _out = output; }
            public void Write(int value, int count)
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    if (((value >> i) & 1) != 0) _out[_bit >> 3] |= (byte)(0x80 >> (_bit & 7));
                    _bit++;
                }
            }
            public void PadToByte() { while ((_bit & 7) != 0) _bit++; }
            public void Finish(byte first, byte second)
            {
                int byteIndex = _bit >> 3;
                bool toggle = true;
                for (int i = byteIndex; i < _out.Length; i++)
                {
                    _out[i] = toggle ? first : second;
                    toggle = !toggle;
                }
            }
        }

        private static byte[] ReedSolomon(byte[] data, int degree)
        {
            // Generator polynomial, highest term dropped.
            var gen = new byte[degree];
            gen[degree - 1] = 1;
            byte root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < degree; j++)
                {
                    gen[j] = Mul(gen[j], root);
                    if (j + 1 < degree) gen[j] ^= gen[j + 1];
                }
                root = Mul(root, 2);
            }

            var rem = new byte[degree];
            foreach (byte b in data)
            {
                byte factor = (byte)(b ^ rem[0]);
                Array.Copy(rem, 1, rem, 0, degree - 1);
                rem[degree - 1] = 0;
                for (int j = 0; j < degree; j++) rem[j] ^= Mul(gen[j], factor);
            }
            return rem;
        }

        private static byte Mul(byte x, byte y)
        {
            int z = 0;
            for (int i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ ((z >> 7) * 0x11D);
                z ^= ((y >> i) & 1) * x;
            }
            return (byte)z;
        }

        private static bool[,] Build(int version, byte[] codewords)
        {
            int size = version * 4 + 17;
            var modules = new bool[size, size];
            var reserved = new bool[size, size];

            // Finder patterns with their separators.
            for (int i = -1; i <= 7; i++)
            {
                for (int j = -1; j <= 7; j++)
                {
                    bool inside = (i >= 0 && i <= 6 && (j == 0 || j == 6)) ||
                                  (j >= 0 && j <= 6 && (i == 0 || i == 6)) ||
                                  (i >= 2 && i <= 4 && j >= 2 && j <= 4);
                    SetIfInside(modules, reserved, i, j, inside);
                    SetIfInside(modules, reserved, size - 7 + i, j, inside);
                    SetIfInside(modules, reserved, i, size - 7 + j, inside);
                }
            }

            // Timing patterns.
            for (int i = 8; i < size - 8; i++)
            {
                bool dark = (i % 2) == 0;
                SetIfInside(modules, reserved, i, 6, dark);
                SetIfInside(modules, reserved, 6, i, dark);
            }

            // Alignment patterns (none in version 1).
            int[] centers = version == 1 ? new int[0]
                : version == 2 ? new[] { 6, 18 }
                : version == 3 ? new[] { 6, 22 }
                : new[] { 6, 26 };
            for (int i = 0; i < centers.Length; i++)
            {
                for (int j = 0; j < centers.Length; j++)
                {
                    // Skip the three that would land on the finder patterns.
                    if ((centers[i] == 6 && centers[j] == 6) ||
                        (centers[i] == 6 && centers[j] == size - 7) ||
                        (centers[i] == size - 7 && centers[j] == 6)) continue;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                            SetIfInside(modules, reserved, centers[i] + dx, centers[j] + dy,
                                Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
                }
            }

            DrawFormat(modules, reserved, size);
            PlaceData(modules, reserved, size, codewords);
            ApplyMask(modules, reserved, size);
            return modules;
        }

        private static void SetIfInside(bool[,] modules, bool[,] reserved, int x, int y, bool dark)
        {
            if (x < 0 || y < 0 || x >= modules.GetLength(0) || y >= modules.GetLength(1)) return;
            modules[y, x] = dark;
            reserved[y, x] = true;
        }

        private static void DrawFormat(bool[,] modules, bool[,] reserved, int size)
        {
            // Error correction L is 0b01, mask 0 is 0b000.
            int data = (1 << 3) | 0;
            int rem = data;
            for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            int bits = ((data << 10) | rem) ^ 0x5412;

            Func<int, bool> bit = i => ((bits >> i) & 1) != 0;

            for (int i = 0; i <= 5; i++) SetIfInside(modules, reserved, 8, i, bit(i));
            SetIfInside(modules, reserved, 8, 7, bit(6));
            SetIfInside(modules, reserved, 8, 8, bit(7));
            SetIfInside(modules, reserved, 7, 8, bit(8));
            for (int i = 9; i < 15; i++) SetIfInside(modules, reserved, 14 - i, 8, bit(i));

            for (int i = 0; i < 8; i++) SetIfInside(modules, reserved, size - 1 - i, 8, bit(i));
            for (int i = 8; i < 15; i++) SetIfInside(modules, reserved, 8, size - 15 + i, bit(i));
            SetIfInside(modules, reserved, 8, size - 8, true); // dark module
        }

        private static void PlaceData(bool[,] modules, bool[,] reserved, int size, byte[] data)
        {
            int i = 0;
            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                for (int vert = 0; vert < size; vert++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int y = upward ? size - 1 - vert : vert;
                        if (!reserved[y, x] && i < data.Length * 8)
                        {
                            bool on = ((data[i >> 3] >> (7 - (i & 7))) & 1) != 0;
                            modules[y, x] = on;
                            i++;
                        }
                    }
                }
            }
        }

        private static void ApplyMask(bool[,] modules, bool[,] reserved, int size)
        {
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if (!reserved[y, x] && ((x + y) & 1) == 0)
                        modules[y, x] = !modules[y, x];
        }
    }
}
