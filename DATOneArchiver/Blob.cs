using QuesoStruct;
using QuesoStruct.Types.Pointers;
using QuesoStruct.Types.Primitives;

namespace DATOneArchiver
{
    [StructType]
    public partial class Blob : IPointerOwner, IBytesOwner
    {
        public IStructInstance RelativeOffsetBase => Parent.Parent.Parent;
        public long AddedOffsetFromBase => 0;

        public bool IsNullPointer(IStructReference refr) => false;
        public void SetNullPointer(IStructReference refr) { }

        [StructMember]
        [AutoInitialize]
        public UInt32Pointer<Bytes> Data { get; set; }
        public long BytesLength => Size32;

        [StructMember]
        public uint Size32 { get; set; }

        [StructMember]
        public ulong Size64 { get; set; }
    }
}
