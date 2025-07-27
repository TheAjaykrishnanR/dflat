mkdir -p build\csc
mkdir -p build\ilc
mkdir -p build\linker
mkdir -p build\libs\aotsdk
mkdir -p build\libs\refs
mkdir -p build\libs\runtime
mkdir -p build\libs\extras
mkdir -p build\libs\kits
mkdir -p build\libs\msvc

# build csc (dotnet\roslyn)
git clone --depth 1 -b main https://github.com/dotnet/roslyn 
rm roslyn\src\Compilers\CSharp\csc\AnyCpu\csc.csproj
cp .github\diffs\csc.csproj roslyn\src\Compilers\CSharp\csc\AnyCpu\csc.csproj
roslyn\restore.cmd
roslyn\.dotnet\dotnet.exe publish roslyn\src\Compilers\CSharp\csc\AnyCpu\csc.csproj 
cp roslyn\artifacts\bin\csc\Release\net*\win-x64\publish\csc.exe build\csc\csc.exe

# build ilc, runtime, libs (dotnet\runtime)
git clone --depth 1 -b main https://github.com/dotnet/runtime
runtime\build.cmd clr.nativeaotlibs+clr.nativeaotruntime+clr.alljits+clr.tools+libs -rc Release -lc Release
$coreclr = "runtime\artifacts\bin\coreclr\windows.x64.Release\"
cp "$coreclr\aotsdk\*" build\libs\aotsdk\
cp "$coreclr\x64\ilc\*" build\ilc\
cp runtime\artifacts\bin\microsoft.netcore.app.ref\ref\net*\* build\libs\refs
cp runtime\artifacts\bin\microsoft.netcore.app.runtime.win-x64\Release\runtimes\win-x64\lib\net*\* build\libs\runtime
cp runtime\src\coreclr\nativeaot\BuildIntegration\WindowsAPIs.txt build\libs\WindowsAPIs.txt

Remove-Item -Recurse -Force -Confirm:$false "build\libs\aotsdk\*.xml"
Remove-Item -Recurse -Force -Confirm:$false "build\libs\aotsdk\*.pdb"
Remove-Item -Recurse -Force -Confirm:$false "build\libs\refs\*.xml"
Remove-Item -Recurse -Force -Confirm:$false "build\libs\refs\*.pdb"
Remove-Item -Recurse -Force -Confirm:$false "build\libs\runtime\*.xml"
Remove-Item -Recurse -Force -Confirm:$false "build\libs\runtime\*.pdb"
Remove-Item -Recurse -Force -Confirm:$false "build\ilc\*.pdb"
Remove-Item -Recurse -Force -Confirm:$false "build\ilc\*unix*"
Remove-Item -Recurse -Force -Confirm:$false "build\ilc\*universal*"

# lld-link (llvm)
curl -Lo llvm.tar.xz https://github.com/llvm/llvm-project/releases/download/llvmorg-18.1.8/clang+llvm-18.1.8-x86_64-pc-windows-msvc.tar.xz
mkdir llvm
& "C:\Program Files\Git\usr\bin\tar.exe" -xvf llvm.tar.xz -C llvm
cp llvm\*\bin\lld-link.exe build\linker\lld-link.exe

# kits (Windows SDK)
$kitlibs = @("advapi32", "bcrypt", "crypt32", "d3d11", "dxgi", "gdi32", "iphlpapi", "kernel32", "mswsock", "ncrypt", "ntdll", "ole32", "oleaut32", "secur32", "user32", "uuid", "version", "ws2_32")
$msvclibs = @("libcmt", "libcpmt", "libucrt", "libvcruntime", "oldnames")
curl -Lo ms-downloader.py https://gist.github.com/TheAjaykrishnanR/1ed9254e7bf20bfbabb667124e331d21/raw/b3f026554d2a646a28c0a74dd24dbd4a6f15eb2f/portable-msvc.py 
python ms-downloader.py --sdk-version 26100
foreach($name in $kitlibs) {
	& "C:\Program Files\Git\usr\bin\cp.exe" "msvc\Windows Kits\10\Lib\10.0.26100.0\um\x64\$name.lib" build\libs\kits\$name.lib
}
foreach($name in $msvclibs) {
	& "C:\Program Files\Git\usr\bin\cp.exe" "msvc\VC\Tools\MSVC\14.44.35207\lib\x64\$name.lib" build\libs\msvc\$name.lib
}

# compile dflat.cs
curl -Lo System.CommandLine.nupkg https://www.nuget.org/api/v2/package/System.CommandLine/2.0.0-beta6.25358.103
mv System.CommandLine.nupkg System.CommandLine.zip
Expand-Archive System.CommandLine.zip
& "C:\Program Files\Git\usr\bin\cp.exe" "System.CommandLine/lib/net8.0/System.CommandLine.dll" "build/libs/extras/System.CommandLine.dll"
cp compile.ps1 build\compile.ps1
cp dflat.cs build\dflat.cs
cd build
.\compile.ps1 dflat.cs
rm compile.ps1
rm dflat.cs
Remove-Item -Recurse -Force -Confirm:$false .\libs\extras
cd ..

# pack
Compress-Archive .\build\* dflat-win-x64.zip
