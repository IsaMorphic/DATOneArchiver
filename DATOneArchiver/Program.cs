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
                var dir = Path.GetDirectoryName(file.Key);
                Directory.CreateDirectory(dir);

                using var io = File.Create(file.Key);
                file.Value.CopyTo(io);

                Console.WriteLine($"{file.Key}");
            }
        }
    }
}
