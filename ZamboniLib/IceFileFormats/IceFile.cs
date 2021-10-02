// Decompiled with JetBrains decompiler
// Type: zamboni.IceFile
// Assembly: zamboni, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 73B487C9-8F41-4586-BEF5-F7D7BFBD4C55
// Assembly location: D:\Downloads\zamboni_ngs (3)\zamboni.exe

using PhilLibX.Compression;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ZamboniLib.IceFileFormats
{
    public abstract class IceFile
    {
        protected int decryptShift = 16;

        public byte[][] GroupOneFiles { get; set; }

        public byte[][] GroupTwoFiles { get; set; }

        public byte[] Header { get; set; }

        protected abstract int SecondPassThreshold { get; }

        /// <summary>
        /// Load Ice file.
        /// </summary>
        /// <param name="fileName">path of icefile</param>
        /// <returns></returns>
        public static IceFile LoadIceFile(string fileName)
        {
            using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                return LoadIceFile(fs);
            }
        }

        /// <summary>
        /// Load Ice file by stream
        /// </summary>
        /// <param name="fileName">path of icefile</param>
        /// <returns></returns>
        public static IceFile LoadIceFile(Stream inStream)
        {
            byte[] buffer = new byte[inStream.Length];

            if (buffer.Length <= 127 || buffer[0] != 73 || buffer[1] != 67 || buffer[2] != 69 || buffer[3] != 0)
            {
                throw new ZamboniException("Not an Ice file.");
            }

            inStream.Seek(8L, SeekOrigin.Begin);
            int num = inStream.ReadByte();
            inStream.Seek(0L, SeekOrigin.Begin);
            IceFile iceFile;
            switch (num)
            {
                case 3:
                    iceFile = new IceV3File(inStream);
                    break;
                case 4:
                    iceFile = new IceV4File(inStream);
                    break;
                case 5:
                    iceFile = new IceV5File(inStream);
                    break;
                case 6:
                    iceFile = new IceV5File(inStream);
                    break;
                case 7:
                    iceFile = new IceV5File(inStream);
                    break;
                case 8:
                    iceFile = new IceV5File(inStream);
                    break;
                case 9:
                    iceFile = new IceV5File(inStream);
                    break;
                default:
                    throw new ZamboniException("Invalid ICE version: " + num.ToString());
            }
            inStream.Dispose();

            if (iceFile == null)
            {
                throw new ZamboniException("Could not parse ice file.");
            }
            return iceFile;
        }

        public static string GetFileName(byte[] fileToWrite)
        {
            int int32 = BitConverter.ToInt32(fileToWrite, 0x10);
            return Encoding.ASCII.GetString(fileToWrite, 0x40, int32).TrimEnd(new char[1]);
        }

        protected static byte[][] SplitGroup(byte[] groupToSplit, int fileCount)
        {
            byte[][] numArray = new byte[fileCount][];
            int sourceIndex = 0;
            for (int index = 0; index < fileCount && sourceIndex < groupToSplit.Length; ++index)
            {
                int int32 = BitConverter.ToInt32(groupToSplit, sourceIndex + 4);
                numArray[index] = new byte[int32];
                Array.Copy(groupToSplit, sourceIndex, numArray[index], 0, int32);
                sourceIndex += int32;
            }
            return numArray;
        }

        protected static byte[] CombineGroup(byte[][] filesToJoin, bool headerLess = true)
        {
            List<byte> outBytes = new List<byte>();
            for (int i = 0; i < filesToJoin.Length; i++)
            {
                outBytes.AddRange(filesToJoin[i]);
            }

            return outBytes.ToArray();
        }

        protected byte[] DecryptGroup(byte[] buffer, uint key1, uint key2, bool v3Decrypt)
        {
            byte[] block1 = new byte[buffer.Length];
            if (v3Decrypt == false)
            {
                block1 = FloatageFish.DecryptBlock(buffer, (uint)buffer.Length, key1, decryptShift);
            }
            else
            {
                Array.Copy(buffer, 0, block1, 0, buffer.Length);
            }
            byte[] block2 = new BlewFish(ReverseBytes(key1)).DecryptBlock(block1);
            byte[] numArray = block2;
            if (block2.Length <= SecondPassThreshold && v3Decrypt == false)
                numArray = new BlewFish(ReverseBytes(key2)).DecryptBlock(block2);
            return numArray;
        }

        public static uint ReverseBytes(uint x)
        {
            x = x >> 16 | x << 16;
            return (x & 4278255360U) >> 8 | (uint)(((int)x & 16711935) << 8);
        }

        protected static GroupHeader[] ReadHeaders(byte[] decryptedHeaderData)
        {
            GroupHeader[] groupHeaderArray = new GroupHeader[2]
            {
                new GroupHeader(),
                null
            };
            groupHeaderArray[0].decompSize = BitConverter.ToUInt32(decryptedHeaderData, 0);
            groupHeaderArray[0].compSize = BitConverter.ToUInt32(decryptedHeaderData, 4);
            groupHeaderArray[0].count = BitConverter.ToUInt32(decryptedHeaderData, 8);
            groupHeaderArray[0].CRC = BitConverter.ToUInt32(decryptedHeaderData, 12);
            groupHeaderArray[1] = new GroupHeader();
            groupHeaderArray[1].decompSize = BitConverter.ToUInt32(decryptedHeaderData, 16);
            groupHeaderArray[1].compSize = BitConverter.ToUInt32(decryptedHeaderData, 20);
            groupHeaderArray[1].count = BitConverter.ToUInt32(decryptedHeaderData, 24);
            groupHeaderArray[1].CRC = BitConverter.ToUInt32(decryptedHeaderData, 28);
            return groupHeaderArray;
        }

        protected byte[] ExtractGroup(
          GroupHeader header,
          BinaryReader openReader,
          bool encrypt,
          uint groupOneTempKey,
          uint groupTwoTempKey,
          bool ngsMode,
          bool v3Decrypt = false)
        {
            byte[] buffer = openReader.ReadBytes((int)header.GetStoredSize());
            byte[] inData = !encrypt ? buffer : DecryptGroup(buffer, groupOneTempKey, groupTwoTempKey, v3Decrypt);
            return header.compSize <= 0U ? inData :
                (!ngsMode ? DecompressGroup(inData, header.decompSize) :
                DecompressGroupNgs(inData, header.decompSize));
        }

        protected static byte[] DecompressGroup(byte[] inData, uint bufferLength)
        {
            byte[] input = new byte[inData.Length];
            Array.Copy(inData, input, input.Length);
            for (int index = 0; index < input.Length; ++index)
                input[index] ^= 149;
            return PrsCompDecomp.Decompress(input, bufferLength);
        }

        protected static byte[] DecompressGroupNgs(byte[] inData, uint bufferLength) => Oodle.Decompress(inData, bufferLength);

        protected static byte[] GetCompressedContents(byte[] buffer, bool compress)
        {
            if (!compress || (uint)buffer.Length <= 0U)
                return buffer;
            byte[] numArray = PrsCompDecomp.Compress(buffer);
            for (int index = 0; index < numArray.Length; ++index)
                numArray[index] ^= 149;
            return numArray;
        }

        protected byte[] PackGroup(byte[] buffer, uint key1, uint key2, bool encrypt)
        {
            if (!encrypt)
                return buffer;
            byte[] block = buffer;
            if (buffer.Length <= SecondPassThreshold)
                block = new BlewFish(ReverseBytes(key2)).EncryptBlock(buffer);
            byte[] data_block = new BlewFish(ReverseBytes(key1)).EncryptBlock(block);
            return FloatageFish.DecryptBlock(data_block, (uint)data_block.Length, key1);
        }

        public class GroupHeader
        {
            public uint decompSize;
            public uint compSize;
            public uint count;
            public uint CRC;

            public uint GetStoredSize() => compSize > 0U ? compSize : decompSize;
        }
    }
}