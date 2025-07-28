using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.CommandLine;
using System.CommandLine.Help;
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
	public static string kits = Path.Join(home, @"libs\kits");
	public static string msvc = Path.Join(home, @"libs\msvc");

	static List<string> externalLibs = new();
	static List<string> cscExtraArgs = new(), ilcExtraArgs = new(), linkerExraArgs = new();

	static string NORMAL = "\x1b[39m";
	static string RED = "\x1b[91m";
	static string GREEN = "\x1b[92m";

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
		if (!Directory.Exists(kits)) throw new Exception($"{kits} not found");
		if (!Directory.Exists(msvc)) throw new Exception($"{msvc} not found");

		Argument<List<FileInfo>> sourceFilesArg = new("SOURCE FILES") { Description = ".cs files to compile", };
		Option<bool> justILFlag = new("/il") { Description = "Compile to IL", };
		Option<string[]> externalLibsOption = new("/r") { Description = "Additional reference .dlls or folders containing them", };
		Option<bool> verbosity = new("/verbosity") { Description = "Set verbosity", };
		Option<string> outputArg = new("/out") { Description = "Output file name", };
		Option<string> entryPoint = new("/main") { Description = "Specify the class containing Main()", };
		Option<bool> langversion = new("/langversion") { Description = "Print supported lang versions", };
		langversion.Action = new LangversionAction();
		Option<CSCTargets> targetsOption = new("/target") { Description = "Specify the target", };
		Option<CSCPlatforms> platformOption = new("/platform") { Description = "Specify the platform", };
		Option<bool> optimizeFlag = new("/optimize") { Description = "optimize", };
		RootCommand cmd = new("dflat, a native aot compiler for c#\nAjaykrishnan R, 2025") {
			sourceFilesArg,
			outputArg,
			entryPoint,
			externalLibsOption,
			justILFlag,
			verbosity,
			langversion,
			targetsOption,
			platformOption,
			optimizeFlag,
		};
		// override defaults
		HelpAction defaultHelpAction = null;
		for (int i = 0; i < cmd.Options.Count; i++)
		{
			if (cmd.Options[i].GetType() == typeof(VersionOption))
			{
				VersionOption vo = new("/version", []);
				vo.Action = new CustomVersionAction();
				cmd.Options[i] = vo;
			}
			if (cmd.Options[i].GetType() == typeof(HelpOption))
			{
				defaultHelpAction = (HelpAction)cmd.Options[i].Action;
				HelpOption ho = new("/h", ["/?", "/help"]);
				ho.Action = defaultHelpAction;
				cmd.Options[i] = ho;
			}
		}

		cmd.SetAction(result =>
		{
			List<FileInfo> sourceFiles = result.GetValue(sourceFilesArg);
			if (sourceFiles.Count == 0)
			{
				Console.WriteLine($"{RED}No source files supplied{NORMAL}");
				defaultHelpAction.Invoke(result);
				return;
			}
			foreach (FileInfo sourceFile in sourceFiles)
			{
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
			outputType = result.GetValue(targetsOption);
			if (result.GetValue(platformOption) != null) cscExtraArgs.Add($"/platform:{result.GetValue(platformOption).ToString()}");
			if (result.GetValue(optimizeFlag)) { cscExtraArgs.Add("/O"); ilcExtraArgs.Add("--optimize"); }
			if (result.GetValue(entryPoint) != null) { cscExtraArgs.Add($"/main:{result.GetValue(entryPoint)}"); }
			if (result.GetValue(justILFlag)) { justIL = true; }
			Compile(sourceFiles, result.GetValue(outputArg), cscExtraArgs, ilcExtraArgs);
		});

		cmd.Parse(args).Invoke();
	}

	static string tmpDir = Path.Join(cwd, ".dflat.tmp");
	static string program;
	static string ilexe;
	static string obj;
	static string exe;

	// export definitions created by ILC for linker
	static string def;

	static Stopwatch sw = new();
	static bool justIL = false;
	static CSCTargets outputType = CSCTargets.EXE;
	static void Compile(List<FileInfo> sourceFiles, string? exeOut, List<string> cscExtraArgs, List<string> ilcExtraArgs)
	{
		// set paths
		program = exeOut == null ? sourceFiles.First().Name.Replace(".cs", "") : exeOut.Replace(".exe", "");
		if (!justIL)
		{
			Directory.CreateDirectory(tmpDir);
			ilexe = Path.Join(tmpDir, $"{program}.il.exe");
			obj = Path.Join(tmpDir, $"{program}.obj");
			exe = outputType switch
			{
				CSCTargets.EXE => Path.Join(cwd, $"{program}.exe"),
				CSCTargets.LIBRARY => Path.Join(cwd, $"{program}.dll"),
			};
		}
		else
		{
			ilexe = Path.Join(cwd, $"{program}.il.exe");
		}

		if (outputType == CSCTargets.LIBRARY)
		{
			def = Path.Join(tmpDir, $"{program}.def");
			cscExtraArgs.Add($"/target:library");
			ilcExtraArgs.AddRange(["--nativelib", "--export-unmanaged-entrypoints", $"--exportsfile:{def}"]);
			linkerExraArgs.AddRange(["/dll", $"/def:{def}"]);
		}

		sw.Start();
		if (!HandleError(CscCompile(sourceFiles, cscExtraArgs))) return;
		if (justIL) { Finish(); return; }
		if (!HandleError(ILCompile(ilcExtraArgs))) return;
		if (!HandleError(Link(linkerExraArgs))) return;
		Finish();
	}

	static bool HandleError(bool result)
	{
		if (!result)
		{
			sw.Stop();
			if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
			Console.Error.WriteLine($"{RED}Compilation failed{NORMAL}");
		}
		return result;
	}

	static void Finish()
	{
		sw.Stop();
		Console.WriteLine($"{GREEN}Compilation finished in {(double)sw.ElapsedMilliseconds / 1000}s, output written to {program}.exe{NORMAL}");
		if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
	}

	static bool verbose = false;
	static void Log(string text)
	{
		if (verbose) Console.WriteLine(text);
	}

	static bool CscCompile(List<FileInfo> sourceFiles, List<string> args)
	{
		Log("CSCCompile...");
		string argString = $"/noconfig /out:{ilexe} /nologo /nostdlib /nosdkpath /unsafe";
		foreach (FileInfo sourceFile in sourceFiles)
		{
			argString += $" {sourceFile.FullName}";
		}
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
		foreach (string dll in externalLibs)
		{
			argString += $" -r:{dll}";
		}
		foreach (string arg in args)
		{
			argString += $" {arg}";
		}
		Log(argString);
		CallCompiler(ilc, argString);
		return File.Exists(obj);
	}

	static bool Link(List<string> args)
	{
		Log("Linking...");
		string argString = $"{obj} /out:{exe} /subsystem:console";
		argString += outputType switch
		{
			CSCTargets.EXE => $" {Path.Join(aotsdk, "bootstrapper.obj")}",
			CSCTargets.LIBRARY => $" {Path.Join(aotsdk, "bootstrapperdll.obj")}",
		};
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
		argString += $" {Path.Join(kits, "advapi32.lib")}";
		argString += $" {Path.Join(kits, "bcrypt.lib")}";
		argString += $" {Path.Join(kits, "crypt32.lib")}";
		argString += $" {Path.Join(kits, "iphlpapi.lib")}";
		argString += $" {Path.Join(kits, "kernel32.lib")}";
		argString += $" {Path.Join(kits, "mswsock.lib")}";
		argString += $" {Path.Join(kits, "ncrypt.lib")}";
		argString += $" {Path.Join(kits, "ntdll.lib")}";
		argString += $" {Path.Join(kits, "ole32.lib")}";
		argString += $" {Path.Join(kits, "oleaut32.lib")}";
		argString += $" {Path.Join(kits, "secur32.lib")}";
		argString += $" {Path.Join(kits, "user32.lib")}";
		argString += $" {Path.Join(kits, "uuid.lib")}";
		argString += $" {Path.Join(kits, "version.lib")}";
		argString += $" {Path.Join(kits, "ws2_32.lib")}";
		argString += $" {Path.Join(kits, "libucrt.lib")}";
		argString += $" {Path.Join(msvc, "libcmt.lib")}";
		argString += $" {Path.Join(msvc, "libcpmt.lib")}";
		argString += $" {Path.Join(msvc, "libvcruntime.lib")}";
		argString += $" {Path.Join(msvc, "oldnames.lib")}";
		argString += $" -libpath:{msvc}";
		foreach (string arg in args)
		{
			argString += $" {arg}";
		}
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
