using Avalonia;
using System;
using Avalonia.OpenGLBackend.Extensions;

namespace OpenGL.Demo;

class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .UsePlatformRenderBackend()
            .LogToTrace();
}
