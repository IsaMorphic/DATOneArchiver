# DATOneDriver

**DATOneDriver** is a user-mode mountable filesystem driver for Windows based on Dokan that allows for direct modification of compatible TT Games DAT files through the OS shell and any other applications that access the filesystem through conventional means. 

## How get?

### Prerequisites

Install the [latest version of Dokan 2.x](https://github.com/dokan-dev/dokany/releases) (I personally recommend the x86 or x64 .msi files)

### To Compile

1. Download and install
   1. [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
   2. [Git](https://git-scm.com/)
2. Open up a terminal and enter
   1. `git clone https://www.github.com/yodadude2003/DATOneArchiver.git`
   2. `cd DATOneArchiver`
   3. `git switch dokan-driver`
   4. `cd DATOneDriver`
   5. `dotnet build -c Debug` (do not do a release build; it is unstable for reasons I do not understand)
3. Find your binaries. Should be in `bin/Debug/net6.0-windows`. 
5. Proceed with **How Use?** instructions

### To Download

1. Download and install the [.NET 6 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/6.0)
2. Download the **latest release** from the [Chosen Few Software website](https://www.chosenfewsoftware.com/)
3. Extract the zip archive wherever you please
4. Proceed with **How Use?** instructions

## How Use?

Well, depends on exactly *what* you want to do with it!  First of all, nab a DAT file from your nearest copy of LSW1 or LSW2 and place it in a given directory.  From now on we will refer to the full path of this file as `<archive-path>`.  

Please note the following: 

* You may want to add the **DATOneDriver** binaries directory to your `PATH` for convenience.  
* All further instructions require the use of a command-line shell.  `cmd` is best on Windows systems.  If you don't know how to access/use it, use Google or your favorite alternative search engine.

Launch a command-line shell instance in the **DATOneDriver** binaries directory (or if you added it to `PATH`, wherever else is most convenient).  Now use this command with appropriate parameters substituted:

`datonedriver list -f <archive-path> -m <mount-dir> -g lsw1|lsw2 [-a <alignment-size>] [-e little|big]`

Parameters:

* `-f` (required): Path of the archive file that is to be mounted.
* `-m` (required): Archive mount point; can be an NTFS folder path or a drive letter (e.g `E`, `F`, `G`, `H`, etc.)
* `-g` (required): The game that the output archive is going to be used with.  Value must be either `lsw1` or `lsw2`.
* `-a` (optional): A decimal integer number representing a size in bytes.  If specified, archive contents will be padded so that the beginning of every file is aligned to the specified boundary.  Otherwise, no padding is added.  Use `-a 2048` when making archive files for the PS2 games.  
* `-e` (optional): Endianness of the archive file.  Value must be either `little` or `big`.  Defaults to `little` if left unspecified.

Once the driver is running, you may access your DAT file as a regular Windows filesystem from the mount-point you specified. All conventional actions and applications work as one would expect. All changes are cached on local persistent storage and *NOT* permanently written to the DAT file itself until the filesystem is unmounted. 

To unmount the DAT file for saving changes, simply press `Ctrl+C` or `Ctrl+Break` in the command window where the driver is running and wait for the command to terminate successfully.  

**ALWAYS KEEP BACKUPS WHEN MODIFYING YOUR GAME FILES.**

## Questions? Problems? Feedback?

**Questions & Problems:** Join https://discord.gg/9gYXPka.  I'm an active member/moderator of the server and we'd love to help you if you're curious or if you can't seem to figure it out.  Make sure that you give proper context regarding your issue, it makes everybody's life easier.  

**Feedback:** If you have any technical feedback to offer, [open an issue](../../issues) on this here repo.  

