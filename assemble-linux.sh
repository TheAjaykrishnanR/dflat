# mkdir -p build/csc
# mkdir -p build/ilc
# mkdir -p build/linker
# mkdir -p build/libs/aotsdk
# mkdir -p build/libs/refs
# mkdir -p build/libs/runtime
# mkdir -p build/libs/extras
# mkdir -p build/libs/kits
# mkdir -p build/libs/msvc

# build csc (dotnet\roslyn)
# git clone --depth 1 -b main https://github.com/dotnet/roslyn 
# rm roslyn/src/Compilers/CSharp/csc/AnyCpu/csc.csproj
# cp .github/diffs/csc-linux.csproj roslyn/src/Compilers/CSharp/csc/AnyCpu/csc.csproj
# chmod +x ./roslyn/restore.sh
# ./roslyn/restore.sh
# ./roslyn/.dotnet/dotnet publish roslyn/src/Compilers/CSharp/csc/AnyCpu/csc.csproj 
# cp roslyn/artifacts/bin/csc/Release/net*/linux-x64/publish/csc build/csc/csc

# build ilc, runtime, libs (dotnet\runtime)
# git clone --depth 1 -b main https://github.com/dotnet/runtime
# sudo apt install libkrb5-dev liblttng-ust-dev
# ./runtime/build.sh clr.nativeaotlibs+clr.nativeaotruntime+clr.alljits+clr.tools+libs -rc Release -lc Release
# coreclr="runtime/artifacts/bin/coreclr/linux.x64.Release"
# cp -r $coreclr/aotsdk/* build/libs/aotsdk/
# cp -r $coreclr/x64/ilc/* build/ilc/
# cp runtime/artifacts/bin/microsoft.netcore.app.ref/ref/net*/* build/libs/refs
# cp runtime/artifacts/bin/microsoft.netcore.app.runtime.linux-x64/Release/runtimes/linux-x64/lib/net*/* build/libs/runtime
# cp runtime/artifacts/bin/native/*/*.a build/libs/aotsdk
# 
# rm build/libs/aotsdk/*.xml
# rm build/libs/aotsdk/*.pdb
# rm build/libs/refs/*.xml
# rm build/libs/refs/*.pdb
# rm build/libs/runtime/*.xml
# rm build/libs/runtime/*.pdb
# rm build/ilc/*.pdb
# rm build/ilc/*universal*
# rm build/ilc/*win*

curl -Lo dflat.tar.gz http://tmpfiles.org/dl/9500485/dflat.img
mkdir build
tar -xvf dflat.tar.gz -C build

# install pwsh
curl -Lo pwsh.deb https://github.com/PowerShell/PowerShell/releases/download/v7.4.11/powershell_7.4.11-1.deb_amd64.deb
sudo dpkg -i pwsh.deb

# compile dflat.cs
curl -Lo System.CommandLine.nupkg https://www.nuget.org/api/v2/package/System.CommandLine/2.0.0-beta6.25358.103
mv System.CommandLine.nupkg System.CommandLine.zip
Expand-Archive System.CommandLine.zip
cp "System.CommandLine/lib/net8.0/System.CommandLine.dll" "build/libs/extras/System.CommandLine.dll"
cp compile-linux.ps1 build/compile-linux.ps1
cp dflat-linux.cs build/dflat-linux.cs
cd build
pwsh compile-linux.ps1 dflat-linux.cs
mv dflat-linux.exe dflat
rm compile-linux.ps1
rm dflat-linux.cs
rm -rf ./libs/extras
cd ..

# pack
tar -czvf dflat-linux-x64.tar.gz build/*
