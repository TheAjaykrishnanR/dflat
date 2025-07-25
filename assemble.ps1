rm -rf dflat
mkdir dflat
mkdir -p dflat\csc
mkdir -p dflat\ilc
mkdir -p dflat\linker

# build csc (dotnet\roslyn)

git clone --depth 1 -b main https://github.com/dotnet/roslyn 
rm roslyn\src\Compilers\CSharp\csc\AnyCpu\csc.csproj
cp csc.csproj roslyn\src\Compilers\CSharp\csc\AnyCpu\csc.csproj
roslyn\build.cmd

cp roslyn\artifacts\bin\csc\Release\net<>\win-x64\publish\csc.exe dflat\csc\csc.exe

# build ilc, runtime (dotnet\runtime)
git clone --depth 1 -b main https://github.com/dotnet/runtime
runtime\build.cmd clr.nativeaotlibs+clr.nativeaotruntime+clr.alljits+clr.tools+libs -rc Release -lc Release
