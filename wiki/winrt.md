# WinRT

## How to consume the WinRT API in dflat ?

You just need two dlls.

1. `Microsoft.Windows.SDK.NET.dll`
2. `WinRT.Runtime.dll`

They are part of the [Microsoft.windows.sdk.net.ref](https://www.nuget.org/packages/Microsoft.windows.sdk.net.ref)

A simple application utilizing the WinRT API (`main.cs`):

```csharp
using Windows.UI.Notifications;

class _ {
    static void Main() {

    }
}
```

Compile it using:

```
dflat main.cs /r:Microsoft.Windows.SDK.NET.dll /r:WinRT.Runtime.dll
```
