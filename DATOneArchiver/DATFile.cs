﻿using QuesoStruct;
using QuesoStruct.Types.Pointers;
using QuesoStruct.Types.Primitives;

namespace DATOneArchiver
{
    [StructType]
    public partial class DATFile
    {
        [StructType]
        public partial class TablePtr : IPointerOwner
        {
            public IStructInstance RelativeOffsetBase => this;
            public long AddedOffsetFromBase => 0;

            public bool IsNullPointer(IStructReference refr) => false;
            public void SetNullPointer(IStructReference refr) { }

            [StructMember]
            [AutoInitialize]
            public UInt32Pointer<FileTable> Pointer { get; set; }
        }

        [StructType]
        public partial class EndPtr : IPointerOwner
        {
            public IStructInstance RelativeOffsetBase => (Parent as DATFile).Table.Pointer.Instance;
            public long AddedOffsetFromBase => 0;

            public bool IsNullPointer(IStructReference refr) => false;
            public void SetNullPointer(IStructReference refr) { }

            [StructMember]
            [AutoInitialize]
            public UInt32Pointer<NullTerminatingString> Pointer { get; set; }
        }

        [StructMember]
        [AutoInitialize]
        public TablePtr Table { get; set; }

        [StructMember]
        [AutoInitialize]
        public EndPtr End { get; set; }
    }
}
