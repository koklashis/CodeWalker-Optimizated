/*
    Copyright(c) 2015 Neodymium

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.
*/

using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace CodeWalker.GameFiles
{
    public class GTACrypto
    {
        public static byte[] DecryptAES(byte[] data)
        {
            return DecryptAESData(data, GTA5Keys.PC_AES_KEY);
        }

        public static byte[] EncryptAES(byte[] data)
        {
            return EncryptAESData(data, GTA5Keys.PC_AES_KEY);
        }

        public static byte[] DecryptAESData(byte[] data, byte[] key, int rounds = 1)
        {
            if (data == null) return null;
            var buffer = (byte[])data.Clone();
            DecryptAESDataInPlace(buffer, 0, buffer.Length, key, rounds);
            return buffer;
        }

        public static void DecryptAESDataInPlace(byte[] buffer, int offset, int length, byte[] key, int rounds = 1)
        {
            if (buffer == null || length <= 0 || key == null) return;
            int alignedLength = length - (length % 16);
            if (alignedLength <= 0) return;

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                aes.Key = key;

                using (var decryptor = aes.CreateDecryptor())
                {
                    for (int roundIndex = 0; roundIndex < rounds; roundIndex++)
                    {
                        decryptor.TransformBlock(buffer, offset, alignedLength, buffer, offset);
                    }
                }
            }
        }

        public static byte[] EncryptAESData(byte[] data, byte[] key, int rounds = 1)
        {
            if (data == null) return null;
            var buffer = (byte[])data.Clone();
            EncryptAESDataInPlace(buffer, 0, buffer.Length, key, rounds);
            return buffer;
        }

        public static void EncryptAESDataInPlace(byte[] buffer, int offset, int length, byte[] key, int rounds = 1)
        {
            if (buffer == null || length <= 0 || key == null) return;
            int alignedLength = length - (length % 16);
            if (alignedLength <= 0) return;

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                aes.Key = key;

                using (var encryptor = aes.CreateEncryptor())
                {
                    for (int roundIndex = 0; roundIndex < rounds; roundIndex++)
                    {
                        encryptor.TransformBlock(buffer, offset, alignedLength, buffer, offset);
                    }
                }
            }
        }

        public static byte[] GetNGKey(string name, uint length)
        {
            uint hash = GTA5Hash.CalculateHash(name);
            uint keyidx = (hash + length + (101 - 40)) % 0x65;
            return GTA5Keys.PC_NG_KEYS[keyidx];
        }

        public static uint[] GetNGKeyUints(string name, uint length)
        {
            uint hash = GTA5Hash.CalculateHash(name);
            uint keyidx = (hash + length + (101 - 40)) % 0x65;
            if (GTA5Keys.PC_NG_KEYS_UINTS != null && keyidx < GTA5Keys.PC_NG_KEYS_UINTS.Length)
            {
                return GTA5Keys.PC_NG_KEYS_UINTS[keyidx];
            }
            byte[] key = GTA5Keys.PC_NG_KEYS[keyidx];
            var keyuints = new uint[key.Length / 4];
            Buffer.BlockCopy(key, 0, keyuints, 0, key.Length);
            return keyuints;
        }

        public static byte[] DecryptNG(byte[] data, string name, uint length)
        {
            uint[] keyuints = GetNGKeyUints(name, length);
            return DecryptNG(data, keyuints);
        }

        public static byte[] DecryptNG(byte[] data, byte[] key)
        {
            var keyuints = new uint[key.Length / 4];
            Buffer.BlockCopy(key, 0, keyuints, 0, key.Length);
            return DecryptNG(data, keyuints);
        }

        public static byte[] DecryptNG(byte[] data, uint[] keyuints)
        {
            if (data == null) return null;
            var decryptedData = new byte[data.Length];
            DecryptNGInternal(data, 0, data.Length, decryptedData, 0, keyuints);
            return decryptedData;
        }

        public static void DecryptNGInPlace(byte[] data, int offset, int length, uint[] keyuints)
        {
            if (data == null || length <= 0) return;
            DecryptNGInternal(data, offset, length, data, offset, keyuints);
        }

        public static void DecryptNGInPlace(byte[] data, int offset, int length, byte[] key)
        {
            if (data == null || length <= 0 || key == null) return;
            var keyuints = new uint[key.Length / 4];
            Buffer.BlockCopy(key, 0, keyuints, 0, key.Length);
            DecryptNGInternal(data, offset, length, data, offset, keyuints);
        }

        private static void DecryptNGInternal(byte[] src, int srcOffset, int length, byte[] dst, int dstOffset, uint[] keyuints)
        {
            var tables = GTA5Keys.PC_NG_DECRYPT_TABLES;
            int numBlocks = length / 16;

            if (numBlocks > 512)
            {
                Parallel.For(0, numBlocks, blockIndex =>
                {
                    DecryptBlockFast(src, srcOffset + blockIndex * 16, dst, dstOffset + blockIndex * 16, keyuints, tables);
                });
            }
            else
            {
                for (int blockIndex = 0; blockIndex < numBlocks; blockIndex++)
                {
                    DecryptBlockFast(src, srcOffset + blockIndex * 16, dst, dstOffset + blockIndex * 16, keyuints, tables);
                }
            }

            int left = length % 16;
            if (left != 0)
            {
                Buffer.BlockCopy(src, srcOffset + length - left, dst, dstOffset + length - left, left);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DecryptBlockFast(byte[] src, int srcOffset, byte[] dst, int dstOffset, uint[] keyuints, uint[][][] tab)
        {
            byte d0 = src[srcOffset + 0];
            byte d1 = src[srcOffset + 1];
            byte d2 = src[srcOffset + 2];
            byte d3 = src[srcOffset + 3];
            byte d4 = src[srcOffset + 4];
            byte d5 = src[srcOffset + 5];
            byte d6 = src[srcOffset + 6];
            byte d7 = src[srcOffset + 7];
            byte d8 = src[srcOffset + 8];
            byte d9 = src[srcOffset + 9];
            byte d10 = src[srcOffset + 10];
            byte d11 = src[srcOffset + 11];
            byte d12 = src[srcOffset + 12];
            byte d13 = src[srcOffset + 13];
            byte d14 = src[srcOffset + 14];
            byte d15 = src[srcOffset + 15];

            // Round 0 (RoundA)
            var t = tab[0];
            uint x1 = t[0][d0] ^ t[1][d1] ^ t[2][d2] ^ t[3][d3] ^ keyuints[0];
            uint x2 = t[4][d4] ^ t[5][d5] ^ t[6][d6] ^ t[7][d7] ^ keyuints[1];
            uint x3 = t[8][d8] ^ t[9][d9] ^ t[10][d10] ^ t[11][d11] ^ keyuints[2];
            uint x4 = t[12][d12] ^ t[13][d13] ^ t[14][d14] ^ t[15][d15] ^ keyuints[3];

            // Round 1 (RoundA)
            t = tab[1];
            uint nx1 = t[0][(byte)x1] ^ t[1][(byte)(x1 >> 8)] ^ t[2][(byte)(x1 >> 16)] ^ t[3][(byte)(x1 >> 24)] ^ keyuints[4];
            uint nx2 = t[4][(byte)x2] ^ t[5][(byte)(x2 >> 8)] ^ t[6][(byte)(x2 >> 16)] ^ t[7][(byte)(x2 >> 24)] ^ keyuints[5];
            uint nx3 = t[8][(byte)x3] ^ t[9][(byte)(x3 >> 8)] ^ t[10][(byte)(x3 >> 16)] ^ t[11][(byte)(x3 >> 24)] ^ keyuints[6];
            uint nx4 = t[12][(byte)x4] ^ t[13][(byte)(x4 >> 8)] ^ t[14][(byte)(x4 >> 16)] ^ t[15][(byte)(x4 >> 24)] ^ keyuints[7];
            x1 = nx1; x2 = nx2; x3 = nx3; x4 = nx4;

            // Rounds 2..15 (RoundB)
            for (int k = 2; k <= 15; k++)
            {
                t = tab[k];
                int kIdx = k * 4;
                nx1 = t[0][(byte)x1] ^ t[7][(byte)(x2 >> 24)] ^ t[10][(byte)(x3 >> 16)] ^ t[13][(byte)(x4 >> 8)] ^ keyuints[kIdx + 0];
                nx2 = t[1][(byte)(x1 >> 8)] ^ t[4][(byte)x2] ^ t[11][(byte)(x3 >> 24)] ^ t[14][(byte)(x4 >> 16)] ^ keyuints[kIdx + 1];
                nx3 = t[2][(byte)(x1 >> 16)] ^ t[5][(byte)(x2 >> 8)] ^ t[8][(byte)x3] ^ t[15][(byte)(x4 >> 24)] ^ keyuints[kIdx + 2];
                nx4 = t[3][(byte)(x1 >> 24)] ^ t[6][(byte)(x2 >> 16)] ^ t[9][(byte)(x3 >> 8)] ^ t[12][(byte)x4] ^ keyuints[kIdx + 3];
                x1 = nx1; x2 = nx2; x3 = nx3; x4 = nx4;
            }

            // Round 16 (RoundA)
            t = tab[16];
            nx1 = t[0][(byte)x1] ^ t[1][(byte)(x1 >> 8)] ^ t[2][(byte)(x1 >> 16)] ^ t[3][(byte)(x1 >> 24)] ^ keyuints[64];
            nx2 = t[4][(byte)x2] ^ t[5][(byte)(x2 >> 8)] ^ t[6][(byte)(x2 >> 16)] ^ t[7][(byte)(x2 >> 24)] ^ keyuints[65];
            nx3 = t[8][(byte)x3] ^ t[9][(byte)(x3 >> 8)] ^ t[10][(byte)(x3 >> 16)] ^ t[11][(byte)(x3 >> 24)] ^ keyuints[66];
            nx4 = t[12][(byte)x4] ^ t[13][(byte)(x4 >> 8)] ^ t[14][(byte)(x4 >> 16)] ^ t[15][(byte)(x4 >> 24)] ^ keyuints[67];

            dst[dstOffset + 0] = (byte)nx1;
            dst[dstOffset + 1] = (byte)(nx1 >> 8);
            dst[dstOffset + 2] = (byte)(nx1 >> 16);
            dst[dstOffset + 3] = (byte)(nx1 >> 24);

            dst[dstOffset + 4] = (byte)nx2;
            dst[dstOffset + 5] = (byte)(nx2 >> 8);
            dst[dstOffset + 6] = (byte)(nx2 >> 16);
            dst[dstOffset + 7] = (byte)(nx2 >> 24);

            dst[dstOffset + 8] = (byte)nx3;
            dst[dstOffset + 9] = (byte)(nx3 >> 8);
            dst[dstOffset + 10] = (byte)(nx3 >> 16);
            dst[dstOffset + 11] = (byte)(nx3 >> 24);

            dst[dstOffset + 12] = (byte)nx4;
            dst[dstOffset + 13] = (byte)(nx4 >> 8);
            dst[dstOffset + 14] = (byte)(nx4 >> 16);
            dst[dstOffset + 15] = (byte)(nx4 >> 24);
        }

        public static byte[] DecryptNGBlock(byte[] data, uint[] key)
        {
            var res = new byte[16];
            DecryptBlockFast(data, 0, res, 0, key, GTA5Keys.PC_NG_DECRYPT_TABLES);
            return res;
        }

        // round 1,2,16 (preserved for compatibility)
        public static byte[] DecryptNGRoundA(byte[] data, uint[] key, uint[][] table)
        {
            var x1 =
                table[0][data[0]] ^
                table[1][data[1]] ^
                table[2][data[2]] ^
                table[3][data[3]] ^
                key[0];
            var x2 =
                table[4][data[4]] ^
                table[5][data[5]] ^
                table[6][data[6]] ^
                table[7][data[7]] ^
                key[1];
            var x3 =
                table[8][data[8]] ^
                table[9][data[9]] ^
                table[10][data[10]] ^
                table[11][data[11]] ^
                key[2];
            var x4 =
                table[12][data[12]] ^
                table[13][data[13]] ^
                table[14][data[14]] ^
                table[15][data[15]] ^
                key[3];

            return new byte[] {
                (byte)((x1 >> 0) & 0xFF),
                (byte)((x1 >> 8) & 0xFF),
                (byte)((x1 >> 16) & 0xFF),
                (byte)((x1 >> 24) & 0xFF),
                (byte)((x2 >> 0) & 0xFF),
                (byte)((x2 >> 8) & 0xFF),
                (byte)((x2 >> 16) & 0xFF),
                (byte)((x2 >> 24) & 0xFF),
                (byte)((x3 >> 0) & 0xFF),
                (byte)((x3 >> 8) & 0xFF),
                (byte)((x3 >> 16) & 0xFF),
                (byte)((x3 >> 24) & 0xFF),
                (byte)((x4 >> 0) & 0xFF),
                (byte)((x4 >> 8) & 0xFF),
                (byte)((x4 >> 16) & 0xFF),
                (byte)((x4 >> 24) & 0xFF),
            };
        }

        // round 3-15 (preserved for compatibility)
        public static byte[] DecryptNGRoundB(byte[] data, uint[] key, uint[][] table)
        {
            var x1 =
                table[0][data[0]] ^
                table[7][data[7]] ^
                table[10][data[10]] ^
                table[13][data[13]] ^
                key[0];
            var x2 =
                table[1][data[1]] ^
                table[4][data[4]] ^
                table[11][data[11]] ^
                table[14][data[14]] ^
                key[1];
            var x3 =
                table[2][data[2]] ^
                table[5][data[5]] ^
                table[8][data[8]] ^
                table[15][data[15]] ^
                key[2];
            var x4 =
                table[3][data[3]] ^
                table[6][data[6]] ^
                table[9][data[9]] ^
                table[12][data[12]] ^
                key[3];

            return new byte[] {
                (byte)((x1 >> 0) & 0xFF),
                (byte)((x1 >> 8) & 0xFF),
                (byte)((x1 >> 16) & 0xFF),
                (byte)((x1 >> 24) & 0xFF),
                (byte)((x2 >> 0) & 0xFF),
                (byte)((x2 >> 8) & 0xFF),
                (byte)((x2 >> 16) & 0xFF),
                (byte)((x2 >> 24) & 0xFF),
                (byte)((x3 >> 0) & 0xFF),
                (byte)((x3 >> 8) & 0xFF),
                (byte)((x3 >> 16) & 0xFF),
                (byte)((x3 >> 24) & 0xFF),
                (byte)((x4 >> 0) & 0xFF),
                (byte)((x4 >> 8) & 0xFF),
                (byte)((x4 >> 16) & 0xFF),
                (byte)((x4 >> 24) & 0xFF),
            };
        }

        public static byte[] EncryptNG(byte[] data, string name, uint length)
        {
            byte[] key = GetNGKey(name, length);
            return EncryptNG(data, key);
        }

        public static byte[] EncryptNG(byte[] data, byte[] key)
        {
            var encryptedData = new byte[data.Length];

            var keyuints = new uint[key.Length / 4];
            Buffer.BlockCopy(key, 0, keyuints, 0, key.Length);

            for (int blockIndex = 0; blockIndex < data.Length / 16; blockIndex++)
            {
                var decryptedBlock = new byte[16];
                Array.Copy(data, 16 * blockIndex, decryptedBlock, 0, 16);
                var encryptedBlock = EncryptNGBlock(decryptedBlock, keyuints);
                Array.Copy(encryptedBlock, 0, encryptedData, 16 * blockIndex, 16);
            }

            if (data.Length % 16 != 0)
            {
                var left = data.Length % 16;
                Buffer.BlockCopy(data, data.Length - left, encryptedData, data.Length - left, left);
            }

            return encryptedData;
        }

        public static byte[] EncryptNGBlock(byte[] data, uint[] key)
        {
            var buffer = data;

            var subKeys = new uint[17][];
            for (int i = 0; i < 17; i++)
            {
                subKeys[i] = new uint[4];
                subKeys[i][0] = key[4 * i + 0];
                subKeys[i][1] = key[4 * i + 1];
                subKeys[i][2] = key[4 * i + 2];
                subKeys[i][3] = key[4 * i + 3];
            }

            buffer = EncryptRoundA(buffer, subKeys[16], GTA5Keys.PC_NG_ENCRYPT_TABLES[16]);
            for (int k = 15; k >= 2; k--)
            {
                buffer = EncryptRoundB_LUT(buffer, subKeys[k], GTA5Keys.PC_NG_ENCRYPT_LUTs[k]);
            }
            buffer = EncryptRoundA(buffer, subKeys[1], GTA5Keys.PC_NG_ENCRYPT_TABLES[1]);
            buffer = EncryptRoundA(buffer, subKeys[0], GTA5Keys.PC_NG_ENCRYPT_TABLES[0]);

            return buffer;
        }

        public static byte[] EncryptRoundA(byte[] data, uint[] key, uint[][] table)
        {
            var x1 =
                table[0][data[0]] ^
                table[1][data[1]] ^
                table[2][data[2]] ^
                table[3][data[3]] ^
                key[0];
            var x2 =
                table[4][data[4]] ^
                table[5][data[5]] ^
                table[6][data[6]] ^
                table[7][data[7]] ^
                key[1];
            var x3 =
                table[8][data[8]] ^
                table[9][data[9]] ^
                table[10][data[10]] ^
                table[11][data[11]] ^
                key[2];
            var x4 =
                table[12][data[12]] ^
                table[13][data[13]] ^
                table[14][data[14]] ^
                table[15][data[15]] ^
                key[3];

            var buf = new byte[16];
            Array.Copy(BitConverter.GetBytes(x1), 0, buf, 0, 4);
            Array.Copy(BitConverter.GetBytes(x2), 0, buf, 4, 4);
            Array.Copy(BitConverter.GetBytes(x3), 0, buf, 8, 4);
            Array.Copy(BitConverter.GetBytes(x4), 0, buf, 12, 4);
            return buf;
        }

        public static byte[] EncryptRoundA_LUT(byte[] dataOld, uint[] key, GTA5NGLUT[] lut)
        {
            var data = (byte[])dataOld.Clone();
            var xorbuf = new byte[16];
            Buffer.BlockCopy(key, 0, xorbuf, 0, 16);
            for (int y = 0; y < 16; y++)
            {
                data[y] ^= xorbuf[y];
            }

            return new byte[] {
                lut[0].LookUp(BitConverter.ToUInt32( new byte[] {  data[0],  data[1],  data[2],  data[3]  }, 0)),
                lut[1].LookUp(BitConverter.ToUInt32( new byte[] {  data[0],  data[1],  data[2],  data[3]  }, 0)),
                lut[2].LookUp(BitConverter.ToUInt32( new byte[] {  data[0],  data[1],  data[2],  data[3]  }, 0)),
                lut[3].LookUp(BitConverter.ToUInt32( new byte[] {  data[0],  data[1],  data[2],  data[3]  }, 0)),
                lut[4].LookUp(BitConverter.ToUInt32( new byte[] {  data[4],  data[5],  data[6],  data[7]  }, 0)),
                lut[5].LookUp(BitConverter.ToUInt32( new byte[] {  data[4],  data[5],  data[6],  data[7]  }, 0)),
                lut[6].LookUp(BitConverter.ToUInt32( new byte[] {  data[4],  data[5],  data[6],  data[7]  }, 0)),
                lut[7].LookUp(BitConverter.ToUInt32( new byte[] {  data[4],  data[5],  data[6],  data[7]  }, 0)),
                lut[8].LookUp(BitConverter.ToUInt32( new byte[] {  data[8],  data[9],  data[10], data[11] }, 0)),
                lut[9].LookUp(BitConverter.ToUInt32( new byte[] {  data[8],  data[9],  data[10], data[11] }, 0)),
                lut[10].LookUp(BitConverter.ToUInt32( new byte[] { data[8],  data[9],  data[10], data[11] }, 0)),
                lut[11].LookUp(BitConverter.ToUInt32( new byte[] { data[8],  data[9],  data[10], data[11] }, 0)),
                lut[12].LookUp(BitConverter.ToUInt32( new byte[] { data[12], data[13], data[14], data[15] }, 0)),
                lut[13].LookUp(BitConverter.ToUInt32( new byte[] { data[12], data[13], data[14], data[15] }, 0)),
                lut[14].LookUp(BitConverter.ToUInt32( new byte[] { data[12], data[13], data[14], data[15] }, 0)),
                lut[15].LookUp(BitConverter.ToUInt32( new byte[] { data[12], data[13], data[14], data[15] }, 0))
             };
        }

        public static byte[] EncryptRoundB_LUT(byte[] dataOld, uint[] key, GTA5NGLUT[] lut)
        {
            var data = (byte[])dataOld.Clone();
            var xorbuf = new byte[16];
            Buffer.BlockCopy(key, 0, xorbuf, 0, 16);
            for (int y = 0; y < 16; y++)
            {
                data[y] ^= xorbuf[y];
            }

            return new byte[] {
                lut[0].LookUp(BitConverter.ToUInt32( new byte[] {  data[0],  data[1],  data[2],  data[3]  }, 0)),
                lut[1].LookUp(BitConverter.ToUInt32( new byte[] {  data[4],  data[5],  data[6],  data[7]  }, 0)),
                lut[2].LookUp(BitConverter.ToUInt32( new byte[] {  data[8],  data[9],  data[10], data[11] }, 0)),
                lut[3].LookUp(BitConverter.ToUInt32( new byte[] {  data[12], data[13], data[14], data[15] }, 0)),
                lut[4].LookUp(BitConverter.ToUInt32( new byte[] {  data[4],  data[5],  data[6],  data[7]  }, 0)),
                lut[5].LookUp(BitConverter.ToUInt32( new byte[] {  data[8],  data[9],  data[10], data[11] }, 0)),
                lut[6].LookUp(BitConverter.ToUInt32( new byte[] {  data[12], data[13], data[14], data[15] }, 0)),
                lut[7].LookUp(BitConverter.ToUInt32( new byte[] {  data[0],  data[1],  data[2],  data[3]  }, 0)),
                lut[8].LookUp(BitConverter.ToUInt32( new byte[] {  data[8],  data[9],  data[10], data[11] }, 0)),
                lut[9].LookUp(BitConverter.ToUInt32( new byte[] {  data[12], data[13], data[14], data[15] }, 0)),
                lut[10].LookUp(BitConverter.ToUInt32( new byte[] { data[0],  data[1],  data[2],  data[3]  }, 0)),
                lut[11].LookUp(BitConverter.ToUInt32( new byte[] { data[4],  data[5],  data[6],  data[7]  }, 0)),
                lut[12].LookUp(BitConverter.ToUInt32( new byte[] { data[12], data[13], data[14], data[15] }, 0)),
                lut[13].LookUp(BitConverter.ToUInt32( new byte[] { data[0],  data[1],  data[2],  data[3]  }, 0)),
                lut[14].LookUp(BitConverter.ToUInt32( new byte[] { data[4],  data[5], data[6],   data[7]  }, 0)),
                lut[15].LookUp(BitConverter.ToUInt32( new byte[] { data[8],  data[9], data[10], data[11]  }, 0))};
        }
    }
}
