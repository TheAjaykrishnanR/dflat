using System;
using System.IO;
using System.Diagnostics;
using System.CommandLine;

class Dflat
{
	static string csc = "./csc/csc.exe";
	static string il = "./ilc/ilc.exe";
	static string aotsdk = "./libs/aotsdk";
	static string refs = "./libs/aotsdk";
	static string runtime = "./libs/runtime";

	static void Main(string[] args)
	{
		// check compilers
		if (!File.Exists(csc)) throw new Exception($"{csc} not found");
		if (!File.Exists(il)) throw new Exception($"{il} not found");

		// check refs + runtime assemblies + aotsdk
		if (!Directory.Exists(aotsdk)) throw new Exception($"{aotsdk} not found");
		if (!Directory.Exists(refs)) throw new Exception($"{refs} not found");
		if (!Directory.Exists(runtime)) throw new Exception($"{runtime} not found");

		Argument<FileInfo> sourceFileArg = new("SOURCE")
		{
			Description = ".cs file to compile",
		};
		RootCommand cmd = new("dflat, a native aot compiler for c#") {
			sourceFileArg
		};
		ParseResult ps = cmd.Parse(args);

		ps.Invoke();
	}

	static void Compile()
	{

	}

	static void CscCompile()
	{

	}

	static void ILCompile()
	{

	}

	// Link .obj, .lib files
	static void Link()
	{

	}
}

