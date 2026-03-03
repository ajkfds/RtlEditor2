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

public partial class LLMAgent : Window
{
    public LLMAgent(CodeEditor2.Data.Project project,Action<CodeEditor2.LLM.LLMAgent> initialize)
    {
        InitializeComponent();

        bool useFunctionCallApi = false;

        // chat agent tab
        pluginAi.OpenRouterChat chat = new OpenRouterChat(OpenRouterModels.deepseek_deepseek_v3_2, useFunctionCallApi);

        CodeEditor2.LLM.LLMAgent agent = new CodeEditor2.LLM.LLMAgent();
        initialize(agent);
//        LLM.InitializeVerilogLLMAgent.Run(project, agent, useFunctionCallApi);

        ChatControl.SetModel(chat, agent);
        Content = ChatControl;
    }

    private ChatControl ChatControl = new ChatControl();
}