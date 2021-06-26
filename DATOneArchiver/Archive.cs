using QuesoStruct;
using QuesoStruct.Types.Primitives;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DATOneArchiver
{
    public class Archive
    {
        private static readonly ISerializer<DATFile> fileIO;
        private static readonly ISerializer<FileTable> tableIO;

        private static readonly ISerializer<Blob> blobIO;
        private static readonly ISerializer<Bytes> bytesIO;

        private static readonly ISerializer<Entry> entryIO;
        private static readonly ISerializer<NullTerminatingString> stringIO;


        public IDictionary<string, Stream> Files => files;
        private readonly SortedDictionary<string, Stream> files;

        private readonly Stream stream;
        private readonly Endianess endianess;

        static Archive()
        {
            fileIO = Serializers.Get<DATFile>();
            tableIO = Serializers.Get<FileTable>();

            blobIO = Serializers.Get<Blob>();
            bytesIO = Serializers.Get<Bytes>();

            entryIO = Serializers.Get<Entry>();
            stringIO = Serializers.Get<NullTerminatingString>();
        }

        public Archive(Stream stream, Endianess endianess)
        {
            files = new SortedDictionary<string, Stream>();

            this.stream = stream;
            this.endianess = endianess;
        }

        public void Read()
        {
            var context = new Context(stream, endianess, Encoding.ASCII);
            var datFile = fileIO.Read(context);
            var table = datFile.TablePointer.Instance;

            var blobs = table.Blobs;
            var entries = table.Entries;

            WalkEntries(blobs, entries, 1, entries[0].BlobIndex);
        }


        //private class Node
        //{
        //    public Dictionary<string, Node> Children { get; }
        //    public short? BlobIndex { get; }

        //    public Node()
        //    {
        //        Children = new Dictionary<string, Node>();
        //    }

        //    public Node(short blobIndex)
        //    {
        //        BlobIndex = blobIndex;
        //    }

        //    public int WalkNodes(List<Entry> entries, List<NullTerminatingString> strings, int startIdx, int prevIdx)
        //    {
        //        int idx = startIdx;

        //        var selfEntry = new Entry() { NodeIndex = (short)prevIdx };
        //        entries.Add(selfEntry);

        //        bool isFirstChild = true;
        //        foreach (var child in Children)
        //        {
        //            var str = new NullTerminatingString() { Value = child.Key };
        //            var entry = new Entry();

        //            if (!isFirstChild)
        //            {
        //            }

        //            if (child.Value.BlobIndex.HasValue)
        //            {
        //                entry.BlobIndex = child.Value.BlobIndex.Value;
        //            }
        //            else { }
        //            strings.Add(str);
        //        }
        //    }
        //}

        //public void Write()
        //{
        //    var context = new Context(stream, endianess, Encoding.ASCII);

        //    var blobs = new List<Blob>();
        //    var strings = new Dictionary<string, NullTerminatingString>();


        //}

        private int WalkEntries(IList<Blob> blobs, IList<Entry> entries, int startIdx, short terminator, string dir = "")
        {
            int idx = startIdx;
            do
            {
                if (entries[idx].BlobIndex < 1)
                {
                    var blob = blobs[-entries[idx].BlobIndex];

                    var fileName = entries[idx].NodeName.Instance.Value;
                    var filePath = Path.Combine(dir, fileName);
                    var stream = blob.Data.Instance.Stream;

                    files.Add(filePath, stream);

                    idx++;
                }
                else
                {
                    var newTerm = entries[idx].BlobIndex;
                    var subDir = entries[idx].NodeName.Instance.Value;

                    idx = WalkEntries(blobs, entries, idx + 1, newTerm, Path.Combine(dir, subDir));
                }
            } while (idx < entries.Count && (entries[idx - 1].NodeIndex > 0 && entries[idx - 1].NodeIndex < terminator - 1) || (entries[idx - 1].NodeIndex == 0 && idx < terminator));

            return idx;
        }
    }
}
