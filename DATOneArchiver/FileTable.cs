using QuesoStruct;
using QuesoStruct.Types.Collections;
using QuesoStruct.Types.Pointers;

namespace DATOneArchiver
{
    [StructType]
    public partial class FileTable : IPointerOwner, ICollectionOwner<Blob>, ICollectionOwner<Entry>
    {
        public IStructInstance RelativeOffsetBase => Parent;
        public long AddedOffsetFromBase => 0;

        public bool IsNullPointer(IStructReference refr) => false;
        public void SetNullPointer(IStructReference refr) { }

        public bool TerminateOnStreamEnd => false;
        public bool IsTerminator(IStructInstance inst) => false;

        [StructMember]
        public uint Checksum { get; set; }

        [StructMember]
        public uint NumBlobs { get; set; }
        long? ICollectionOwner<Blob>.ItemCount => NumBlobs;

        [StructMember]
        public Collection<Blob> Blobs { get; set; }

        [StructMember]
        public uint NumEntries { get; set; }
        long? ICollectionOwner<Entry>.ItemCount => NumEntries;

        [StructMember]
        public Collection<Entry> Entries { get; set; }

        [StructMember]
        public uint NamesSize { get; set; }

        [StructMember]
        public Dummy Names { get; set; }
    }
}
