using CommandLine;
using QuesoStruct;
using System.Collections.Generic;

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

        private class ExtractOptions
        {
            [Option('i', "input-archive", Required = true, HelpText = "The DAT archive to extract files from.")]
            public string ArchivePath { get; set; }

            [Option('o', "output-dir", Required = true, HelpText = "The directory in which the extraction output is to be placed.")]
            public string ExtractPath { get; set; }

            [Option('e', "endian", Default = "little", HelpText = "The endianess of the input file.")]
            public string Endianess { get; set; }

            [Option('c', "decompress", HelpText = "Use of this flag enables automatic decompression of RNC encoded data from the input file.\nIt is otherwise disabled.")]
            public bool Decompress { get; set; }
        }

        private static void Main(string[] args)
        {
            Parser.Default.ParseArguments<ExtractOptions>(args)
                .WithParsed(RunExtract);
        }

        private static void RunExtract(ExtractOptions options)
        {
            var endianess = endianesses[options.Endianess.ToUpperInvariant()];

            using var archive = new Archive(options.ArchivePath, ArchiveMode.ReadOnly, endianess);
            archive.Read();

            archive.Extract(options.ExtractPath, options.Decompress);
        }
    }
}
