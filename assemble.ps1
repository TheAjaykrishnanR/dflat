mkdir -p build\csc
mkdir -p build\ilc
mkdir -p build\linker
mkdir -p build\libs\aotsdk
mkdir -p build\libs\refs
mkdir -p build\libs\runtime

# build csc (dotnet\roslyn)
# git clone --depth 1 -b main https://github.com/dotnet/roslyn 
# rm roslyn\src\Compilers\CSharp\csc\AnyCpu\csc.csproj
# cp .github\diffs\csc.csproj roslyn\src\Compilers\CSharp\csc\AnyCpu\csc.csproj
# roslyn\restore.cmd
# roslyn\.dotnet\dotnet.exe publish roslyn\src\Compilers\CSharp\csc\AnyCpu\csc.csproj 
# 
# cp roslyn\artifacts\bin\csc\Release\net*\win-x64\publish\csc.exe build\csc\csc.exe

# build ilc, runtime, libs (dotnet\runtime)
# git clone --depth 1 -b main https://github.com/dotnet/runtime
# runtime\build.cmd clr.nativeaotlibs+clr.nativeaotruntime+clr.alljits+clr.tools+libs -rc Release -lc Release
# 
# $coreclr = "runtime\artifacts\bin\coreclr\windows.x64.Release\"
# cp "$coreclr\aotsdk\*" build\libs\aotsdk\
# cp "$coreclr\x64\ilc\*" build\ilc\
# cp runtime\artifacts\bin\microsoft.netcore.app.ref\ref\net*\* build\libs\refs
# cp runtime\artifacts\bin\microsoft.netcore.app.runtime.win-x64\Release\runtimes\win-x64\lib\net*\* build\libs\runtime
# cp runtime\src\coreclr\nativeaot\BuildIntegration\WindowsAPIs.txt build\libs\WindowsAPIs.txt
# 
# Remove-Item -Recurse -Force -Confirm:$false "build\libs\aotsdk\*.xml"
# Remove-Item -Recurse -Force -Confirm:$false "build\libs\aotsdk\*.pdb"
# Remove-Item -Recurse -Force -Confirm:$false "build\libs\refs\*.xml"
# Remove-Item -Recurse -Force -Confirm:$false "build\libs\refs\*.pdb"
# Remove-Item -Recurse -Force -Confirm:$false "build\libs\runtime\*.xml"
# Remove-Item -Recurse -Force -Confirm:$false "build\libs\runtime\*.pdb"

# lld-link (llvm)
curl -Lo llvm.tar.xz https://github.com/llvm/llvm-project/releases/download/llvmorg-18.1.8/clang+llvm-18.1.8-x86_64-pc-windows-msvc.tar.xz
mkdir llvm
& "C:\Program Files\Git\usr\bin\tar.exe" -xvf llvm.tar.xz -C llvm
cp llvm\*\bin\lld-link.exe build\linker\lld-link.exe

# compile dflat.cs
cp compile.ps1 build\compile.ps1
cp dflat.cs build\dflat.cs
cd build
compile.ps1 dflat.cs
rm compile.ps1
rm dflat.cs
cd ..

# pack
& "C:\Program Files\Git\usr\bin\tar.exe" -czvf dflat.tar.gz .\build
