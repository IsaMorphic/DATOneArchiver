using QuesoStruct;
using System;
using System.Collections.Generic;
using System.IO;

namespace DATOneArchiver
{
    class Program
    {
        private static readonly Dictionary<string, Endianess> endianesses =
            new Dictionary<string, Endianess>
            {
                { "LITTLE", Endianess.Little },
                { "BIG", Endianess.Big },
            };

        static void Main(string[] args)
        {
            var endianess = endianesses[args[1].ToUpperInvariant()];

            using var oldArchive = new Archive(args[0], ArchiveMode.ReadOnly, endianess);
            oldArchive.Read();

            //foreach (var file in oldArchive.Files)
            //{
            //    Archive.Logger.WriteLine($"Extracting {file.Key}...");

            //    var path = Path.Combine(".", "extracted", file.Key);

            //    var dir = Path.GetDirectoryName(path);
            //    Directory.CreateDirectory(dir);

            //    using var io = File.Create(path);
            //    file.Value.CopyTo(io);
            //}

            using var newArchive = new Archive("new.dat", ArchiveMode.BuildNew, endianess);

            foreach (var path in oldArchive.Files.Keys)
            {
                newArchive.Files.Add(path, File.OpenRead(Path.Combine("extracted", path)));
            }

            //foreach (var path in Directory.EnumerateFiles("./extracted", "*.*", SearchOption.AllDirectories))
            //{
            //    var file = Path.GetRelativePath("./extracted", path);
            //    newArchive.Files.Add(file, File.OpenRead(path));
            //    Console.WriteLine(file);
            //}

            newArchive.Write(oldArchive.TTGKey, 2048);
        }
    }
}
