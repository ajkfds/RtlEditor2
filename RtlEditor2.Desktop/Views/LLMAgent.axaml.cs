using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CodeEditor2.LLM;
using pluginAi;
using pluginVerilog.LLM;
using System.Security.Cryptography.X509Certificates;

namespace RtlEditor2.Desktop;

public partial class LLMAgent : Window
{
    public LLMAgent(CodeEditor2.Data.Project project)
    {
        InitializeComponent();

        bool useFunctionCallApi = false;

        // chat agent tab
        pluginAi.OpenRouterChat chat = new OpenRouterChat(OpenRouterModels.deepseek_deepseek_v3_2, useFunctionCallApi);

        CodeEditor2.LLM.LLMAgent agent = new CodeEditor2.LLM.LLMAgent();
        LLM.InitializeLLMAgent.Run(project, agent, useFunctionCallApi);

        ChatControl.SetModel(chat, agent);
        Content = ChatControl;
    }

    private ChatControl ChatControl = new ChatControl();
}