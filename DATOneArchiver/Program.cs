using CommandLine;
using GlobExpressions;
using QuesoStruct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DATOneArchiver
{
    class Program
    {
        private static readonly Dictionary<string, Endianess> endianesses =
            new Dictionary<string, Endianess>
            {
                { "little", Endianess.Little },
                { "big", Endianess.Big },
            };

        private static readonly Dictionary<string, Game> games =
            new Dictionary<string, Game>
            {
                { "lsw1", Game.LSW1 },
                { "lsw2", Game.LSW2 },
            };

        private class BaseOptions
        {
            [Option('f', "archive-file", Required = true, HelpText = "The DAT archive to operate on.")]
            public string ArchivePath { get; set; }

            [Option('e', "endian", Default = "little", HelpText = "The endianess of the archive file.")]
            public string Endianess { get; set; }
        }

        [Verb("list", HelpText = "List the contents of existing DAT archives without extracting them.")]
        private class ListOptions : BaseOptions
        {
            [Option('d', "list-dir", Default = "", HelpText = "The path of a specific subdirectory within the archive to traverse and print (rather than the whole thing).")]
            public string ListPath { get; set; }
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
            [Option('g', "game", Required = true, HelpText = "The game of origin for the input file. Can be lsw1 or lsw2.")]
            public string Game { get; set; }

            [Option('p', "patch-dir", Required = true, HelpText = "The directory containing the files to be patched in.\nTo replace files, you must maintain the same relative directory structure as the original DAT.")]
            public string PatchPath { get; set; }

            [Option('o', "output-file", HelpText = "Optional path of output file. The input archive will be preserved and a patched copy will be created at the specified path, or will be overwritten otherwise.")]
            public string OutputPath { get; set; }
        }

        [Verb("build", HelpText = "Build a brand new TT Games DAT archive file.")]
        private class BuildOptions : BaseOptions
        {
            [Option('g', "game", Required = true, HelpText = "The game of origin for the input file. Can be lsw1 or lsw2.")]
            public string Game { get; set; }

            [Option('d', "data-dir", Required = true, HelpText = "The directory containing the files to be packed.")]
            public string DataPath { get; set; }

            [Option('a', "align", Default = 1, HelpText = "The alignment to enforce for embedded file data. Setting to 1 disables any alignment control.")]
            public int FileAlign { get; set; }
        }

        private static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.Unicode;
            Parser.Default.ParseArguments<ListOptions, ExtractOptions, BuildOptions, ModifyOptions>(args)
                .WithParsed<ListOptions>(RunList)
                .WithParsed<ExtractOptions>(RunExtract)
                .WithParsed<BuildOptions>(RunBuild)
                .WithParsed<ModifyOptions>(RunModify);
        }

        private static void RunList(ListOptions options)
        {
            var endianess = endianesses[options.Endianess.ToLowerInvariant()];

            using var archive = new Archive(options.ArchivePath, ArchiveMode.ReadOnly, Game.LSW1, endianess);
            archive.Read();

            archive.List(options.ListPath);
        }

        private static void RunExtract(ExtractOptions options)
        {
            var endianess = endianesses[options.Endianess.ToLowerInvariant()];

            var dir = Path.GetDirectoryName(options.ArchivePath);
            var glob = Path.GetFileName(options.ArchivePath);

            foreach (var fileName in Glob.Files(dir == "" ? "." : dir, glob))
            {
                var path = Path.Combine(dir, fileName);

                using var archive = new Archive(path, ArchiveMode.ReadOnly, Game.LSW1, endianess);
                archive.Read();

                archive.Extract(options.ExtractPath, options.Decompress);
            }
        }

        private static void RunBuild(BuildOptions options)
        {
            var game = games[options.Game.ToLowerInvariant()];
            var endianess = endianesses[options.Endianess.ToLowerInvariant()];

            using var archive = new Archive(options.ArchivePath, ArchiveMode.BuildNew, game, endianess);
            archive.Build(options.DataPath, options.FileAlign);
        }

        private static void RunModify(ModifyOptions options)
        {
            var game = games[options.Game.ToLowerInvariant()];
            var endianess = endianesses[options.Endianess.ToLowerInvariant()];

            using var archive = new Archive(options.ArchivePath, ArchiveMode.ReadOnly, game, endianess);
            archive.Read();

            archive.Patch(options.PatchPath, options.OutputPath);
        }
    }
}
