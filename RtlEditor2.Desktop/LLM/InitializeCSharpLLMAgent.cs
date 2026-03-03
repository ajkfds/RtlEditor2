using Avalonia.Platform;
using pluginVerilog.LLM.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RtlEditor2.Desktop.LLM
{
    public class InitializeCSharpLLMAgent
    {
        public static void Run(CodeEditor2.Data.Project project, CodeEditor2.LLM.LLMAgent agent, bool useFunctioncallApi)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            agent.PersudoFunctionCallMode = true;


            StringBuilder sb = new StringBuilder();

            sb.Append(getAssetString("avares://CodeEditor2VerilogPlugin/Assets/LLMPrompt/AgentBasePrompt.md"));
            if (useFunctioncallApi)
            {
                sb.Append("avares://CodeEditor2VerilogPlugin/Assets/LLMPrompt/ToolCall.md");
            }

            sb.Append(getAssetString("avares://CodeEditor2VerilogPlugin/Assets/LLMPrompt/RtlEditSkill.md"));

            sb.Append(getAssetString("avares://CodeEditor2/Assets/LLMPrompt/CheckAgentMd.md"));
            //sb.Append("""

            //    ===
            //    まず上記の指示を理解し、ユーザのタスク指示に備えて待機してください。

            //    """);


            agent.BasePrompt = sb.ToString();

            agent.PromptParameters.Add("Role", "a highly skilled software engineer with extensive knowledge in many programming languages, frameworks, design patterns, and best practices");
            // agent functions

            CodeEditor2.LLM.Tools.ReadFile readFile = new CodeEditor2.LLM.Tools.ReadFile(project);
            agent.Tools.Add(readFile.GetAIFunction());

            CodeEditor2.LLM.Tools.ReplaceInFile replaceInFile = new CodeEditor2.LLM.Tools.ReplaceInFile(project);
            agent.Tools.Add(replaceInFile.GetAIFunction());

            CodeEditor2.LLM.Tools.SearchFiles searchFiles = new CodeEditor2.LLM.Tools.SearchFiles(project);
            agent.Tools.Add(searchFiles.GetAIFunction());

            CodeEditor2.LLM.Tools.WriteToFile writeToFile = new CodeEditor2.LLM.Tools.WriteToFile(project);
            agent.Tools.Add(writeToFile.GetAIFunction());

            CodeEditor2.LLM.Tools.ListFiles listFiles = new CodeEditor2.LLM.Tools.ListFiles(project);
            agent.Tools.Add(listFiles.GetAIFunction());

            CodeEditor2.LLM.Tools.BuildDotNet buildDotNet = new CodeEditor2.LLM.Tools.BuildDotNet(project);
            agent.Tools.Add(buildDotNet.GetAIFunction());

            CodeEditor2.LLM.Tools.DocExtractor.LibraryPath = new Dictionary<string, string>()
            {
                { "Terminal.Gui", @"C:\Users\tomok\.nuget\packages\terminal.gui\2.0.0-develop.5097\lib\net10.0\Terminal.Gui.dll" },
                { "Avalonia",@"C:\Users\tomok\.nuget\packages\avalonia\11.2.3\Avalonia.dll" }
            };

            CodeEditor2.LLM.Tools.GetLibDefinition getLibDefinition = new CodeEditor2.LLM.Tools.GetLibDefinition(project);
            agent.Tools.Add(getLibDefinition.GetAIFunction());

            //GetBuildingBlockDefinedFilePath getBuildingBlockDefinedFilePath = new GetBuildingBlockDefinedFilePath(project);
            //agent.Tools.Add(getBuildingBlockDefinedFilePath.GetAIFunction());

            //pluginVerilog.LLM.Tools.RunVerilogSimulation runVerilogSimulation = new pluginVerilog.LLM.Tools.RunVerilogSimulation(project, new pluginIcarusVerilog.Simulation.IcarusVerilogSimulation());
            //agent.Tools.Add(runVerilogSimulation.GetAIFunction());

        }

        private static string getAssetString(string assetPath)
        {
            using (var stream = AssetLoader.Open(new Uri(assetPath)))
            {
                byte[] buffer = new byte[stream.Length];
                stream.Read(buffer, 0, (int)stream.Length);
                var encoding = Encoding.GetEncoding("UTF-8");
                return encoding.GetString(buffer);
            }
        }
    }
}
