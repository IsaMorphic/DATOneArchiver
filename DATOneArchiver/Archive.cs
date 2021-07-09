using QuesoStruct;
using QuesoStruct.Types.Collections;
using QuesoStruct.Types.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DATOneArchiver
{
    public class Archive : IDisposable
    {
        private static readonly ISerializer<DATFile> fileIO;
        private static readonly ISerializer<FileTable> tableIO;
        private static readonly ISerializer<Bytes> bytesIO;
        private static readonly ISerializer<Dummy> dummyIO;
        private static readonly ISerializer<RNCHeader> rncIO;

        public IDictionary<string, Stream> Files => files;
        private readonly Dictionary<string, Stream> files;

        private readonly Stream stream;
        private readonly Endianess endianess;

        static Archive()
        {
            fileIO = Serializers.Get<DATFile>();
            tableIO = Serializers.Get<FileTable>();
            bytesIO = Serializers.Get<Bytes>();
            dummyIO = Serializers.Get<Dummy>();
            rncIO = Serializers.Get<RNCHeader>();
        }

        public Archive(Stream stream, Endianess endianess)
        {
            files = new Dictionary<string, Stream>();

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
                    for (int i = 0; i < tokens.Length; i++)
                    {
                        var token = tokens[i];

                        if (current.Children.ContainsKey(token))
                            current = current.Children[token];
                        else
                        {
                            Node node;
                            if (i == tokens.Length - 1)
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
                        else
                        {
                            secIdx = idx + 1;
                            isFirstChild = false;
                            idx++;
                        }
                        entry.BlobIndex = child.BlobIndex.Value;
                    }
                    else
                    {
                        var newIdx = child.WalkNodes(entries, strings, idx + 1, out int temp);

                        if (isFirstChild && child.Children.Values.All(c => !c.BlobIndex.HasValue))
                        {
                            entry.NodeIndex = (short)idx;
                            entry.BlobIndex = (short)temp;

                            isFirstChild = false;
                            idx = newIdx;
                        }
                        else
                        {
                            if (isFirstChild)
                                isFirstChild = false;

                            entry.NodeIndex = (short)secIdx;
                            entry.BlobIndex = (short)newIdx;

                            idx = newIdx;
                        }

                        secIdx = temp - 1;
                    }
                }

                return idx;
            }
        }

        public void Write(int? fileAlign = null)
        {
            var context = new Context(stream, endianess, Encoding.ASCII);

            var datFile = new DATFile();
            var table = new FileTable(datFile)
            {
                Checksum = uint.MaxValue
            };

            var bytes = new Collection<Bytes>();
            var blobs = new Collection<Blob>(table);

            foreach (var file in Files.Values)
            {
                Blob blob;

                try
                {
                    var ctx = new Context(file, Endianess.Big, Encoding.ASCII);
                    var header = rncIO.Read(ctx);

                    blob = new Blob(blobs)
                    {
                        ActualSize = (uint)file.Length,
                        UncompressedSize = header.UnpackedLength,
                        CompressFlag = 1,
                    };
                }
                catch (InvalidOperationException)
                {
                    blob = new Blob(blobs)
                    {
                        ActualSize = (uint)file.Length,
                        UncompressedSize = (uint)file.Length,
                    };
                }

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

            entry.BlobIndex = (short)root.WalkNodes(entries, strings, 0, out int _);

            table.NumBlobs = (uint)blobs.Count;
            table.Blobs = blobs;

            table.NumEntries = (uint)entries.Count;
            table.Entries = entries;

            table.Names = new NameList(table);
            table.Names.Strings = strings;

            var footer = new Dummy(table.Names);

            fileIO.Write(datFile, context);

            long offset = fileAlign ?? 8;
            foreach (var data in bytes)
            {
                data.Offset = offset;
                bytesIO.Write(data, context);

                offset += fileAlign.HasValue ? (data.Stream.Length / fileAlign.Value + 1) * fileAlign.Value : data.Stream.Length;
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

        private void PrintEntry(Entry entry, int idx, int level)
        {
            var indent = new char[level * 3];
            Array.Fill(indent, ' ');
            Console.WriteLine($"{new string(indent)}{idx:X4};{entry.BlobIndex:X4};{entry.NodeIndex:X4};\"{entry.NodeName.Instance.Value}\"");
        }

        private int WalkEntries(IList<Blob> blobs, IList<Entry> entries, int startIdx, short terminator, string dir = "", int level = 0, bool debug = true)
        {
            int idx = startIdx;
            do
            {
                PrintEntry(entries[idx], idx, level);

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

                    idx = WalkEntries(blobs, entries, idx + 1, newTerm, Path.Combine(dir, subDir), level + 1, debug);
                }
            } while (idx < entries.Count && (entries[idx - 1].NodeIndex > 0 && entries[idx - 1].NodeIndex < terminator - 1) || (entries[idx - 1].NodeIndex == 0 && idx - 1 < terminator));

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
