using System;
using System.IO;

class _
{
	static void Main()
	{
		if (!File.Exists("./csc/csc.exe"))
		{
			throw new Exception("[ FILE NOT FOUND ]: csc.exe");
		}
	}
}
