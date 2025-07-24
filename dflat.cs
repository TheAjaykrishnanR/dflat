using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.CommandLine;

class Dflat
{
	static string home = new FileInfo(Environment.ProcessPath).Directory.FullName;
	static string cwd = Directory.GetCurrentDirectory();
	static string csc = Path.Join(home, @"csc\csc.exe");
	static string ilc = Path.Join(home, @"ilc\ilc.exe");
	static string linker = Path.Join(home, @"linker\lld-link.exe");
	static string aotsdk = Path.Join(home, @"libs\aotsdk");
	static string refs = Path.Join(home, @"libs\refs");
	static string runtime = Path.Join(home, @"libs\runtime");

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

		Argument<FileInfo> sourceFileArg = new("SOURCE")
		{
			Description = ".cs file to compile",
		};
		Option<bool> justILFlag = new("/il")
		{
			Description = "just compile to IL",
		};
		Option<string[]> externalLibsOption = new("/r")
		{
			Description = "additional reference .dlls or folders containing them",
		};
		Option<bool> verbosity = new("/v")
		{
			Description = "set verbosity",
		};
		RootCommand cmd = new("dflat, a native aot compiler for c#") {
			sourceFileArg,
			externalLibsOption,
			justILFlag,
			verbosity,
		};
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
			Compile(sourceFile);
		});
		cmd.Parse(args).Invoke();
	}

	static string tmpDir = Path.Join(cwd, ".dflat.tmp");
	static string program;
	static string ilexe;
	static string obj;
	static string exe;

	static void Compile(FileInfo sourceFile)
	{
		// set paths
		Directory.CreateDirectory(tmpDir);
		program = sourceFile.Name.Replace(".cs", "");
		ilexe = Path.Join(tmpDir, $"{program}.il.exe");
		obj = Path.Join(tmpDir, $"{program}.obj");
		exe = Path.Join(tmpDir, $"{program}.exe");

		if (!HandleError(CscCompile(sourceFile))) return;
		if (!HandleError(ILCompile())) return;
		if (!HandleError(Link())) return;
	}

	static bool HandleError(bool result)
	{
		//if (!result) Directory.Delete(tmpDir);
		return result;
	}

	static bool CscCompile(FileInfo sourceFile, string[] args = null)
	{
		Console.WriteLine("CSCCompile...");
		string argString = $"{sourceFile.FullName} /noconfig /out:{ilexe}";
		foreach (string dll in Directory.GetFiles(refs).Where(file => file.EndsWith(".dll")))
		{
			argString += $" /r:{new FileInfo(dll).FullName}";
		}
		foreach (string dll in externalLibs)
		{
			argString += $" /r:{dll}";
		}
		Console.WriteLine(argString);
		args = args ?? [];
		foreach (string arg in args)
		{
			argString += $" {arg}";
		}
		ProcessStartInfo psi = new()
		{
			FileName = csc,
			Arguments = argString,
		};
		Process process = new()
		{
			StartInfo = psi,
		};
		process.Start();
		process.WaitForExit();
		var exists = File.Exists(ilexe);
		Console.WriteLine($"{ilexe}: {exists}");
		return exists;
	}

	static bool ILCompile(string[] args = null)
	{
		Console.WriteLine("ILCompile...");
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
		ProcessStartInfo psi = new()
		{
			FileName = ilc,
			Arguments = argString,
		};
		Process process = new()
		{
			StartInfo = psi,
		};
		process.Start();
		process.WaitForExit();
		return File.Exists(obj);
	}

	static bool Link()
	{
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

