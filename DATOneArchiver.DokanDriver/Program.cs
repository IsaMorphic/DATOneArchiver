using DokanNet;
using System;

namespace DATOneArchiver.DokanDriver
{
    class Program
    {
        static void Main(string[] args)
        {
            var archive = new Archive(args[0], ArchiveMode.ReadWrite, Game.LSW1, QuesoStruct.Endianess.Little, 2048);
            archive.Read();

            Dokan.Init();
            new ArchiveOperations(archive).Mount("H", DokanOptions.RemovableDrive, true);
            Dokan.Shutdown();
        }
    }
}
