using System.Runtime.Versioning;
using Uno.UI.Hosting;

namespace Deskband11Lib.Uno.Skia.Sample;

[SupportedOSPlatform("windows10.0.22000.0")]
internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseWin32()
            .Build();

        host.Run();
    }
}
