﻿using QuesoStruct;
using QuesoStruct.Types.Collections;
using QuesoStruct.Types.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace DATOneArchiver
{
    public enum ArchiveMode
    {
        ReadOnly,
        BuildNew,
    }

    public class Archive : IDisposable
    {
        private const string SIGNATURE = "IAN.S";

        private static readonly string[] BUZZ_WORDS = new string[] { "ai", "old", "tmp" };

        private static readonly ISerializer<DATFile> fileIO;
        private static readonly ISerializer<FileTable> tableIO;
        private static readonly ISerializer<Bytes> bytesIO;
        private static readonly ISerializer<RNCHeader> rncIO;

        public static string RNCProPackPath { get; set; } = "./propack/rnc_propack.exe";
        public static string VirtualFileDir { get; set; } = "./_virt";

        public static ILogger Logger { get; set; } = new ConsoleLogger();

        public IDictionary<string, Stream> Files => files;
        private readonly Dictionary<string, Stream> files;

        public uint TTGKey { get; private set; }

        private readonly string filePath;
        private readonly Stream stream;

        private readonly Endianess endianess;

        static Archive()
        {
            fileIO = Serializers.Get<DATFile>();
            tableIO = Serializers.Get<FileTable>();
            bytesIO = Serializers.Get<Bytes>();
            rncIO = Serializers.Get<RNCHeader>();
        }

        public Archive(string filePath, ArchiveMode mode, Endianess endianess)
        {
            files = new Dictionary<string, Stream>();

            this.filePath = filePath;
            if (mode == ArchiveMode.BuildNew)
                stream = File.Create(this.filePath);
            else
                stream = File.OpenRead(this.filePath);
            this.endianess = endianess;
        }


        private class Node
        {
            public string Name { get; }
            public Dictionary<string, Node> Children { get; }

            public int Depth { get; set; }
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

            public int WalkNodes(Collection<Entry> entries, Collection<NullTerminatingString> strings, int startIdx, out int firstIdx)
            {
                firstIdx = 0;
                int idx = startIdx;
                bool isFirstChild = true;

                int count = 0;
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
                            firstIdx = ++idx;
                            isFirstChild = false;
                        }
                        entry.BlobIndex = child.BlobIndex.Value;
                    }
                    else
                    {
                        var newIdx = child.WalkNodes(entries, strings, idx + 1, out int tempFirst);

                        if (!isFirstChild)
                        {
                            if (BUZZ_WORDS.Contains(child.Name.ToLowerInvariant()))
                                entry.NodeIndex = (short)idx;
                            else
                                entry.NodeIndex = (short)firstIdx;
                        }
                        else
                            isFirstChild = false;

                        if (child.Children.Values.All(c => c.BlobIndex.HasValue))
                        {
                            entry.BlobIndex = (short)newIdx;
                        }
                        else
                        {
                            entry.BlobIndex = (short)tempFirst;
                        }

                        firstIdx = idx + 1;
                        idx = newIdx;
                    }
                    count++;
                }

                return idx;
            }
        }

        public void Write(uint ttgKey, int fileAlign = 1)
        {
            Logger.WriteLine("Building archive...");

            TTGKey = ttgKey;
            var context = new Context(stream, endianess, Encoding.ASCII);

            var datFile = new DATFile()
            {
                TTGKey = TTGKey,
            };

            var table = new FileTable(datFile)
            {
                Checksum = uint.MaxValue
            };

            Logger.WriteLine("Indexing blobs...");

            var bytes = new Collection<Bytes>();
            var blobs = new Collection<Blob>(table);

            foreach (var file in Files.Values)
            {
                Blob blob;
                file.Seek(0, SeekOrigin.Begin);

                // RNC check (read header)
                var ctx = new Context(file, Endianess.Big, Encoding.ASCII);

                RNCHeader header = null;
                try
                {
                    header = rncIO.Read(ctx);
                }
                catch (EndOfStreamException) { }
                finally
                {
                    stream.Seek(0, SeekOrigin.Begin);
                }

                // RNC check (set flags if valid)
                if (header?.IsValid ?? false)
                {
                    blob = new Blob(blobs)
                    {
                        ActualSize = (uint)file.Length,
                        UncompressedSize = header.UnpackedLength,
                        CompressFlag = 1,
                    };
                }
                else
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

            Logger.WriteLine("Constructing file table...");

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

            root.WalkNodes(entries, strings, 0, out int blobIdx);
            entry.BlobIndex = (short)blobIdx;

            table.NumBlobs = (uint)blobs.Count;
            table.Blobs = blobs;

            table.NumEntries = (uint)entries.Count;
            table.Entries = entries;

            table.Names = new NameList(table);
            table.Names.Strings = strings;
            table.Names.SectionLength = (uint)(strings.Sum(s => s.Value.Length) + strings.Count + (SIGNATURE.Length + 1));
            table.Names.Signature.Value = SIGNATURE;

            Logger.WriteLine($"Writing archive to {filePath}");

            fileIO.Write(datFile, context);

            long offset = fileAlign == 1 ? 8 : fileAlign;
            foreach (var data in bytes)
            {
                Logger.Write(".");

                data.Offset = offset;
                bytesIO.Write(data, context);

                var length = data.Stream.Length;
                if ((offset + length) % fileAlign == 0)
                    offset += length;
                else
                    offset = ((offset + length) / fileAlign + 1) * fileAlign;
            }

            tableIO.Write(table, context);

            datFile.TablePointer.Instance = table;

            foreach (var str in strings)
            {
                (str.Parent as Entry).NodeName.Instance = str;
            }

            foreach (var data in bytes)
            {
                (data.Parent as Blob).Data.Instance = data;
            }

            context.RewriteUnresolvedReferences();

            Logger.WriteLine("");
            Logger.WriteLine("Build/write complete!!!");
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
                    var ctx = new Context(stream, Endianess.Big, Encoding.ASCII);

                    RNCHeader header = null;
                    try
                    {
                        header = rncIO.Read(ctx);
                    }
                    catch (EndOfStreamException) { }
                    finally
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                    }

                    if (header?.IsValid ?? false)
                    {
                        Logger.WriteLine($"Decompressing {filePath}...");

                        var virtFile = Path.Combine(VirtualFileDir, filePath);
                        var virtDir = Path.GetDirectoryName(virtFile);

                        Directory.CreateDirectory(virtDir);

                        var startInfo = new ProcessStartInfo(RNCProPackPath, $"u {this.filePath} {virtFile} -i 0x{blob.Data.Instance.Offset:X8}")
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = false,
                        };

                        Process.Start(startInfo).WaitForExit();

                        files.Add(filePath, File.OpenRead(virtFile));
                    }
                    else
                    {
                        files.Add(filePath, stream);
                    }

                    idx++;
                }
                else
                {
                    var newTerm = entries[idx].BlobIndex;
                    var subDir = entries[idx].NodeName.Instance.Value;

                    idx = WalkEntries(blobs, entries, idx + 1, newTerm, Path.Combine(dir, subDir));
                }
            } while (idx < entries.Count && (entries[idx - 1].NodeIndex > 0 && entries[idx - 1].NodeIndex < terminator - 1) || (entries[idx - 1].NodeIndex == 0 && idx - 1 < terminator));

            return idx;
        }

        public void Read()
        {
            Logger.WriteLine("Reading archive...");

            var context = new Context(stream, endianess, Encoding.ASCII);

            var datFile = fileIO.Read(context);
            TTGKey = datFile.TTGKey;

            var table = datFile.TablePointer.Instance;

            var blobs = table.Blobs;
            var entries = table.Entries;

            WalkEntries(blobs, entries, 1, entries[0].BlobIndex);

            Logger.WriteLine("Read complete!!!");
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

                    stream.Dispose();
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
