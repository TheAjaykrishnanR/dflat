# Compiling a C# program

1. CSC
2. ILCompiler
3. A linker
4. runtime (managed + native)

`csc.exe`: To get the csc executable we build the csc project in the [dotnet/roslyn](https://github.com/dotnet/roslyn) repo.
A slight modification is made to the `csc.csproj` file so that csc itself is aot compiled and we get a single native executable.However on linux we skip AOT due to some quirks and publish it just as a single file.

`ilc.exe`: Building the [dotnet/runtime](https://github.com/dotnet/runtime) repo yields `ilc.exe`.

`linker`: On Windows we go with the native MSVC `link.exe` and on Linux we use the native `ld.bfd` linker part of gnu binutils.
