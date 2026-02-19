using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.ReactiveUI;
using Avalonia.Svg.Skia;
using CodeEditor2;
using CodeEditor2.LLM;
using pluginAi;
using pluginVerilog.LLM;
using System;
using System.Reflection.Metadata;
using static CodeEditor2.Controller;

namespace RtlEditor2.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        CodeEditor2.Setups.Setup.ApplicationName = "RtlEditor";
        CodeEditor2.Setups.Setup.GetIconImage += () => { return AjkAvaloniaLibs.Libs.Icons.GetSvgBitmap("RtlEditor2.Desktop/Assets/RtlEditor.svg"); };
        CodeEditor2.Setups.Setup.InitializeWindow = (window) =>
        {
            using (var stream = AssetLoader.Open(new Uri("avares://RtlEditor2.Desktop/Assets/RtlEditor.ico")))
            {
                window.Icon = new WindowIcon(stream);
            }

        };
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
            pluginAi.Snippet.CleanEnglishSnippet.GetLLM = () =>
            {
                //return new pluginAi.LLMChat(new pluginAi.OpenRouterChat(pluginAi.OpenRouterModels.openai_gpt_oss_20b, false));
                return new pluginAi.LLMChat(new pluginAi.OpenRouterChat(pluginAi.OpenRouterModels.openai_gpt_oss_120b, false));
                //return new pluginAi.LLMChat(new pluginAi.OpenRouterChat(pluginAi.OpenRouterModels.openai_gpt_5_1_codex_mini , false));
                //                return new pluginAi.LLMChat(new pluginAi.OpenRouterChat(pluginAi.OpenRouterModels.google_gemini_3_pro_preview, false));
            };
            using (System.IO.StreamReader sw = new System.IO.StreamReader(@"C:\ApiKey\openrouter.txt"))
            {
                string apiKey = sw.ReadToEnd().Trim();
                if (apiKey == "") throw new Exception();
                pluginAi.OpenRouterChat.ApiKey = apiKey;
            }

            var plugin = new pluginAi.Plugin();
            Global.Plugins.Add(plugin.Id, plugin);
        }


        // DrawIo
        {
            var plugin = new pluginDrawIo.Plugin();
            Global.Plugins.Add(plugin.Id, plugin);
        }

        CodeEditor2.NavigatePanel.ProjectNode.CustomizeSpecificNodeContextMenu += ((m) => {
            ContextMenu menu = m;
            MenuItem menuItem_Agent = CodeEditor2.Global.CreateMenuItem(
                "LLM Agent", "menuItem_Agent",
                "CodeEditor2/Assets/Icons/ai.svg",
                Avalonia.Media.Colors.YellowGreen
                );
            menu.Items.Add(menuItem_Agent);
            menuItem_Agent.Click += MenuItem_Agent_Click;
        });

        BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);


    }
    public static void CustomizeNavigateNodeContextMenuHandler(Avalonia.Controls.ContextMenu contextMenu)
    {
        Avalonia.Media.Color themeColor = Avalonia.Media.Colors.YellowGreen;

        MenuItem menuItem_LLM = CodeEditor2.Global.CreateMenuItem(
            "LLM", "menuItem_LLM",
            "CodeEditor2/Assets/Icons/ai.svg",
             themeColor);
        contextMenu.Items.Add(menuItem_LLM);

        MenuItem menuItem_Agent = CodeEditor2.Global.CreateMenuItem(
            "LLM Agent", "menuItem_Agent",
            "CodeEditor2/Assets/Icons/ai.svg",
            Avalonia.Media.Colors.YellowGreen
            );
        menuItem_LLM.Items.Add(menuItem_Agent);
        menuItem_Agent.Click += MenuItem_Agent_Click;
    }


    private static void MenuItem_Agent_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // chat agent tab
        pluginAi.OpenRouterChat chat = new OpenRouterChat(OpenRouterModels.deepseek_deepseek_v3_2, false);
        CodeEditor2.NavigatePanel.NavigatePanelNode? node = CodeEditor2.Controller.NavigatePanel.GetSelectedNode();
        if (node == null) return;

        LLMAgent agent = new LLMAgent(node.GetProject());
        agent.Show();
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
                    // Tooltipがメインウィンドウの上に表示するworkaround
                    OverlayPopups = true
                })
                .WithInterFont()
                .LogToTrace()
                .UseReactiveUI();
    }
}
