using QuesoStruct;
using System;
using System.IO;

namespace DATOneArchiver
{
    class Program
    {
        static void Main(string[] args)
        {
            using var stream = File.OpenRead(args[0]);

            var archive = new Archive(stream, Endianess.Little);
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
