/* Copyright (C) 2022 Chosen Few Software
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

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
    public enum ArchiveMode
    {
        ReadWrite,
        BuildNew,
    }

    public enum Game
    {
        LSW1,
        LSW2,
        TCS,
    }

    public partial class Archive : IDisposable
    {
        private const string SIGNATURE = "IAN.S";

        private static readonly string[] BUZZ_WORDS = new string[] { "ai", "old", "tmp" };

        private static readonly ISerializer<DATFile> fileIO;
        private static readonly ISerializer<FileTable> tableIO;
        private static readonly ISerializer<Bytes> bytesIO;
        private static readonly ISerializer<RNCHeader> rncIO;

        public Node RootDirectory { get; set; }

        public static ILogger Logger { get; set; } = new ConsoleLogger();

        private readonly string filePath;
        private readonly Stream stream;

        private readonly Endianess endianess;
        private readonly Game game;

        static Archive()
        {
            fileIO = Serializers.Get<DATFile>();
            tableIO = Serializers.Get<FileTable>();
            bytesIO = Serializers.Get<Bytes>();
            rncIO = Serializers.Get<RNCHeader>();
        }

        public Archive(string filePath, ArchiveMode mode, Game game, Endianess endianess)
        {
            this.filePath = filePath;
            if (mode == ArchiveMode.BuildNew)
                stream = File.Create(this.filePath);
            else
                stream = File.Open(this.filePath, FileMode.Open);

            this.endianess = endianess;
            this.game = game;

            RootDirectory = new Node("");
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

        public void Rebuild(bool update = false, int fileAlign = 1)
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

            var entry = new Entry(entries);
            entries.Add(entry);

            var name = new NullTerminatingString(entry) { Value = RootDirectory.Name };
            strings.Add(name);

            int blobIdx = 0;
            var nodes = new SortedList<int, Node>();
            RootDirectory.WalkNodes(game, nodes, entries, strings, 0, ref blobIdx, out int outIdx);

            entry.BlobIndex = (short)outIdx;

            table.NumEntries = (uint)entries.Count;
            table.Entries = entries;

            table.Names = new NameList(table);
            table.Names.Strings = strings;
            table.Names.Signature.Value = SIGNATURE;

            Logger.WriteLine("Indexing blobs...");

            var bytes = new Collection<Bytes>();
            var blobs = new Collection<Blob>(table);

            foreach (var file in nodes.Values.Select(n => n.Stream))
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

                var inst = new Bytes(blob) { Stream = (file is SubStream == update) ? null : file };

                bytes.Add(inst);
                blobs.Add(blob);
            }

            table.NumBlobs = (uint)blobs.Count;
            table.Blobs = blobs;

            if (update == false)
            {
                Logger.Write($"Writing archive to {filePath}");

                fileIO.Write(datFile, context);

                long offset = fileAlign == 1 ? 8 : fileAlign;
                foreach (var x in nodes.Values.Zip(bytes, (n, b) => new { node = n, data = b }))
                {
                    Logger.Write(".");

                    x.data.Offset = offset;
                    bytesIO.Write(x.data, context);

                    x.node.Stream = x.data.Stream;

                    var length = x.data.Stream.Length;
                    if ((offset + length) % fileAlign == 0)
                        offset += length;
                    else
                        offset = ((offset + length) / fileAlign + 1) * fileAlign;
                }
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

            Logger.WriteLine("\nRebuild complete.");
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

                    var fileNode = RootDirectory[filePath];

                    fileNode.Stream = stream;
                    fileNode.BlobIndex = entries[idx].BlobIndex;

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

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
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
