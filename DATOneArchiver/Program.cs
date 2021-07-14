using CommandLine;
using QuesoStruct;
using System;
using System.Collections.Generic;
using System.Text;

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

        private class BaseOptions
        {
            [Option('i', "input-archive", Required = true, HelpText = "The DAT archive to extract files from.")]
            public string ArchivePath { get; set; }

            [Option('e', "endian", Default = "little", HelpText = "The endianess of the input file.")]
            public string Endianess { get; set; }
        }

        [Verb("list", HelpText = "List the contents of existing DAT archives without extracting them.")]
        private class ListOptions : BaseOptions
        {
        }

        [Verb("extract", HelpText = "Extract the contents of existing DAT archives with optional decompression.")]
        private class ExtractOptions : BaseOptions
        {
            [Option('o', "output-dir", Required = true, HelpText = "The directory in which the extraction output is to be placed.")]
            public string ExtractPath { get; set; }

            [Option('c', "decompress", HelpText = "Use of this flag enables automatic decompression of RNC encoded data from the input file.\nIt is otherwise disabled.")]
            public bool Decompress { get; set; }
        }

        [Verb("modify", HelpText = "Modify existing DAT archive files by adding new files and/or replacing current ones.")]
        private class ModifyOptions : BaseOptions
        {
            [Option('p', "patch-dir", Required = true, HelpText = "The directory containing the files to be patched in.\nTo replace files, you must maintain the same relative directory structure as the original DAT.")]
            public string PatchPath { get; set; }
        }

        [Verb("build", HelpText = "Build a brand new TT Games DAT archive file.")]
        private class BuildOptions : BaseOptions
        {
            [Option('d', "data-dir", Required = true, HelpText = "The directory containing the files to be packed.")]
            public string DataPath { get; set; }

            [Option('k', "ttg-key", Required = true, HelpText = "The TTG Key for the game this archive is intended to be used with.")]
            public string TTGKey { get; set; }

            [Option('a', "align", Default = 1, HelpText = "The alignment to enforce for embedded file data. Setting to 1 disables any alignment control.")]
            public int FileAlign { get; set; }
        }

        private static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.Unicode;
            Parser.Default.ParseArguments<ListOptions, ExtractOptions, BuildOptions>(args)
                .WithParsed<ListOptions>(RunList)
                .WithParsed<ExtractOptions>(RunExtract)
                .WithParsed<BuildOptions>(RunBuild);
        }

        private static void RunList(ListOptions options)
        {
            var endianess = endianesses[options.Endianess.ToUpperInvariant()];

            using var archive = new Archive(options.ArchivePath, ArchiveMode.ReadOnly, endianess);
            archive.Read();

            archive.List();
        }

        private static void RunExtract(ExtractOptions options)
        {
            var endianess = endianesses[options.Endianess.ToUpperInvariant()];

            using var archive = new Archive(options.ArchivePath, ArchiveMode.ReadOnly, endianess);
            archive.Read();

            archive.Extract(options.ExtractPath, options.Decompress);
        }

        private static void RunBuild(BuildOptions options)
        {
            var endianess = endianesses[options.Endianess.ToUpperInvariant()];

            var ttgKeyValid = uint.TryParse(options.TTGKey, out uint ttgKey);
            if (!ttgKeyValid)
            {
                Console.WriteLine("Invalid TTG Key format! Please enter in hexadecimal!");
                return;
            }

            using var archive = new Archive(options.ArchivePath, ArchiveMode.BuildNew, endianess);
            archive.Build(options.DataPath, ttgKey, options.FileAlign);
        }
    }
}
