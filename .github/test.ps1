mkdir -p build\csc
mkdir -p build\ilc
mkdir -p build\linker
mkdir -p build\libs\aotsdk
mkdir -p build\libs\refs
mkdir -p build\libs\runtime
mkdir -p build\libs\extras
mkdir -p build\libs\kits

$kitlibs = @("advapi32", "bcrypt", "crypt32", "iphlpapi", "kernel32", "mswsock", "ncrypt", "ntdll", "ole32", "oleaut32", "secur32", "version", "ws2_32", "user32")
curl -Lo ms-downloader.py https://gist.github.com/TheAjaykrishnanR/1ed9254e7bf20bfbabb667124e331d21/raw/b3f026554d2a646a28c0a74dd24dbd4a6f15eb2f/portable-msvc.py 
python ms-downloader.py --sdk-version 26100
foreach($name in $kitlibs) {
	& "C:\Program Files\Git\usr\bin\cp.exe" "msvc\Windows Kits\10\Lib\10.0.26100.0\um\x64\$name.lib" build\libs\kits\$name.lib
}
