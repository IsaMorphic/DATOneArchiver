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
                this.magic = value;

                var bytes = new Span<byte>(BitConverter.GetBytes(this.magic));
                var magic = bytes.Slice(1, 3); magic.Reverse();

                IsValid = Encoding.ASCII.GetString(magic) == "RNC";
            }
        }
        private uint magic;

        public byte Version { get => BitConverter.GetBytes(magic)[0]; set => magic = (magic & 0xFF) | value; }
        public bool IsValid { get; private set; }

        [StructMember]
        public uint UnpackedLength { get; private set; }

        [StructMember]
        public uint PackedLength { get; private set; }
    }
}
