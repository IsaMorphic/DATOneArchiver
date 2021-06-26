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
            using var stream = File.OpenRead(args[0]);

            var archive = new Archive(stream, endianesses[args[1].ToUpperInvariant()]);
            archive.Read();

            foreach (var file in archive.Files) 
            {
                var path = Path.Combine(".", "extracted", file.Key);

                var dir = Path.GetDirectoryName(path);
                Directory.CreateDirectory(dir);

                using var io = File.Create(path);
                file.Value.CopyTo(io);

                Console.WriteLine($"{file.Key}");
            }
        }
    }
}
