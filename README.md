## dflat, a native aot compiler for C#

>Inspired by  [bflat](https://github.com/bflattened/bflat)

<p align="center">
    <img src="https://github.com/TheAjaykrishnanR/dflat/blob/master/imgs/WindowsTerminal_IMA1T6cL6n.gif"/>
</p>

### Usage

Download from [releases](https://github.com/TheAjaykrishnanR/dflat/releases/tag/dflat-3-1)

```
Description:
  dflat, a native aot compiler for c#
  Ajaykrishnan R, 2025

Usage:
  dflat [<SOURCE FILES>...] [options]

Arguments:
  <SOURCE FILES>  .cs files to compile

Options:
  /?, /h, /help                                                      Show help and usage information
  /version                                                           Show version information
  /out                                                               Output file name
  /main                                                              Specify the class containing Main()
  /r                                                                 Additional reference .dlls or folders containing them
  /il                                                                Compile to IL
  /verbosity                                                         Set verbosity
  /langversion                                                       Print supported lang versions
  /target <EXE|LIBRARY|WINEXE>                                       Specify the target
  /platform <anycpu|anycpu32bitpreferred|arm|arm64|Itamium|x64|x86>  Specify the platform
  /optimize                                                          optimize
  /csc                                                               extra csc flags [as a single string]
  /ilc                                                               extra ilc flags [as a single string]
  /lld                                                               extra lld flags [as a single string]
```

### Building

Preferrable to run it as a github [workflow](https://github.com/TheAjaykrishnanR/dflat/blob/master/.github/workflows/build_dflat.yaml)

Requirements:

```
1. Git
2. python
```

```
git clone https://github.com/TheAjaykrishnanR/dflat
cd dflat
.\assemble.ps1
```

### How it works

To compile a C# program to a native executable we need:

1. CSC
2. ILCompiler
3. A linker
4. runtime (managed + native)

`csc.exe` : To get the csc executable we build the csc project in the [dotnet/runtime](https://github.com/dotnet/runtime) repo.
A slight modification is made to the `csc.csproj` file so that csc itself is aot compiled and we get a single native executable.



