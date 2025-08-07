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
	public static string csc = Path.Join(home, @"csc/csc");
	public static string ilc = Path.Join(home, @"ilc/ilc");
	public static string linker = @"/usr/bin/ld.bfd";
	public static string aotsdk = Path.Join(home, @"libs/aotsdk");
	public static string refs = Path.Join(home, @"libs/refs");
	public static string runtime = Path.Join(home, @"libs/runtime");
	public static string kits = Path.Join(home, @"libs/kits");

	static List<string> externalLibs = new();
	static List<string> cscExtraArgs = new(), ilcExtraArgs = new(), linkerExraArgs = new();

	static string NORMAL = "\x1b[39m";
	static string RED = "\x1b[91m";
	static string GREEN = "\x1b[92m";

	static void Main(string[] args)
	{
		// check compilers
		if (!File.Exists(csc)) { Console.WriteLine($"{csc} not found"); return; }
		if (!File.Exists(ilc)) { Console.WriteLine($"{ilc} not found"); return; }
		if (!File.Exists(linker)) { Console.WriteLine($"{linker} not found"); return; }

		// check refs + runtime assemblies + aotsdk
		if (!Directory.Exists(aotsdk)) { Console.WriteLine($"{aotsdk} not found"); return; }
		if (!Directory.Exists(refs)) { Console.WriteLine($"{refs} not found"); return; }
		if (!Directory.Exists(runtime)) { Console.WriteLine($"{runtime} not found"); return; }
		if (!Directory.Exists(kits)){ Console.WriteLine($"{kits} not found"); return; } 

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
			ilcExtraArgs.AddRange(["--nativlib", "--export-unmanaged-entrypoints", $"--exportsfile:{def}"]);
			linkerExraArgs.AddRange(["/dll", $"/def:{def}", "/noimplib"]);
		}
		else if (outputType == CSCTargets.WINEXE) { cscExtraArgs.Add($"/target:winexe"); }

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
		argString += $" --generateunmanagedentrypoints:System.Private.CoreLib";
		argString += $" --dehydrate";
		argString += $" --initassembly:System.Private.CoreLib";
		argString += $" --initassembly:System.Private.StackTraceMetadata";
		argString += $" --initassembly:System.Private.TypeLoader";
		argString += $" --initassembly:System.Private.Reflection.Execution";
		argString += $" --directpinvoke:libSystem.Native";
		argString += $" --directpinvoke:libSystem.Globalization.Native";
		argString += $" --directpinvoke:libSystem.IO.Compression.Native";
		argString += $" --directpinvoke:libSystem.Net.Security.Native";
		argString += $" --directpinvoke:libSystem.Security.Cryptography.Native.OpenSsl";
		argString += $" --jitpath:/home/jayakuttan/dflat/ilc/libclrjit_unix_x64_x64.so";
		argString += $" --stacktracedata";
		argString += $" --scanreflection";
		//
		argString += $" --feature:System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization=false";
		argString += $" --feature:System.Diagnostics.Tracing.EventSource.IsSupported=false";
		argString += $" --feature:System.Resources.ResourceManager.AllowCustomResourceTypes=false";
		argString += $" --feature:System.Linq.Expressions.CanEmitObjectArrayDelegate=false";
		argString += $" --feature:System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported=false";
		argString += $" --feature:System.Globalization.Invariant=true";
		argString += $" --feature:System.Diagnostics.Debugger.IsSupported=false";
		argString += $" --feature:System.StartupHookProvider.IsSupported=false";
		
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
	
	static string glibc = @"/lib/x86_64-linux-gnu";
	static string gcc = @"/usr/lib/gcc/x86_64-linux-gnu";
	static int GetGccVer() {
		return Directory.GetDirectories(gcc).ToList().Select(folder => Convert.ToInt32(new DirectoryInfo(folder).Name)).Max();
	}
	static string gcclibs = Path.Join(gcc, GetGccVer().ToString());

	static bool Link(List<string> args)
	{
		Log("Linking...");
		string argString = "";
		
		argString += $" {obj}";
		argString += outputType switch
		{
			CSCTargets.EXE => $" {Path.Join(aotsdk, "libbootstrapper.o")}",
			CSCTargets.LIBRARY => $" {Path.Join(aotsdk, "libbootstrapperdll.o")}",
		};
		argString += $" {Path.Join(aotsdk, "libRuntime.WorkstationGC.a")}";
		argString += $" {Path.Join(aotsdk, "libeventpipe-disabled.a")}";
		argString += $" {Path.Join(aotsdk, "libRuntime.VxsortEnabled.a")}";
		argString += $" {Path.Join(aotsdk, "libstandalonegc-disabled.a")}";
		argString += $" {Path.Join(aotsdk, "libstdc++compat.a")}";
		argString += $" {Path.Join(aotsdk, "libSystem.Native.a")}";
		argString += $" {Path.Join(aotsdk, "libSystem.Globalization.Native.a")}";
		argString += $" {Path.Join(aotsdk, "libSystem.IO.Compression.Native.a")}";
		argString += $" {Path.Join(aotsdk, "libSystem.Net.Security.Native.a")}";
		argString += $" {Path.Join(aotsdk, "libSystem.Security.Cryptography.Native.OpenSsl.a")}";
		argString += $" {Path.Join(aotsdk, "libz.a")}";
		argString += $" {Path.Join(aotsdk, "libaotminipal.a")}";
		argString += $" {Path.Join(aotsdk, "libbrotlicommon.a")}";
		argString += $" {Path.Join(aotsdk, "libbrotlidec.a")}";
		argString += $" {Path.Join(aotsdk, "libbrotlienc.a")}";
		argString += $" {Path.Join(glibc, "crt1.o")}";
		argString += $" {Path.Join(glibc, "crti.o")}";
		argString += $" {Path.Join(glibc, "crtn.o")}";
		argString += $" {Path.Join(glibc, "libc.so")}";
		argString += $" {Path.Join(glibc, "libc.so.6")}";
		argString += $" {Path.Join(glibc, "libpthread.so.0")}";
		argString += $" {Path.Join(glibc, "libm.so")}";
		argString += $" {Path.Join(glibc, "libm.so.6")}";
		argString += $" {Path.Join(glibc, "libdl.so.2")}";
		argString += $" {Path.Join(glibc, "librt.so.1")}";
		argString += $" {Path.Join(gcclibs, "crtbeginS.o")}";
		argString += $" {Path.Join(gcclibs, "crtendS.o")}";
		argString += $" --output={exe}";
		argString += $" --dynamic-linker=/lib64/ld-linux-x86-64.so.2";
		argString += $" --nostdlib";
		argString += $" --compress-debug-sections=zlib";
		argString += $" --discard-all";
		argString += $" --gc-sections";
		argString += $" --as-needed";
		argString += $" --strip-all";
		argString += $" -pie";

		argString += $" -z relro";
		argString += $" -z now";
		argString += $" --eh-frame-hdr";
		argString += $" --export-dynamic";

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
	LIBRARY,
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
