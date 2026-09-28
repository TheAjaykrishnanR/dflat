## dflat, a native aot compiler for C#

> A portable and relatively lightweight C# AOT compiler. Build native executables, libraries on windows and linux. Inspired by  [bflat](https://github.com/bflattened/bflat)

<br/>
<p align="center">
    <img src="https://github.com/TheAjaykrishnanR/dflat/blob/master/imgs/demo.gif"/>
</p>

### Usage

Download from [releases](https://github.com/TheAjaykrishnanR/dflat/releases/latest)

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

#### Windows

Preferrable to run it as a github [workflow](https://github.com/TheAjaykrishnanR/dflat/blob/master/.github/workflows/build_dflat.yaml)

Requirements:

```
1. Git
2. python
```

Build:

```
git clone https://github.com/TheAjaykrishnanR/dflat
cd dflat
.\1_0_download_build_assemble.ps1
```

#### Linux

Preferrable to run it as a github [workflow](https://github.com/TheAjaykrishnanR/dflat/blob/master/.github/workflows/build_dflat_linux.yaml)

Requirements:

```
GLIBC>=2.38
Git
Python
Powershell
binutils
```

Build:

```
git clone https://github.com/TheAjaykrishnanR/dflat
cd dflat
.\3_0_download_build_assemble_linux.sh
```

### Wiki

Please the read the [wiki](https://github.com/TheAjaykrishnanR/dflat/blob/master/wiki/landing.md) for guides and other relevant information;
