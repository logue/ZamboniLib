// Decompiled with JetBrains decompiler
// Type: psu_generic_parser.PrsCompressor
// Assembly: zamboni, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 73B487C9-8F41-4586-BEF5-F7D7BFBD4C55
// Assembly location: D:\Downloads\zamboni_ngs (3)\zamboni.exe

using System;
using System.Collections.Generic;

namespace psu_generic_parser
{
    public class PrsCompressor
    {
        private byte[] compBuffer;
        private int ctrlByteCounter;
        private int outLoc;
        private int ctrlBitCounter;
        private readonly Tuple<List<int>, int> emptyTuple = new Tuple<List<int>, int>(new List<int>(), 0);

        public byte[] Compress(byte[] toCompress)
        {
            Dictionary<byte, Tuple<List<int>, int>> offsetDictionary = BuildOffsetDictionary(toCompress);
            ctrlByteCounter = 0;
            int length = toCompress.Length;
            compBuffer = new byte[length];
            outLoc = 3;
            ctrlBitCounter = 2;
            compBuffer[0] = 3;
            Array.Copy(toCompress, 0, compBuffer, 1, 2);
            int currentOffset = 2;
            while (currentOffset < length)
            {
                Tuple<List<int>, int> offsetList = GetOffsetList(offsetDictionary, toCompress[currentOffset], currentOffset);
                int count = 2;
                int num1 = -1;
                int num2 = currentOffset - 256;
                for (int index = offsetList.Item2; index < offsetList.Item1.Count && offsetList.Item1[index] < currentOffset; ++index)
                {
                    int num3 = offsetList.Item1[index];
                    int num4 = 0;
                    int num5 = Math.Min(length - currentOffset, 256);
                    while (num4 < num5 && toCompress[num3 + num4] == toCompress[currentOffset + num4])
                        ++num4;
                    if ((num4 > 2 || num3 > num2) && (num4 > count || num4 == count && num3 > num1))
                    {
                        count = num4;
                        num1 = num3;
                    }
                }
                if (num1 == -1 || currentOffset - num1 > 256 && count < 3)
                {
                    WriteRawByte(toCompress[currentOffset++]);
                }
                else
                {
                    if (count < 6 && currentOffset - num1 < 256)
                        WriteShortReference(count, (byte)(num1 - (currentOffset - 256)));
                    else
                        WriteLongReference(count, num1 - (currentOffset - 8192));
                    currentOffset += count;
                }
            }
            FinalizeCompression();
            Array.Resize(ref compBuffer, outLoc);
            return compBuffer;
        }

        private Tuple<List<int>, int> GetOffsetList(
          Dictionary<byte, Tuple<List<int>, int>> offsetDictionary,
          byte currentVal,
          int currentOffset)
        {
            Tuple<List<int>, int> offset = offsetDictionary[currentVal];
            if (offset == null)
                return emptyTuple;
            if (offset.Item2 < currentOffset - 8176)
            {
                int index = offset.Item2;
                while (offset.Item1[index] < currentOffset - 8176 && index < offset.Item1.Count)
                    ++index;
                Tuple<List<int>, int> tuple = new Tuple<List<int>, int>(offset.Item1, index);
                offsetDictionary[currentVal] = tuple;
            }
            return offsetDictionary[currentVal];
        }

        private Dictionary<byte, Tuple<List<int>, int>> BuildOffsetDictionary(
          byte[] toCompress)
        {
            Dictionary<byte, Tuple<List<int>, int>> dictionary = new Dictionary<byte, Tuple<List<int>, int>>();
            for (int index = 0; index < toCompress.Length; ++index)
            {
                byte key = toCompress[index];
                if (!dictionary.ContainsKey(key))
                    dictionary.Add(key, new Tuple<List<int>, int>(new List<int>(), 0));
                dictionary[key].Item1.Add(index);
            }
            return dictionary;
        }

        private void FinalizeCompression()
        {
            AddCtrlBit(0);
            AddCtrlBit(1);
            compBuffer[outLoc++] = 0;
            compBuffer[outLoc++] = 0;
        }

        private void WriteRawByte(byte val)
        {
            AddCtrlBit(1);
            compBuffer[outLoc++] = val;
        }

        private void WriteShortReference(int count, byte offset)
        {
            AddCtrlBit(0);
            AddCtrlBit(0);
            AddCtrlBit(count - 2 >> 1);
            AddCtrlBit(count - 2 & 1);
            compBuffer[outLoc++] = offset;
        }

        private void WriteLongReference(int count, int offset)
        {
            AddCtrlBit(0);
            AddCtrlBit(1);
            ushort num = (ushort)(offset << 3);
            if (count <= 9)
                num |= (ushort)(count - 2);
            BitConverter.GetBytes(num).CopyTo(compBuffer, outLoc);
            outLoc += 2;
            if (count <= 9)
                return;
            compBuffer[outLoc++] = (byte)(count - 10);
        }

        private void AddCtrlBit(int input)
        {
            if (ctrlBitCounter == 8)
            {
                ctrlBitCounter = 0;
                ctrlByteCounter = outLoc++;
            }
            compBuffer[ctrlByteCounter] |= (byte)(input << ctrlBitCounter);
            ++ctrlBitCounter;
        }

        private class CompressionBuffer
        {
            /*
            private readonly byte[] buffer;
            private readonly int ctrlByteCounter;
            private readonly int outLoc;
            private readonly int ctrlBitCounter;
            */
        }

        private interface ICompressionChunk
        {
            void Encode(CompressionBuffer buff);
        }
    }
}