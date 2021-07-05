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
        // TT Games 32 bit signing key.
        // I believe only certain specific values are valid in this field, it is unrelated to contents.
        public uint TTGKey { get; set; }
    }
}
