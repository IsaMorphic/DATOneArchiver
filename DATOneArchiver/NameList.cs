using QuesoStruct;
using QuesoStruct.Types.Collections;
using QuesoStruct.Types.Pointers;
using QuesoStruct.Types.Primitives;

namespace DATOneArchiver
{
    [StructType]
    public partial class NameList : IPointerOwner, ICollectionOwner<NullTerminatingString>
    {
        public IStructInstance RelativeOffsetBase => this;
        public long AddedOffsetFromBase => 0;

        public bool IsNullPointer(IStructReference refr) => false;
        public void SetNullPointer(IStructReference refr) { }

        public bool TerminateOnStreamEnd => false;
        public bool IsTerminator(IStructInstance inst) => false;

        [StructMember]
        [AutoInitialize]
        public UInt32Pointer<NullTerminatingString> SectionLength { get; set; }

        [StructMember]
        public Collection<NullTerminatingString> Strings { get; set; }
        public long? ItemCount => (Parent as FileTable).NumEntries;

        [StructMember]
        [AutoInitialize]
        public NullTerminatingString Signature { get; set; }
    }
}
