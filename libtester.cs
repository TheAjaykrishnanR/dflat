using System;
using System.Runtime.InteropServices;

class _
{
	[DllImport("kernel32")]
	static extern nint LoadLibrary(string path);

	[DllImport("kernel32")]
	static extern nint GetProcAddress(nint hModule, string procName);

	static void Main()
	{
		nint dllBase = LoadLibrary("testlib.dll");
		nint procAddress = GetProcAddress(dllBase, "Test");
		Console.WriteLine($"Test(): {procAddress}, dllBase: {dllBase}");
	}
}
