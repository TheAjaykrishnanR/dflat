using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.CommandLine;
using System.CommandLine.Invocation;

#nullable enable

class Dflat
{
	public static string home = new FileInfo(Environment.ProcessPath).Directory.FullName;
	public static string cwd = Directory.GetCurrentDirectory();
	public static string csc = Path.Join(home, @"csc\csc.exe");
	public static string ilc = Path.Join(home, @"ilc\ilc.exe");
	public static string linker = Path.Join(home, @"linker\lld-link.exe");
	public static string aotsdk = Path.Join(home, @"libs\aotsdk");
	public static string refs = Path.Join(home, @"libs\refs");
	public static string runtime = Path.Join(home, @"libs\runtime");

	static List<string> externalLibs = new();

	static void Main(string[] args)
	{
		// check compilers
		if (!File.Exists(csc)) throw new Exception($"{csc} not found");
		if (!File.Exists(ilc)) throw new Exception($"{ilc} not found");
		if (!File.Exists(linker)) throw new Exception($"{linker} not found");

		// check refs + runtime assemblies + aotsdk
		if (!Directory.Exists(aotsdk)) throw new Exception($"{aotsdk} not found");
		if (!Directory.Exists(refs)) throw new Exception($"{refs} not found");
		if (!Directory.Exists(runtime)) throw new Exception($"{runtime} not found");

		Argument<FileInfo> sourceFileArg = new("SOURCE") { Description = ".cs file to compile", };
		Option<bool> justILFlag = new("/il") { Description = "Compile to IL", };
		Option<string[]> externalLibsOption = new("/r") { Description = "Additional reference .dlls or folders containing them", };
		Option<bool> verbosity = new("/verbosity") { Description = "Set verbosity", };
		Option<string> outputArg = new("/out") { Description = "Output file name", };
		Option<bool> langversion = new("/langversion") { Description = "Print supported lang versions", };
		langversion.Action = new LangversionAction();
		Option<CSCTargets> targetsOption = new("/target") { Description = "Specify the target", };
		Option<CSCPlatforms> platformOption = new("/platform") { Description = "Specify the platform", };
		Option<bool> optimizeFlag = new("/optimize") { Description = "optimize", };
		RootCommand cmd = new("dflat, a native aot compiler for c#") {
			sourceFileArg,
			outputArg,
			externalLibsOption,
			justILFlag,
			verbosity,
			langversion,
			targetsOption,
			platformOption,
			optimizeFlag,
		};
		// override defaults
		for (int i = 0; i < cmd.Options.Count; i++)
		{
			if (cmd.Options[i].GetType() == typeof(VersionOption))
			{
				VersionOption vo = new("/version", []);
				vo.Action = new CustomVersionAction();
				cmd.Options[i] = vo;
			}
		}
		cmd.SetAction(result =>
		{
			FileInfo sourceFile = result.GetValue(sourceFileArg);
			if (!sourceFile.Exists)
			{
				Console.Error.WriteLine($"file {sourceFile.Name} does not exist");
				return;
			}
			if (!sourceFile.Name.EndsWith(".cs"))
			{
				Console.Error.WriteLine($"please input a .cs file");
				return;
			}
			if (result.GetValue(verbosity)) { verbose = true; }
			foreach (string path in result.GetValue(externalLibsOption))
			{
				if (!File.Exists(path))
				{
					Console.Error.WriteLine($"{path} does not exist");
					return;
				}
				if (File.GetAttributes(path).HasFlag(FileAttributes.Directory))
				{
					foreach (string dll in Directory.GetFiles(path).Where(file => file.EndsWith(".dll")))
					{
						externalLibs.Add(Path.Join(path, dll));
					}
					continue;
				}
				externalLibs.Add(new FileInfo(path).FullName);
			}
			List<string> cscExtraArgs = new(), ilcExtraArgs = new();
			if (result.GetValue(targetsOption) != null) cscExtraArgs.Add($"/target:{result.GetValue(targetsOption).ToString()}");
			if (result.GetValue(platformOption) != null) cscExtraArgs.Add($"/platform:{result.GetValue(platformOption).ToString()}");
			if (result.GetValue(optimizeFlag)) { cscExtraArgs.Add("/O"); ilcExtraArgs.Add("--optimize"); }
			Compile(sourceFile, result.GetValue(outputArg), cscExtraArgs, ilcExtraArgs);
		});

		cmd.Parse(args).Invoke();
	}

	static string tmpDir = Path.Join(cwd, ".dflat.tmp");
	static string program;
	static string ilexe;
	static string obj;
	static string exe;

	static void Compile(FileInfo sourceFile, string? exeOut, List<string> cscExtraArgs, List<string> ilcExtraArgs)
	{
		// set paths
		Directory.CreateDirectory(tmpDir);
		program = exeOut == null ? sourceFile.Name.Replace(".cs", "") : exeOut.Replace(".exe", "");
		ilexe = Path.Join(tmpDir, $"{program}.il.exe");
		obj = Path.Join(tmpDir, $"{program}.obj");
		exe = Path.Join(cwd, $"{program}.exe");

		if (!HandleError(CscCompile(sourceFile, cscExtraArgs))) return;
		if (!HandleError(ILCompile(ilcExtraArgs))) return;
		if (!HandleError(Link())) return;

		Directory.Delete(tmpDir, recursive: true);
	}

	static bool HandleError(bool result)
	{
		if (!result) Directory.Delete(tmpDir, recursive: true);
		return result;
	}

	static bool verbose = false;
	static void Log(string text)
	{
		if (verbose) Console.WriteLine(text);
	}

	static bool CscCompile(FileInfo sourceFile, List<string> args)
	{
		Log("CSCCompile...");
		string argString = $"{sourceFile.FullName} /noconfig /out:{ilexe} /nologo /nostdlib /nosdkpath /unsafe";
		foreach (string dll in Directory.GetFiles(refs).Where(file => file.EndsWith(".dll")))
		{
			argString += $" /r:{new FileInfo(dll).FullName}";
		}
		foreach (string dll in externalLibs)
		{
			argString += $" /r:{dll}";
		}
		foreach (string arg in args)
		{
			argString += $" {arg}";
		}
		Log(argString);
		CallCompiler(csc, argString);
		var exists = File.Exists(ilexe);
		return exists;
	}

	public static void CallCompiler(string compiler, string argString)
	{
		ProcessStartInfo psi = new()
		{
			FileName = compiler,
			Arguments = argString,
		};
		Process process = new()
		{
			StartInfo = psi,
		};
		process.Start();
		process.WaitForExit();
	}

	static bool ILCompile(List<string> args)
	{
		Log("ILCompile...");
		string argString = $"{ilexe} --out:{obj}";
		argString += $" -r:{Path.Join(aotsdk, "*.dll")}";
		argString += $" -r:{Path.Join(runtime, "*.dll")}";
		foreach (string dll in externalLibs)
		{
			argString += $" -r:{dll}";
		}
		argString += $" -g";
		argString += $" --generateunmanagedentrypoints:System.Private.CoreLib,HIDDEN";
		argString += $" --dehydrate";
		argString += $" --initassembly:System.Private.CoreLib";
		argString += $" --initassembly:System.Private.StackTraceMetadata";
		argString += $" --initassembly:System.Private.TypeLoader";
		argString += $" --initassembly:System.Private.Reflection.Execution";
		argString += $" --stacktracedata";
		argString += $" --scanreflection";
		argString += $" --feature:System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization=false";
		argString += $" --feature:System.Diagnostics.Tracing.EventSource.IsSupported=false";
		argString += $" --feature:System.Resources.ResourceManager.AllowCustomResourceTypes=false";
		argString += $" --feature:System.Linq.Expressions.CanEmitObjectArrayDelegate=false";
		argString += $" --feature:System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported=false";
		argString += $" --feature:System.Globalization.Invariant=true";
		argString += $" --feature:System.Diagnostics.Debugger.IsSupported=false";
		argString += $" --feature:System.StartupHookProvider.IsSupported=false";
		argString += $" --directpinvokelist:{Path.Join(home, @"libs\WindowsAPIs.txt")}";
		argString += $" --directpinvoke:System.Globalization.Native";
		argString += $" --directpinvoke:System.IO.Compression.Native";
		Log(argString);
		CallCompiler(ilc, argString);
		return File.Exists(obj);
	}

	static bool Link()
	{
		Log("Linking...");
		string argString = $"{obj} /out:{exe} /subsystem:console";
		argString += $" {Path.Join(aotsdk, "bootstrapper.obj")}";
		argString += $" {Path.Join(aotsdk, "dllmain.obj")}";
		argString += $" {Path.Join(aotsdk, "Runtime.ServerGC.lib")}";
		argString += $" {Path.Join(aotsdk, "standalonegc-disabled.lib")}";
		argString += $" {Path.Join(aotsdk, "aotminipal.lib")}";
		argString += $" {Path.Join(aotsdk, "brotlicommon.lib")}";
		argString += $" {Path.Join(aotsdk, "eventpipe-enabled.lib")}";
		argString += $" {Path.Join(aotsdk, "Runtime.WorkstationGC.lib")}";
		argString += $" {Path.Join(aotsdk, "brotlidec.lib")}";
		argString += $" {Path.Join(aotsdk, "brotlienc.lib")}";
		argString += $" {Path.Join(aotsdk, "Runtime.VxsortEnabled.lib")}";
		argString += $" {Path.Join(aotsdk, "System.Globalization.Native.Aot.lib")}";
		argString += $" {Path.Join(aotsdk, "System.IO.Compression.Native.Aot.lib")}";
		argString += $" {Path.Join(aotsdk, "zlibstatic.lib")}";
		argString += $" advapi32.lib";
		argString += $" ole32.lib";
		argString += $" bcrypt.lib";
		argString += $" user32.lib";
		argString += $" kernel32.lib";
		argString += $" version.lib";
		Log(argString);
		ProcessStartInfo psi = new()
		{
			FileName = linker,
			Arguments = argString,
		};
		Process process = new()
		{
			StartInfo = psi
		};
		process.Start();
		process.WaitForExit();
		return File.Exists(exe);
	}
}

class CustomVersionAction : SynchronousCommandLineAction
{
	public override int Invoke(ParseResult ps)
	{
		Console.Write($"CSC: ");
		Dflat.CallCompiler(Dflat.csc, "/version");
		Console.Write($"ILC: ");
		Dflat.CallCompiler(Dflat.ilc, "--version");
		Console.WriteLine($"Runtime: {FileVersionInfo.GetVersionInfo(Path.Join(Dflat.runtime, "System.dll")).ProductVersion}");
		return 0;
	}
}

class LangversionAction : SynchronousCommandLineAction
{
	public override int Invoke(ParseResult ps)
	{
		Dflat.CallCompiler(Dflat.csc, "/langversion:?");
		return 0;
	}
}

enum CSCTargets
{
	EXE,
	WINEXE,
	MODULE,
	LIBRARY,
	APPCONTAINEREXE
}

enum CSCPlatforms
{
	x86,
	Itamium,
	x64,
	arm,
	arm64,
	anycpu32bitpreferred,
	anycpu
}
