using System;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using Avalonia.Svg.Skia;
using CodeEditor2;

namespace RtlEditor2.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        {
            var plugin = new pluginMarkdown.Plugin();
            Global.Plugins.Add(plugin.Id, plugin);
        }
        {
            var plugin = new pluginVerilog.Plugin();
            Global.Plugins.Add(plugin.Id, plugin);
        }



        // Windows

        {
            var plugin = new pluginIcarusVerilog.Plugin();
            Global.Plugins.Add(plugin.Id, plugin);
        }

        {
            var plugin = new pluginVivado.Plugin();
            Global.Plugins.Add(plugin.Id, plugin);
        }

        // AI
        {
            var plugin = new pluginAi.Plugin();
            Global.Plugins.Add(plugin.Id, plugin);
        }


        // DrawIo
        {
            var plugin = new pluginDrawIo.Plugin();
            Global.Plugins.Add(plugin.Id, plugin);
        }

        // Linux




        BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);
    }

    //public static void Main(string[] args) => BuildAvaloniaApp()
    //    .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    //public static AppBuilder BuildAvaloniaApp()
    //    => AppBuilder.Configure<App>()
    //        .UsePlatformDetect()
    //        .WithInterFont()
    //        .LogToTrace()
    //        .UseReactiveUI();
    public static AppBuilder BuildAvaloniaApp()
    {
        GC.KeepAlive(typeof(SvgImageExtension).Assembly);
        GC.KeepAlive(typeof(Avalonia.Svg.Skia.Svg).Assembly);
        return AppBuilder.Configure<CodeEditor2.App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace()
                .UseReactiveUI();
    }
}
