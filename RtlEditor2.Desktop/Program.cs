using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.ReactiveUI;
using Avalonia.Svg.Skia;
using CodeEditor2;
using System;
using System.Reflection.Metadata;

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

        CodeEditor2.Setups.Setup.ApplicationName = "RtlEditor";
        CodeEditor2.Setups.Setup.InitializeWindow = (window) =>
        {
            using (var stream = AssetLoader.Open(new Uri("avares://RtlEditor2.Desktop/Assets/RtlEditor.ico")))
            {
                window.Icon = new WindowIcon(stream);
            }
        };
        CodeEditor2.Setups.Setup.GetIconImage += () => { return AjkAvaloniaLibs.Libs.Icons.GetSvgBitmap("RtlEditor2.Desktop/Assets/RtlEditor.svg"); };

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
                .With(new X11PlatformOptions
                {
                    // Tooltipがメインウィンドウの上に表示されるように試みる
                    OverlayPopups = true
                })
                .WithInterFont()
                .LogToTrace()
                .UseReactiveUI();
    }
}
