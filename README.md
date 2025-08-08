## dflat, a native aot compiler for C#

Inspired by  [bflat](https://github.com/bflattened/bflat)

### Usage

Download from [releases](https://github.com/TheAjaykrishnanR/dflat/releases/tag/dflat-3-1)

```
dflat main.cs
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



