# DATOneArchiver

For as long as the [TT Games Modding Community](https://discord.gg/ttlegomods) has existed, so has a **big** problem.  The majority of the games require their data to be bundled inside of **opaque**, **proprietary**, and **ugly** archive files known none-other as **DAT files** (.dat is the extension, short for "data" of course.  Very creative TT devs... 🙄).  

Thankfully, a long time ago on a computer no more than half the circumference of Earth away, a clever programmer known as **Luigi "Aluigi" Auriemma** took the time and energy to reverse engineer the **many variations** *(more info on that later)* of the DAT file format to the point that their data can be extracted, decompressed, and, under some circumstances, modified on a per file basis using a tool of his own design named **[QuickBMS](http://aluigi.altervista.org/quickbms.htm)**.  

However, that last part has severe limitations.  To modify a file within a DAT archive using **QuickBMS**:

1. The file must already exist within the DAT archive.  In other words, if `fart.txt` does not already exist in `POTTY_HUMOR.DAT`, you're out of luck; It cannot be added.  
2. The new version must be either smaller than or the same size as the current in-archive data
3. You must be lucky (somewhat). **QuickBMS**, like all other software, isn't perfect.  Bugs exist that can and will screw stuff up.  

**Enter DATOneArchiver**. Though it is not a be-all, end-all solution, it has the potential to become just that.  

## What it do?

**DATOneArchiver** in it's current state (v1.0) is a complete toolset for extracting, modifying, and creating TT Games DAT archive files for these games:

* Lego Star Wars: The Video Game (**PC**, Mac, **PS2**, Xbox, GameCube)
* Lego Star Wars II: The Original Trilogy (PC, Mac, **PS2**, Xbox, GameCube)

Platforms that have been tested and confirmed working are **bolded** above.  However, theoretically all of those listed should work.  See **Help Wanted** for info on how you can help fill in the blanks.  

## How get?

**DATOneArchiver** is a command-line based tool written in .NET (currently targets 5.x).  To use the tool, it can either be compiled or downloaded pre-built (recommended).

### To Compile

1. Download and install
   1. [.NET 5 SDK](https://dotnet.microsoft.com/download/dotnet/5.0)
   2. [Git](https://git-scm.com/)
2. Open up a terminal and enter
   1. `git clone https://www.github.com/yodadude2003/DATOneArchiver.git`
   2. `cd DATOneArchiver`
   3. `git submodule update --init --recursive`
   4. `cd DATOneArchiver` (yes, again)
   5. `dotnet build -c Release`
3. Find your binaries. Should be in `bin/Release/net5.0`. 
4. Copy the `propack_bin` folder to this location and rename it to `propack` (or compile the code found in `rnc_propack` and relocate it in a similar fashion)
5. Proceed with **How Use?** instructions

### To Download

1. Download and install the [.NET 5 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/5.0)
2. Download the **latest release** here on GitHub or on the [Chosen Few Software website](https://www.chosenfewsoftware.com/)
3. Extract the zip archive wherever you please
4. Proceed with **How Use?** instructions

## How Use?

Well, depends on exactly *what* you want to do with it!  First of all, nab a DAT file from your nearest copy of LSW1 or LSW2 and place it in a given directory.  From now on we will refer to the full path of this file as `<archive-path>`.  

Please note the following: 

* You may want to add the **DATOneArchiver** binaries directory to your `PATH` for convenience.  
* All further instructions require the use of a command-line shell.  `cmd` is best on Windows systems.  If you don't know how to access/use it, use Google or your favorite alternative search engine.

Launch a command-line shell instance in the **DATOneArchiver** binaries directory (or if you added it to `PATH`, wherever else is most convenient).  Next find your preferred action below.

### To List Contents

`datonearchiver list -f <archive-path> [-e little|big] [-d <list-dir>]`

Verb: `list`

Parameters:

* `-f` (required): Path to an archive file the user desires to list the contents of.

* `-e` (optional): Endianness of the archive file.  Value must be either `little` or `big`.  Defaults to `little` if left unspecified.

* `-d` (optional): Path of directory/subdirectory within the archive file to list the contents of.  Useful for making output less verbose in cases where the user is only interested in listing a portion of the contents.  Entire archive tree is printed if left unspecified.  

Expected behavior: 

Outputs "ASCII art" file tree of DAT archive contents; similar to the `tree` command from Windows/MS-DOS.  

### To Extract Contents

`datonearchiver extract -f <archive-path> -o <output-dir> [-c] [-e little|big]`

Verb: `extract`

Parameters:

* `-f` (required): Path to an archive file the user desires to extract the contents of.  Wildcards are allowed.

* `-o` (required): Path of a directory to create and extract the archive data to.  
* `-c` (optional): If specified, signals **DATOneArchiver** to decompress any compressed files it finds during extraction (if the compression scheme is supported.  See **Help Wanted**).  Otherwise, all files will be extracted in their raw, compressed form.  
* `-e` (optional): Endianness of the archive file.  Value must be either `little` or `big`.  Defaults to `little` if left unspecified.

### To Modify/Add to Contents

`datonearchiver modify -f <archive-path> -g lsw1|lsw2 -p <patch-dir> [-o <output-file>] [-e little|big]`

Verb: `modify`

Parameters:

* `-f` (required): Path to an archive file the user desires to modify the contents of.
* `-g` (required): The game that the output archive is going to be used with.  Value must be either `lsw1` or `lsw2`.
* `-p` (required): Path to a directory in which the files the user desires to inject reside, structured similarly to the contents of the target archive.

* `-o` (optional): If unspecified, **DATOneArchiver** will overwrite the input archive with the desired modifications.  To prevent data loss, the user may specify a path to a new file in which the modified output is placed.
* `-e` (optional): Endianness of the archive file.  Value must be either `little` or `big`.  Defaults to `little` if left unspecified.

### To Create an Archive from Scratch

`datonearchiver build -f <archive-path> -g lsw1|lsw2 -d <data-dir> [-a <alignment-size>] [-e little|big]`

Verb: `build`

Parameters:

* `-f` (required): Path of the new archive file that is to be created.
* `-g` (required): The game that the output archive is going to be used with.  Value must be either `lsw1` or `lsw2`.

* `-d` (required): Path to the directory that should be made into an archive.
* `-a` (optional): A decimal integer number representing a size in bytes.  If specified, archive contents will be padded so that the beginning of every file is aligned to the specified boundary.  Otherwise, no padding is added.  Use `-a 2048` when making archive files for the PS2 games.  
* `-e` (optional): Endianness of the archive file.  Value must be either `little` or `big`.  Defaults to `little` if left unspecified.

## Questions? Problems? Feedback?

**Questions & Problems:** Join https://discord.gg/ttlegomods.  I'm an active member/moderator of the server and we'd love to help you if you're curious or if you can't seem to figure it out.  Make sure that you give proper context regarding your issue, it makes everybody's life easier.  

**Feedback:** If you have any technical feedback to offer, open an issue on this here repo.  

## Help Wanted

**This project is nowhere near done!!!** If you have any RE experience or knowledge that you can contribute to this project (and the time), *DEW IT!!*

Here's a laundry list of missing features/information:

* Graphical user interface
* Support for more games and format variations (specifically TCS, LSW3 & TFA)
* Support for more compression methods
* Hardware and emulation tests on the various versions of supported games.  
  *Open an issue if you happen to do some testing!*

