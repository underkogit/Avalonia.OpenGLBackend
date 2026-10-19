namespace Avalonia.OpenGLBackend.Extensions;

public static class AppBuilderRenderExtensions
{
    public static AppBuilder UsePlatformRenderBackend(this AppBuilder builder)
    {
        if (OperatingSystem.IsWindows())
        {
            builder.With(new Win32PlatformOptions
            {
                RenderingMode = new[] { Win32RenderingMode.Wgl }
            });
        }
        else if (OperatingSystem.IsLinux())
        {
            builder.With(new X11PlatformOptions
            {
                RenderingMode = new[] { X11RenderingMode.Glx }
            });
        }
        else if (OperatingSystem.IsMacOS())
        {
            builder.With(new AvaloniaNativePlatformOptions
            {
                RenderingMode = new[] { AvaloniaNativeRenderingMode.OpenGl }
            });
        }

        return builder;
    }
}
