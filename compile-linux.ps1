# cleanup
rm *exe
rm *.obj

$csFile = $args[0]
if($args.Length -eq 0) { 
	Write-Host "specify a source file"
	return 
}
$program = $csFile.Replace(".cs", "")
$cwd = $(Get-Location).Path

# .cs -> IL [csc.exe]
$refFolder = "./libs/refs"
$refs = @()
foreach($dll in Get-ChildItem $refFolder | Where-Object -Property Name -Like "*dll") 
{
	$refs += "/r:$($dll.FullName) "
}
$extrasFolder = "./libs/extras"
$extras = @()
foreach($dll in Get-ChildItem $extrasFolder | Where-Object -Property Name -Like "*dll") 
{
	$extras += "/r:$($dll.FullName) "
}
$ilexe = "$program.il.exe";
.\csc\csc $csFile @refs @extras "/out:$ilexe"

# IL -> Bytecode [ILCompiler dotnet\runtime\artifacts\bin\coreclr\windows.x64.Release\x64\ilc\ilc.exe]
$aotsdk = "$cwd/libs/aotsdk"
$obj = "$program.obj"
.\ilc\ilc `
	$ilexe `
	--out $obj `
	-r:"$aotsdk/*.dll" `
	-r:"$cwd/libs/runtime/*.dll" `
	-r:"$cwd/libs/extras/*.dll" `
	-g `
	--generateunmanagedentrypoints:System.Private.CoreLib,HIDDEN `
	--dehydrate `
	--initassembly:System.Private.CoreLib `
	--initassembly:System.Private.StackTraceMetadata `
	--initassembly:System.Private.TypeLoader `
	--initassembly:System.Private.Reflection.Execution `
	--directpinvoke:libSystem.Native `
	--directpinvoke:libSystem.Globalization.Native `
	--directpinvoke:libSystem.IO.Compression.Native `
	--directpinvoke:libSystem.Net.Security.Native `
	--directpinvoke:libSystem.Security.Cryptography.Native.OpenSsl `
	--stacktracedata `
	--scanreflection `
	--feature:System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization=false `
	--feature:System.Diagnostics.Tracing.EventSource.IsSupported=false `
	--feature:System.Resources.ResourceManager.AllowCustomResourceTypes=false `
	--feature:System.Linq.Expressions.CanEmitObjectArrayDelegate=false `
	--feature:System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported=false `
	--feature:System.Globalization.Invariant=true `
	--feature:System.Diagnostics.Debugger.IsSupported=false `
	--feature:System.StartupHookProvider.IsSupported=false `


$glibc = "/lib/x86_64-linux-gnu";
$gcclibs = "/usr/lib/gcc/x86_64-linux-gnu/13";
ld.bfd `
	"$glibc/crt1.o" `
	"$glibc/crti.o" `
	"$gcclibs/crtbeginS.o" `
	$obj `
	"$aotsdk/libbootstrapper.o" `
	"$aotsdk/libRuntime.WorkstationGC.a" `
	"$aotsdk/libeventpipe-disabled.a" `
	"$aotsdk/libRuntime.VxsortEnabled.a" `
	"$aotsdk/libstandalonegc-disabled.a" `
	"$aotsdk/libstdc++compat.a" `
	"$aotsdk/libSystem.Native.a" `
	"$aotsdk/libSystem.Globalization.Native.a" `
	"$aotsdk/libSystem.IO.Compression.Native.a" `
	"$aotsdk/libSystem.Net.Security.Native.a" `
	"$aotsdk/libSystem.Security.Cryptography.Native.OpenSsl.a" `
	"$aotsdk/libz.a" `
	"$aotsdk/libaotminipal.a" `
	"$aotsdk/libbrotlicommon.a" `
	"$aotsdk/libbrotlidec.a" `
	"$aotsdk/libbrotlienc.a" `
	"$glibc/libc.so" `
	"$glibc/libc.so.6" `
	"$glibc/libpthread.so.0" `
	"$glibc/libm.so" `
	"$glibc/libm.so.6" `
	"$glibc/libdl.so.2" `
	"$glibc/librt.so.1" `
	"$gcclibs/crtendS.o" `
	"$glibc/crtn.o" `
	--output=$program.exe `
	--dynamic-linker=/lib64/ld-linux-x86-64.so.2 `
	--nostdlib `
	--compress-debug-sections=zlib `
	--discard-all `
	--gc-sections `
	--as-needed `
	--strip-all `
	-pie `
	-z relro `
	-z now `
	--eh-frame-hdr `
	--export-dynamic `

# cleanup
rm "$program.il.exe"
rm "$program.obj"
