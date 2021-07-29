using QuesoStruct;
using QuesoStruct.Types.Collections;
using QuesoStruct.Types.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DATOneArchiver
{
    public enum ArchiveMode
    {
        ReadOnly,
        BuildNew,
    }

    public enum Game
    {
        LSW1,
        LSW2,
        TCS,
    }

    public class Archive : IDisposable
    {
        private const string SIGNATURE = "IAN.S";

        private static readonly string[] BUZZ_WORDS = new string[] { "ai", "old", "tmp" };

        private static readonly ISerializer<DATFile> fileIO;
        private static readonly ISerializer<FileTable> tableIO;
        private static readonly ISerializer<Bytes> bytesIO;
        private static readonly ISerializer<RNCHeader> rncIO;

        public static string RNCProPackPath { get; set; } = ".\\propack\\rnc_propack.exe";
        public static string VirtualFileDir { get; set; } = ".\\_virt";

        public static ILogger Logger { get; set; } = new ConsoleLogger();

        public IDictionary<string, Stream> Files => files;
        private readonly Dictionary<string, Stream> files;

        private readonly string filePath;
        private readonly Stream stream;

        private readonly Game game;
        private readonly Endianess endianess;

        static Archive()
        {
            fileIO = Serializers.Get<DATFile>();
            tableIO = Serializers.Get<FileTable>();
            bytesIO = Serializers.Get<Bytes>();
            rncIO = Serializers.Get<RNCHeader>();
        }

        public Archive(string filePath, ArchiveMode mode, Game game, Endianess endianess)
        {
            files = new Dictionary<string, Stream>();

            this.filePath = filePath;
            if (mode == ArchiveMode.BuildNew)
                stream = File.Create(this.filePath);
            else
                stream = File.OpenRead(this.filePath);

            this.game = game;
            this.endianess = endianess;
        }

        private class Node : IComparable<Node>
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
                        .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

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

            public void PrintNodes(int level = 1, bool isLast = true, HashSet<int> lastLevels = null)
            {
                if (lastLevels == null)
                    lastLevels = new HashSet<int>();
                else
                    lastLevels = new HashSet<int>(lastLevels);

                var indent = new char[level * 3];
                Array.Fill(indent, ' ');

                for (int i = 1; i < level; i++)
                {
                    if (!lastLevels.Contains(i))
                        indent[i * 3] = '\u2502';
                }

                int idx = (level - 1) * 3;
                indent[idx + 0] = isLast ? '\u2514' : '\u251C';
                indent[idx + 1] = '\u2500';
                indent[idx + 2] = '\u2500';

                Logger.WriteLine(new string(indent) + Name);

                if (Children != null)
                {
                    if (isLast) lastLevels.Add(level - 1);

                    int count = 0;
                    foreach (var child in Children.Values)
                    {
                        child.PrintNodes(level + 1, ++count == Children.Count, lastLevels);
                    }
                }
            }

            public int WalkNodes(Game game, Collection<Entry> entries, Collection<NullTerminatingString> strings, int startIdx, ref int blobIdx, out int firstIdx)
            {
                firstIdx = 0;
                int idx = startIdx;
                bool isFirstChild = true;

                foreach (var child in Children.Values.OrderBy(c => c))
                {
                    var entry = new Entry(entries);
                    entries.Add(entry);

                    var str = new NullTerminatingString(entry) { Value = child.Name };
                    strings.Add(str);

                    if (child.Children == null)
                    {
                        child.BlobIndex = entry.BlobIndex = (short)blobIdx--;

                        if (!isFirstChild)
                            entry.NodeIndex = (short)idx++;
                        else
                        {
                            firstIdx = ++idx;
                            isFirstChild = false;
                        }
                    }
                    else
                    {
                        var newIdx = child.WalkNodes(game, entries, strings, idx + 1, ref blobIdx, out int tempFirst);

                        if (child.Children.Values.All(c => c.Children == null))
                            entry.BlobIndex = (short)newIdx;
                        else
                            entry.BlobIndex = (short)tempFirst;

                        if (!isFirstChild)
                        {
                            if ((game == Game.LSW1 && BUZZ_WORDS.Contains(child.Name.ToLowerInvariant())) ||
                                (game == Game.LSW2 && Children.Values.OrderBy(c => c).First(c => c.Children != null) == child))
                                entry.NodeIndex = (short)idx;
                            else
                                entry.NodeIndex = (short)firstIdx;
                        }
                        else isFirstChild = false;

                        firstIdx = idx + 1;
                        idx = newIdx;
                    }
                }

                return idx;
            }

            public int CompareTo(Node other)
            {
                if ((Children == null && other.Children == null) ||
                    (Children != null && other.Children != null) ||
                    (BUZZ_WORDS.Contains(Name) && BUZZ_WORDS.Contains(other.Name)))
                {
                    var filePattern = new Regex("^(.*?)\\.(.*)");

                    var myMatch = filePattern.Match(Name);

                    string myName, myExt, otherName, otherExt;

                    if (myMatch.Success)
                    {
                        myName = myMatch.Groups[1].Value;
                        myExt = myMatch.Groups[2].Value;
                    }
                    else
                    {
                        myName = Name;
                        myExt = "";
                    }

                    var otherMatch = filePattern.Match(other.Name);

                    if (otherMatch.Success)
                    {
                        otherName = otherMatch.Groups[1].Value;
                        otherExt = otherMatch.Groups[2].Value;
                    }
                    else
                    {
                        otherName = other.Name;
                        otherExt = "";
                    }

                    if (BUZZ_WORDS.Any(w => myName.ToLowerInvariant().EndsWith("-" + w)))
                    {
                        return -1;
                    }
                    else if (BUZZ_WORDS.Any(w => otherName.ToLowerInvariant().EndsWith("-" + w)))
                    {
                        return 1;
                    }
                    else
                    {
                        var nameResult = myName.Replace('_', 'z')
                            .CompareTo(otherName.Replace('_', 'z'));

                        var extResult = myExt.Replace('_', 'z')
                            .CompareTo(otherExt.Replace('_', 'z'));

                        return nameResult != 0 ? nameResult : extResult;
                    }
                }
                else if (Children == null || BUZZ_WORDS.Contains(other.Name))
                {
                    return -1;
                }
                else if (other.Children == null || BUZZ_WORDS.Contains(Name))
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        }

        private bool IsRNCStream(Stream stream, out uint unpackedLen)
        {
            stream.Seek(0, SeekOrigin.Begin);

            var ctx = new Context(stream, Endianess.Big, Encoding.ASCII);
            RNCHeader header = null;

            try
            {
                header = rncIO.Read(ctx);
                unpackedLen = header.UnpackedLength;
            }
            catch (EndOfStreamException)
            {
                unpackedLen = 0;
            }

            return header?.IsValid ?? false;
        }

        public void Write(int fileAlign = 1)
        {
            Logger.WriteLine("Building archive...");

            var context = new Context(stream, endianess, Encoding.ASCII);
            var datFile = new DATFile();

            var table = new FileTable(datFile)
            {
                Checksum = uint.MaxValue
            };

            Logger.WriteLine("Constructing file table...");

            var entries = new Collection<Entry>(table);
            var strings = new Collection<NullTerminatingString>(table);

            var root = new Node("");

            var nodes = new List<Node>();
            foreach (var file in Files.Keys)
            {
                nodes.Add(root[file]);
            }

            var entry = new Entry(entries);
            entries.Add(entry);

            var name = new NullTerminatingString(entry) { Value = root.Name };
            strings.Add(name);

            int blobIdx = 0;
            root.WalkNodes(game, entries, strings, 0, ref blobIdx, out int outIdx);
            entry.BlobIndex = (short)outIdx;

            table.NumEntries = (uint)entries.Count;
            table.Entries = entries;

            table.Names = new NameList(table);
            table.Names.Strings = strings;
            table.Names.Signature.Value = SIGNATURE;

            Logger.WriteLine("Indexing blobs...");

            var streams = new SortedList<int, Stream>(nodes
                .Zip(Files.Values, (n, s) => new { n, s })
                .ToDictionary(x => -x.n.BlobIndex.Value, x => x.s)
                );

            var bytes = new Collection<Bytes>();
            var blobs = new Collection<Blob>(table);

            foreach (var file in streams.Values)
            {
                Blob blob;

                // RNC check (set flags if valid)
                if (IsRNCStream(file, out uint unpackedLen))
                {
                    blob = new Blob(blobs)
                    {
                        ActualSize = (uint)file.Length,
                        UncompressedSize = unpackedLen,
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

            table.NumBlobs = (uint)blobs.Count;
            table.Blobs = blobs;

            Logger.Write($"Writing archive to {filePath}");

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

            datFile.Table.Pointer.Instance = table;

            datFile.End.Pointer.Instance = table.Names.Signature;
            table.Names.SectionLength.Instance = table.Names.Signature;

            foreach (var str in strings)
            {
                (str.Parent as Entry).NodeName.Instance = str;
            }

            foreach (var data in bytes)
            {
                (data.Parent as Blob).Data.Instance = data;
            }

            context.RewriteUnresolvedReferences();

            Logger.WriteLine("\nBuild/write complete!!!");
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
            } while (idx < entries.Count && (entries[idx - 1].NodeIndex > 0 && entries[idx - 1].NodeIndex < terminator - 1) || (entries[idx - 1].NodeIndex == 0 && idx - 1 < terminator));

            return idx;
        }

        public void Read()
        {
            Logger.WriteLine("Reading archive file...");

            var context = new Context(stream, endianess, Encoding.ASCII);

            var datFile = fileIO.Read(context);

            var table = datFile.Table.Pointer.Instance;

            var blobs = table.Blobs;
            var entries = table.Entries;

            WalkEntries(blobs, entries, 1, entries[0].BlobIndex);

            Logger.WriteLine("Read complete!!!");
        }

        public void List(string path = "")
        {
            Logger.WriteLine($"Listing contents of archive file \"{filePath}\"...");

            var root = new Node("<root>");
            foreach (var file in Files.Keys)
            {
                var _ = root[file];
            }

            root[path].PrintNodes();
        }

        public void Extract(string extractDir, bool decompress = true)
        {
            Logger.WriteLine($"Extracting archive file \"{filePath}\" to \"{extractDir}\"...");

            foreach (var file in files)
            {
                var filePath = Path.Combine(extractDir, file.Key);

                var dir = Path.GetDirectoryName(filePath);
                Directory.CreateDirectory(dir);

                if (decompress && IsRNCStream(file.Value, out uint _))
                {
                    Logger.WriteLine($"Decompressing {file.Key}...");

                    var virtFile = Path.Combine(VirtualFileDir, filePath);
                    var virtDir = Path.GetDirectoryName(virtFile);

                    Directory.CreateDirectory(virtDir);

                    using (var io = File.Create(virtFile))
                    {
                        file.Value.Seek(0, SeekOrigin.Begin);
                        file.Value.CopyTo(io);
                    }

                    var startInfo = new ProcessStartInfo(RNCProPackPath, $"u {virtFile} {filePath}")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = false,
                    };

                    Process.Start(startInfo).WaitForExit();
                }
                else
                {
                    Logger.WriteLine($"Extracting {file.Key}...");

                    using var io = File.Create(filePath);

                    file.Value.Seek(0, SeekOrigin.Begin);
                    file.Value.CopyTo(io);
                }
            }

            if (decompress)
            {
                Logger.WriteLine("Cleaning up...");
                if (Directory.Exists(VirtualFileDir))
                {
                    Directory.Delete(VirtualFileDir, true);
                }
            }

            Logger.WriteLine("Extraction complete!!!");
        }

        public void Build(string dataDir, int fileAlign = 1)
        {
            Logger.WriteLine($"Building archive file \"{filePath}\" from files in \"{dataDir}\"...");

            foreach (var fullPath in Directory.EnumerateFiles(dataDir, "*.*", SearchOption.AllDirectories))
            {
                var filePath = Path.GetRelativePath(dataDir, fullPath);
                files.Add(filePath, File.OpenRead(fullPath));
            }

            Write(fileAlign);
        }

        public void Patch(string patchDir, string outputPath = null)
        {
            Logger.WriteLine($"Patching archive file \"{filePath}\" with files in \"{patchDir}\"...");

            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = null;
                Directory.CreateDirectory(VirtualFileDir);
            }

            using (var newArchive = new Archive(outputPath ?? Path.Combine(VirtualFileDir, "_.DAT"), ArchiveMode.BuildNew, game, endianess))
            {
                foreach (var fullPath in Directory.EnumerateFiles(patchDir, "*.*", SearchOption.AllDirectories))
                {
                    var filePath = Path.GetRelativePath(patchDir, fullPath);
                    newArchive.Files.Add(filePath, File.OpenRead(fullPath));
                }

                foreach (var file in files)
                {
                    if (!newArchive.Files.ContainsKey(file.Key))
                    {
                        newArchive.Files.Add(file.Key, file.Value);
                    }
                }

                int fileAlign = (int)(files.Values.First() as SubStream).AbsoluteOffset;
                newArchive.Write(fileAlign == 8 ? 1 : fileAlign);
            }

            if (outputPath == null)
            {
                using var self = this;

                Logger.WriteLine("Cleaning up...");

                stream.Dispose();
                File.Move(outputPath, filePath, true);

                Directory.Delete(VirtualFileDir, true);
            }
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
