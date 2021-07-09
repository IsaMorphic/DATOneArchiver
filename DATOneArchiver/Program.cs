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
            using var stream = File.OpenRead(args[0]);

            using var oldArchive = new Archive(stream, endianess);
            oldArchive.Read();

            //foreach (var file in oldArchive.Files)
            //{
            //    var path = Path.Combine(".", "extracted", file.Key);

            //    var dir = Path.GetDirectoryName(path);
            //    Directory.CreateDirectory(dir);

            //    using var io = File.Create(path);
            //    file.Value.CopyTo(io);

            //    Console.WriteLine($"{file.Key}");
            //}

            using var newArchive = new Archive(File.Create("new.dat"), endianess);

            foreach (var path in oldArchive.Files.Keys)
            {
                newArchive.Files.Add(path, File.OpenRead(Path.Combine("extracted", path)));
                Console.WriteLine(path);
            }

            //foreach (var path in Directory.EnumerateFiles("./extracted", "*.*", SearchOption.AllDirectories))
            //{
            //    var file = Path.GetRelativePath("./extracted", path);
            //    newArchive.Files.Add(file, File.OpenRead(path));
            //    Console.WriteLine(file);
            //}

            newArchive.Write(2048);
        }
    }
}
