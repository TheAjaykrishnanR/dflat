# Run from Visual Studio Developer Console

# cleanup
rm *exe
rm *.obj

$csFile = $args[0]
$program = $csFile.Replace(".cs", "")
$cwd = $(Get-Location).Path

# .cs -> IL [csc.exe]
$refFolder = ".\libs\microsoft.netcore.app.ref\ref\net10.0"
$refs = @()
foreach($dll in Get-ChildItem $refFolder | Where-Object -Property Name -Like "*dll") 
{
	$refs += "/r:$($dll.FullName) "
}
$ilexe = "$program.il.exe";
.\csc\csc $csFile @refs "/out:$ilexe"

# IL -> Bytecode [ILCompiler dotnet\runtime\artifacts\bin\coreclr\windows.x64.Release\x64\ilc\ilc.exe]
$aotsdk = "$cwd\libs\aotsdk"
$obj = "$program.obj"
.\ilc\ilc.exe `
	$ilexe `
	--out $obj `
	-r:"$aotsdk\*.dll" `
	-r:"$cwd\libs\microsoft.netcore.app.runtime.win-x64\Release\runtimes\win-x64\lib\net10.0\*.dll" `
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
	--directpinvokelist:C:\Users\Jayakuttan\dev\dotnet\runtime\artifacts\bin\coreclr\windows.x64.Release\build\WindowsAPIs.txt `
	--directpinvoke:System.Globalization.Native `
	--directpinvoke:System.IO.Compression.Native `

# Linking
link $obj `
	"${aotsdk}\bootstrapper.obj" `
	"${aotsdk}\dllmain.obj" `
	"${aotsdk}\Runtime.ServerGC.lib" `
	"${aotsdk}\standalonegc-disabled.lib" `
	"${aotsdk}\aotminipal.lib" `
	"${aotsdk}\brotlicommon.lib" `
	"${aotsdk}\eventpipe-enabled.lib" `
	"${aotsdk}\Runtime.WorkstationGC.lib" `
	"${aotsdk}\brotlidec.lib" `
	"${aotsdk}\brotlienc.lib" `
	"${aotsdk}\Runtime.VxsortEnabled.lib" `
	"${aotsdk}\System.Globalization.Native.Aot.lib" `
	"${aotsdk}\System.IO.Compression.Native.Aot.lib" `
	"${aotsdk}\zlibstatic.lib" `
	advapi32.lib `
	ole32.lib `
	bcrypt.lib `
	/subsystem:console `
	"/out:$program.exe" `
