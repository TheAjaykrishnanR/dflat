mkdir -p build\csc
mkdir -p build\ilc
mkdir -p build\linker

# build csc (dotnet\roslyn)

git clone --depth 1 -b main https://github.com/dotnet/roslyn 
rm roslyn\src\Compilers\CSharp\csc\AnyCpu\csc.csproj
cp csc.csproj roslyn\src\Compilers\CSharp\csc\AnyCpu\csc.csproj
roslyn\restore.cmd
dotnet publish roslyn\src\Compilers\CSharp\csc\AnyCpu\csc.csproj 

# cp roslyn\artifacts\bin\csc\Release\net<>\win-x64\publish\csc.exe dflat\csc\csc.exe

# build ilc, runtime (dotnet\runtime)
# git clone --depth 1 -b main https://github.com/dotnet/runtime
# runtime\build.cmd clr.nativeaotlibs+clr.nativeaotruntime+clr.alljits+clr.tools+libs -rc Release -lc Release
