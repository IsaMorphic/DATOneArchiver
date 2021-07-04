using QuesoStruct;
using QuesoStruct.Types.Pointers;

namespace DATOneArchiver
{
    [StructType]
    public partial class DATFile : IPointerOwner
    {
        public IStructInstance RelativeOffsetBase => this;
        public long AddedOffsetFromBase => 0;

        public bool IsNullPointer(IStructReference refr) => false;
        public void SetNullPointer(IStructReference refr) { }

        [StructMember]
        [AutoInitialize]
        public UInt32Pointer<FileTable> TablePointer { get; set; }

        [StructMember]
        public uint Unk000 { get; set; }
    }
}
