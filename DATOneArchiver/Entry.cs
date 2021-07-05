using QuesoStruct;
using QuesoStruct.Types.Pointers;
using QuesoStruct.Types.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DATOneArchiver
{
    [StructType]
    public partial class Entry : IPointerOwner
    {
        public bool IsNullPointer(IStructReference refr) => false;
        public void SetNullPointer(IStructReference refr) { }

        public IStructInstance RelativeOffsetBase => (Parent.Parent as FileTable).Names.Strings;
        public long AddedOffsetFromBase => 0;

        [StructMember]
        public short BlobIndex { get; set; }

        [StructMember]
        public short NodeIndex { get; set; }

        [StructMember]
        [AutoInitialize]
        public UInt32Pointer<NullTerminatingString> NodeName { get; set; }
    }
}
