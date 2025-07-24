using System;
using System.IO;

class _
{
	static void Main()
	{
		// check compilers
		if (!File.Exists("./csc/csc.exe")) throw new Exception("./csc/csc.exe not found");
		if (!File.Exists("./ilc/ilc.exe")) throw new Exception("./ilc/ilc.exe not found");

		// check refs + runtime assemblies + aotsdk
	}
}
