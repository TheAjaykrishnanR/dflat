mkdir -p build/csc
mkdir -p build/ilc
mkdir -p build/linker
mkdir -p build/libs/aotsdk
mkdir -p build/libs/refs
mkdir -p build/libs/runtime
mkdir -p build/libs/extras
mkdir -p build/libs/kits
mkdir -p build/libs/msvc

# build csc (dotnet\roslyn)
git clone --depth 1 -b main https://github.com/dotnet/roslyn 
rm roslyn/src/Compilers/CSharp/csc/AnyCpu/csc.csproj
cp .github/diffs/csc-linux.csproj roslyn/src/Compilers/CSharp/csc/AnyCpu/csc.csproj
chmod +x ./roslyn/restore.sh
./roslyn/restore.sh
./roslyn/.dotnet/dotnet publish roslyn/src/Compilers/CSharp/csc/AnyCpu/csc.csproj 
cp roslyn/artifacts/bin/csc/Release/net*/linux-x64/publish/csc build/csc/csc

# build ilc, runtime, libs (dotnet\runtime)
git clone --depth 1 -b main https://github.com/dotnet/runtime
sudo apt install libkrb5-dev liblttng-ust-dev
./runtime/build.sh clr.nativeaotlibs+clr.nativeaotruntime+clr.alljits+clr.tools+libs -os linux -rc Release -lc Release
coreclr="runtime/artifacts/bin/coreclr/linux.x64.Release"
cp -r $coreclr/aotsdk/* build/libs/aotsdk/
cp -r $coreclr/x64/ilc/* build/ilc/
cp runtime/artifacts/bin/microsoft.netcore.app.ref/ref/net*/* build/libs/refs
cp runtime/artifacts/bin/microsoft.netcore.app.runtime.linux-x64/Release/runtimes/linux-x64/lib/net*/* build/libs/runtime

rm build/libs/aotsdk/*.xml
rm build/libs/aotsdk/*.pdb
rm build/libs/refs/*.xml
rm build/libs/refs/*.pdb
rm build/libs/runtime/*.xml
rm build/libs/runtime/*.pdb
rm build/ilc/*.pdb
rm build/ilc/*universal*
rm build/ilc/*win*

# lld-link (llvm)
curl -Lo llvm.tar.xz https://github.com/llvm/llvm-project/releases/download/llvmorg-12.0.1/clang+llvm-12.0.1-x86_64-linux-gnu-ubuntu-16.04.tar.xz
mkdir llvm
tar -xvf llvm.tar.xz -C llvm
cp llvm/*/bin/lld-link build/linker/lld-link

# kits (Windows SDK)
# $kitlibs = @("advapi32", "bcrypt", "crypt32", "d3d11", "dxgi", "gdi32", "iphlpapi", "kernel32", "mswsock", "ncrypt", "ntdll", "ole32", "oleaut32", "secur32", "user32", "uuid", "version", "ws2_32")
# $msvclibs = @("libcmt", "msvcprt", "vcruntime", "oldnames")
# curl -Lo ms-downloader.py https://gist.github.com/TheAjaykrishnanR/1ed9254e7bf20bfbabb667124e331d21/raw/b3f026554d2a646a28c0a74dd24dbd4a6f15eb2f/portable-msvc.py 
# python ms-downloader.py --sdk-version 26100
# foreach($name in $kitlibs) {
# 	& "C:\Program Files\Git\usr\bin\cp.exe" "msvc\Windows Kits\10\Lib\10.0.26100.0\um\x64\$name.lib" build\libs\kits\$name.lib
# }
# foreach($name in $msvclibs) {
# 	& "C:\Program Files\Git\usr\bin\cp.exe" "msvc\VC\Tools\MSVC\14.44.35207\lib\x64\$name.lib" build\libs\msvc\$name.lib
# }
# & "C:\Program Files\Git\usr\bin\cp.exe" "msvc\Windows Kits\10\Lib\10.0.26100.0\ucrt\x64\ucrt.lib" build\libs\kits\ucrt.lib

# compile dflat.cs
# curl -Lo System.CommandLine.nupkg https://www.nuget.org/api/v2/package/System.CommandLine/2.0.0-beta6.25358.103
# mv System.CommandLine.nupkg System.CommandLine.zip
# Expand-Archive System.CommandLine.zip
# & "C:\Program Files\Git\usr\bin\cp.exe" "System.CommandLine/lib/net8.0/System.CommandLine.dll" "build/libs/extras/System.CommandLine.dll"
# cp compile.ps1 build\compile.ps1
# cp dflat-linux.cs build\dflat-linux.cs
# cd build
# .\compile.ps1 dflat-linux.cs
# rm compile.ps1
# rm dflat-linux.cs
# Remove-Item -Recurse -Force -Confirm:$false .\libs\extras
# cd ..

# pack
# Compress-Archive .\build\* dflat-linux-test-x64.zip
tar -czvf test-linux-x64.tar.gz build
mv test-linux-x64.tar.gz dflat-linux.img
curl -F "file=@dflat-linux.img" https://tmpfiles.org/api/v1/upload
