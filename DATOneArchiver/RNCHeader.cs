using QuesoStruct;
using System;
using System.Linq;
using System.Text;

namespace DATOneArchiver
{
    [StructType]
    public partial class RNCHeader
    {
        [StructMember]
        public uint Magic
        {
            get => magic;
            set
            {
                var bytes = new Span<byte>(BitConverter.GetBytes(value));
                var magic = bytes.Slice(1, 3); magic.Reverse();

                if (Encoding.ASCII.GetString(magic) != "RNC")
                {
                    throw new InvalidOperationException("Incorrect RNC magic!");
                }

                this.magic = value;
            }
        }
        private uint magic;

        public byte Version { get => BitConverter.GetBytes(magic)[0]; set => magic = (magic & 0xFF) | value; }

        [StructMember]
        public uint UnpackedLength { get; private set; }

        [StructMember]
        public uint PackedLength { get; private set; }
    }
}
