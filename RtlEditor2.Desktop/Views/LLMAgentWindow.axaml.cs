using Avalonia.Controls;
using CodeEditor2.LLM;
using System;

namespace RtlEditor2.Desktop;

public partial class LLMAgentWindow : Window
{
    public LLMAgentWindow(CodeEditor2.Data.Project project, pluginAi.OpenRouterChat openRouterChat, Action<CodeEditor2.LLM.LLMAgent> initialize)
    {
        InitializeComponent();


        CodeEditor2.LLM.LLMAgent agent = new CodeEditor2.LLM.LLMAgent() { UseToolCallId = true };
        initialize(agent);
        //        LLM.InitializeVerilogLLMAgent.Run(project, agent, useFunctionCallApi);

        ChatControl.SetModel(openRouterChat, agent);
        Content = ChatControl;

        // ウィンドウが開いた後にフォーカスを当てる
        Opened += (s, e) =>
        {
            ChatControl.FocusInput();
        };

        // ウィンドウがアクティブになった際、IMEのフォーカス外れを防ぐ
        Activated += (s, e) =>
        {
            ChatControl.FocusInput();
        };
    }

    private ChatControl ChatControl = new ChatControl();
}
