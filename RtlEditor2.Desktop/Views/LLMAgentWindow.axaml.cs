using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CodeEditor2.Data;
using CodeEditor2.LLM;
using pluginAi;
using pluginVerilog.LLM;
using System;
using System.Security.Cryptography.X509Certificates;

namespace RtlEditor2.Desktop;

public partial class LLMAgentWindow : Window
{
    public LLMAgentWindow(CodeEditor2.Data.Project project,pluginAi.OpenRouterChat openRouterChat,Action<CodeEditor2.LLM.LLMAgent> initialize)
    {
        InitializeComponent();


        CodeEditor2.LLM.LLMAgent agent = new CodeEditor2.LLM.LLMAgent();
        initialize(agent);
//        LLM.InitializeVerilogLLMAgent.Run(project, agent, useFunctionCallApi);

        ChatControl.SetModel(openRouterChat, agent);
        Content = ChatControl;
    }

    private ChatControl ChatControl = new ChatControl();
}
