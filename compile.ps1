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
$refFolder = ".\libs\refs"
$refs = @()
foreach($dll in Get-ChildItem $refFolder | Where-Object -Property Name -Like "*dll") 
{
	$refs += "/r:$($dll.FullName) "
}
$extrasFolder = ".\libs\extras"
$extras = @()
foreach($dll in Get-ChildItem $extrasFolder | Where-Object -Property Name -Like "*dll") 
{
	$extras += "/r:$($dll.FullName) "
}
$ilexe = "$program.il.exe";
.\csc\csc $csFile @refs @extras "/out:$ilexe"

# IL -> Bytecode [ILCompiler dotnet\runtime\artifacts\bin\coreclr\windows.x64.Release\x64\ilc\ilc.exe]
$aotsdk = "$cwd\libs\aotsdk"
$obj = "$program.obj"
.\ilc\ilc.exe `
	$ilexe `
	--out $obj `
	-r:"$aotsdk\*.dll" `
	-r:"$cwd\libs\runtime\*.dll" `
	-r:"$cwd\libs\extras\*.dll" `
	-g `
	--generateunmanagedentrypoints:System.Private.CoreLib,HIDDEN `
	--dehydrate `
	--initassembly:System.Private.CoreLib `
	--initassembly:System.Private.StackTraceMetadata `
	--initassembly:System.Private.TypeLoader `
	--initassembly:System.Private.Reflection.Execution `
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
	--directpinvokelist:"$cwd\libs\WindowsAPIs.txt" `
	--directpinvoke:System.Globalization.Native `
	--directpinvoke:System.IO.Compression.Native 

$kits = "$cwd\libs\kits"
$msvc = "$cwd\libs\msvc"
.\linker\link $obj `
	"$aotsdk\bootstrapper.obj" `
	"$aotsdk\dllmain.obj" `
	"$aotsdk\Runtime.ServerGC.lib" `
	"$aotsdk\standalonegc-disabled.lib" `
	"$aotsdk\aotminipal.lib" `
	"$aotsdk\brotlicommon.lib" `
	"$aotsdk\eventpipe-enabled.lib" `
	"$aotsdk\Runtime.WorkstationGC.lib" `
	"$aotsdk\brotlidec.lib" `
	"$aotsdk\brotlienc.lib" `
	"$aotsdk\Runtime.VxsortEnabled.lib" `
	"$aotsdk\System.Globalization.Native.Aot.lib" `
	"$aotsdk\System.IO.Compression.Native.Aot.lib" `
	"$aotsdk\zlibstatic.lib" `
	"$kits\advapi32.lib" `
	"$kits\ole32.lib" `
	"$kits\bcrypt.lib" `
	"$kits\user32.lib" `
	"$kits\kernel32.lib" `
	"$kits\version.lib" `
	"$msvc\libcmt.lib" `
	"$msvc\msvcprt.lib" `
	"$msvc\vcruntime.lib" `
	"$msvc\oldnames.lib" `
	"$kits\ucrt.lib" `
	/subsystem:console `
	"/out:$program.exe" `
	/nodefaultlib `

# cleanup
rm "$program.il.exe"
rm "$program.obj"

