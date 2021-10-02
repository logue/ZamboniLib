using System.IO;

namespace ZamboniLib.IceFileFormats
{
    public class IceV3File : IceFile
    {
        public int groupOneCount = 0;
        public int groupTwoCount = 0;
        protected override int SecondPassThreshold => 102400;

        //Structs based on ice.exe naming
        public struct Group
        {
            public uint originalSize;
            public uint dataSize;
            public uint fileCount;
            public uint crc32;
        }
        public struct StGroup
        {
            public GroupHeader group1;
            public GroupHeader group2;
            public uint group1Size;
            public uint group2Size;
            public uint key;
            public uint reserve;
        }

        public struct StInfo
        {
            public uint r1;
            public uint crc32;
            public uint r2;
            public uint filesize;
        }

        public IceV3File(Stream inFile)
        {
            byte[][] numArray = SplitGroups(inFile);
            Header = numArray[0];
            GroupOneFiles = SplitGroup(numArray[1], groupOneCount);
            GroupTwoFiles = SplitGroup(numArray[2], groupTwoCount);
        }

        public IceV3File(byte[] headerData, byte[][] groupOneIn, byte[][] groupTwoIn)
        {
            Header = headerData;
            GroupOneFiles = groupOneIn;
            GroupTwoFiles = groupTwoIn;
        }

        private byte[][] SplitGroups(Stream inFile)
        {
            byte[][] numArray1 = new byte[3][];
            BinaryReader openReader = new BinaryReader(inFile);
            numArray1[0] = openReader.ReadBytes(128);

            inFile.Seek(0x10, SeekOrigin.Begin); //Skip the ICE header

            // Read group info
            StGroup groupInfo = new StGroup();
            groupInfo.group1 = new GroupHeader();
            groupInfo.group2 = new GroupHeader();
            ReadGroupInfoGroup(openReader, groupInfo.group1);
            ReadGroupInfoGroup(openReader, groupInfo.group2);
            groupInfo.group1Size = openReader.ReadUInt32();
            groupInfo.group2Size = openReader.ReadUInt32();
            groupInfo.key = openReader.ReadUInt32();
            groupInfo.key = openReader.ReadUInt32();

            // Read crypt info
            StInfo info = new StInfo
            {
                r1 = openReader.ReadUInt32(),
                crc32 = openReader.ReadUInt32(),
                r2 = openReader.ReadUInt32(),
                filesize = openReader.ReadUInt32()
            };

            // Seek past padding/unused data
            inFile.Seek(0x30, SeekOrigin.Current);

            // Generate key
            uint key = groupInfo.group1Size;
            if (key > 0)
            {
                key = ReverseBytes(key);
            }
            else if (info.r2 > 0)
            {
                key = (GetKey(groupInfo));
            }

            // Group 1
            if (groupInfo.group1.decompSize > 0)
            {
                numArray1[1] = ExtractGroup(groupInfo.group1, openReader, (info.r2 & 1) > 0U, key, 0, info.r2 == 8 || info.r2 == 9, true);
            }

            // Group 2
            if (groupInfo.group2.decompSize > 0)
            {
                numArray1[2] = ExtractGroup(groupInfo.group2, openReader, (info.r2 & 1) > 0U, key, 0, info.r2 == 8 || info.r2 == 9, true);
            }
            groupOneCount = (int)groupInfo.group1.count;
            groupTwoCount = (int)groupInfo.group2.count;

            return numArray1;
        }

        private static void ReadGroupInfoGroup(BinaryReader openReader, GroupHeader grp)
        {
            grp.decompSize = openReader.ReadUInt32();
            grp.compSize = openReader.ReadUInt32();
            grp.count = openReader.ReadUInt32();
            grp.CRC = openReader.ReadUInt32();
        }

        private static uint GetKey(StGroup group)
        {
            return group.group1.decompSize ^ group.group2.decompSize ^ group.group2Size ^ group.key ^ 0xC8D7469A;
        }
    }
}