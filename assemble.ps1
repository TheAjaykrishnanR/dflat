mkdir -p build\csc
mkdir -p build\ilc
mkdir -p build\linker
mkdir -p build\libs\refs
mkdir -p build\libs\runtime

# build csc (dotnet\roslyn)
# git clone --depth 1 -b main https://github.com/dotnet/roslyn 
# rm roslyn\src\Compilers\CSharp\csc\AnyCpu\csc.csproj
# cp .github\diffs\csc.csproj roslyn\src\Compilers\CSharp\csc\AnyCpu\csc.csproj
# roslyn\restore.cmd
# roslyn\.dotnet\dotnet.exe publish roslyn\src\Compilers\CSharp\csc\AnyCpu\csc.csproj 
# 
# cp roslyn\artifacts\bin\csc\Release\net9.0\win-x64\publish\csc.exe build\csc\csc.exe

# build ilc, runtime, libs (dotnet\runtime)
git clone --depth 1 -b main https://github.com/dotnet/runtime
runtime\build.cmd clr.nativeaotlibs+clr.nativeaotruntime+clr.alljits+clr.tools+libs -rc Release -lc Release

$coreclr = runtime\artifacts\bin\coreclr\windows.x64.Release\
cp "$coreclr\aotsdk" build\libs\
cp "$coreclr\x64\ilc\*" build\ilc\
cp runtime\artifacts\bin\microsoft.netcore.app.ref\net10.0\* build\libs\refs
cp runtime\artifacts\bin\microsoft.netcore.app.runtime.win-x64\Release\runtimes\win-x64\lib\net10.0 build\libs\runtime
cp dotnet\runtime\src\coreclr\nativeaot\BuildIntegration\WindowsAPIs.txt build\libs\WindowsAPIs.txt

# lld-link (llvm)
curl -Lo llvm.tar.xz https://github.com/llvm/llvm-project/releases/download/llvmorg-18.1.8/clang+llvm-18.1.8-x86_64-pc-windows-msvc.tar.xz
tar -xvf llvm.tar.xz -C llvm
cp llvm\bin\lld-link.exe build\linker\lld-link.exe

# compile dflat.cs
cp compile.ps1 build\compile.ps1
cp dflat.cs build\dflat.cs
cd build
compile.ps1 dflat.cs
rm compile.ps1
rm dflat.cs
cd ..

# package
tar -czvf dflat.tar.gz build
