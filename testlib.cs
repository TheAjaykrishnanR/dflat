using System;
using System.Runtime.InteropServices;

class _
{
	[UnmanagedCallersOnly(EntryPoint = "Test")]
	public static void Test()
	{
		Console.WriteLine("Hello from native lib");
	}
}
