using QuesoStruct;
using QuesoStruct.Types.Collections;
using QuesoStruct.Types.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DATOneArchiver
{
    public class Archive : IDisposable
    {
        private static readonly ISerializer<DATFile> fileIO;
        private static readonly ISerializer<FileTable> tableIO;
        private static readonly ISerializer<Bytes> bytesIO;
        private static readonly ISerializer<Dummy> dummyIO;

        public IDictionary<string, Stream> Files => files;
        private readonly SortedDictionary<string, Stream> files;

        private readonly Stream stream;
        private readonly Endianess endianess;

        static Archive()
        {
            fileIO = Serializers.Get<DATFile>();
            tableIO = Serializers.Get<FileTable>();
            bytesIO = Serializers.Get<Bytes>();
            dummyIO = Serializers.Get<Dummy>();
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


        private class Node
        {
            public string Name { get; }
            public Dictionary<string, Node> Children { get; }

            public short? BlobIndex { get; set; }

            public Node this[string path]
            {
                get
                {
                    var tokens = Path.TrimEndingDirectorySeparator(path)
                        .Split(Path.DirectorySeparatorChar);

                    var current = this;
                    foreach (var token in tokens)
                    {
                        if (current.Children.ContainsKey(token))
                            current = current.Children[token];
                        else
                        {
                            Node node;
                            if (Path.HasExtension(token))
                                node = new Node(token, null);
                            else
                                node = new Node(token);

                            current.Children.Add(token, node);
                            current = node;
                        }
                    }

                    return current;
                }
            }

            public Node()
            {
                Children = new Dictionary<string, Node>();
            }

            public Node(string name)
            {
                Name = name;
                Children = new Dictionary<string, Node>();
            }

            public Node(string name, short? blobIndex)
            {
                Name = name;
                BlobIndex = blobIndex;
            }

            public int WalkNodes(Collection<Entry> entries, Collection<NullTerminatingString> strings, int startIdx, out int secIdx)
            {
                secIdx = 0;
                int prevIdx = 0;
                int idx = startIdx;
                bool isFirstChild = true;

                foreach (var child in Children.Values)
                {
                    var entry = new Entry(entries);
                    entries.Add(entry);

                    var str = new NullTerminatingString(entry) { Value = child.Name };
                    strings.Add(str);

                    if (child.BlobIndex.HasValue)
                    {
                        if (!isFirstChild)
                            entry.NodeIndex = (short)idx++;
                        else idx++;
                        entry.BlobIndex = child.BlobIndex.Value;
                    }
                    else
                    {
                        idx = child.WalkNodes(entries, strings, idx, out int temp);

                        if (!isFirstChild)
                            entry.NodeIndex = (short)prevIdx;
                        prevIdx = temp;

                        entry.BlobIndex = (short)idx++;
                    }

                    if (isFirstChild)
                    {
                        secIdx = idx - 1;
                        isFirstChild = false;
                    }
                }

                return idx;
            }
        }

        public void Write(int fileAlign = 1)
        {
            var context = new Context(stream, endianess, Encoding.ASCII);

            var datFile = new DATFile();
            var table = new FileTable(datFile)
            {
                Checksum = uint.MaxValue
            };
            var footer = new Dummy();

            var bytes = new Collection<Bytes>();
            var blobs = new Collection<Blob>(table);

            foreach (var file in Files.Values)
            {
                var blob = new Blob(blobs)
                {
                    ActualSize = (uint)file.Length,
                    UncompressedSize = (uint)file.Length,
                };
                var inst = new Bytes(blob) { Stream = file };

                bytes.Add(inst);
                blobs.Add(blob);
            }

            var entries = new Collection<Entry>(table);
            var strings = new Collection<NullTerminatingString>(table);

            var root = new Node("");

            short idx = 0;
            foreach (var file in Files.Keys)
            {
                root[file].BlobIndex = idx--;
            }

            var entry = new Entry(entries);
            entries.Add(entry);

            var name = new NullTerminatingString(entry) { Value = root.Name };
            strings.Add(name);

            entry.BlobIndex = (short)(root.WalkNodes(entries, strings, 2, out int _) + 1);

            table.NumBlobs = (uint)blobs.Count;
            table.Blobs = blobs;

            table.NumEntries = (uint)entries.Count;
            table.Entries = entries;

            table.Names = new NameList(table);
            table.Names.Strings = strings;

            fileIO.Write(datFile, context);

            long offset = fileAlign;
            foreach (var data in bytes) 
            {
                data.Offset = offset;
                bytesIO.Write(data, context);

                offset += fileAlign > 1 ? (data.Stream.Length / fileAlign + 1) * fileAlign : data.Stream.Length;
            }

            tableIO.Write(table, context);
            dummyIO.Write(footer, context);

            datFile.TablePointer.Instance = table;
            table.Names.EndPtr.Instance = footer;

            foreach (var str in strings)
            {
                (str.Parent as Entry).NodeName.Instance = str;
            }

            foreach (var data in bytes)
            {
                (data.Parent as Blob).Data.Instance = data;
            }

            context.RewriteUnresolvedReferences();
        }

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

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var file in Files.Values)
                    {
                        file.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
