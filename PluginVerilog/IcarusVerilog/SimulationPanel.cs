using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ajkControls.Primitive;
using pluginVerilog.Verilog.BuildingBlocks;

namespace pluginVerilog.IcarusVerilog
{


    public partial class SimulationPanel : UserControl
    {
        public Action<IconImage, IconImage.ColorStyle> RequestTabIconChange;

        [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern uint GetShortPathName( [System.Runtime.InteropServices.MarshalAs(
            System.Runtime.InteropServices.UnmanagedType.LPTStr)]
            string lpszLongPath,
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)] System.Text.StringBuilder lpszShortPath,
            uint cchBuffer);

        public string getShortPath(string path)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(1024);
            uint ret = GetShortPathName(path, sb, (uint)sb.Capacity);
            if (ret == 0) return path;
            return sb.ToString();
        }


        public SimulationPanel(Data.VerilogFile topFile)
        {
            InitializeComponent();
            Disposed += disposed;
            thread = new System.Threading.Thread(run);
            thread.Start();
        }

        private int iconCount = 0;
        IconImage.ColorStyle iconColor = IconImage.ColorStyle.White;
        private void moveTabIcon()
        {
            iconCount++;
            if (iconCount > 5) iconCount = 0;

            if (RequestTabIconChange != null)
            {
                switch (iconCount)
                {
                    case 0:
                        RequestTabIconChange(codeEditor.Global.IconImages.Wave0, iconColor);
                        break;
                    case 1:
                        RequestTabIconChange(codeEditor.Global.IconImages.Wave1, iconColor);
                        break;
                    case 2:
                        RequestTabIconChange(codeEditor.Global.IconImages.Wave2, iconColor);
                        break;
                    case 3:
                        RequestTabIconChange(codeEditor.Global.IconImages.Wave3, iconColor);
                        break;
                    case 4:
                        RequestTabIconChange(codeEditor.Global.IconImages.Wave4, iconColor);
                        break;
                    case 5:
                        RequestTabIconChange(codeEditor.Global.IconImages.Wave5, iconColor);
                        break;
                }
            }
        }

        private void receiveLineString(string lineString)
        {
            if (lineString == "icarusVerilogShell>")
            {
                iconColor = IconImage.ColorStyle.Green;
                logView.AppendLogLine(lineString,System.Drawing.Color.ForestGreen);
            }
            else
            {
                iconColor = IconImage.ColorStyle.White;
                logView.AppendLogLine(lineString);
            }
            moveTabIcon();
        }

        volatile bool abort = false;
        private void disposed(object sender,EventArgs e)
        {
            abort = true;
            RequestTabIconChange = null;
            shell.Dispose();
        }

        ajkControls.CommandShell shell = null;

        System.Threading.Thread thread = null;

        private void run()
        {
            codeEditor.NavigatePanel.NavigatePanelNode node;
            codeEditor.Controller.NavigatePanel.GetSelectedNode(out node);
            NavigatePanel.VerilogFileNode verilogFileNode = node as NavigatePanel.VerilogFileNode;
            if (node == null) return;

            codeEditor.Controller.NavigatePanel.Parse(node);

            Data.VerilogFile topFile = verilogFileNode.VerilogFile;
            codeEditor.Data.Project project = topFile.Project;

            if (topFile == null) return;
            Verilog.ParsedDocument topParsedDocument = topFile.ParsedDocument as Verilog.ParsedDocument;
            if (topParsedDocument == null) return;
            if (topParsedDocument.Root.BuldingBlocks.Count == 0) return;

            string simName = topFile.Name.Substring(0, topFile.Name.LastIndexOf('.'));


            string simulationPath = Setup.SimulationPath + "\\" + simName;
            if (!System.IO.Directory.Exists(simulationPath))
            {
                System.IO.Directory.CreateDirectory(simulationPath);
            }

            List<string> filePathList = new List<string>();
            {
                string absolutePath = project.GetAbsolutePath(topFile.RelativePath);
                filePathList.Add(absolutePath);
            }

            List<string> includeFileList = new List<string>();

            foreach (BuildingBlock module in topParsedDocument.Root.BuldingBlocks.Values)
            {
                appendFiles(filePathList,includeFileList, module, project);
            }


            using (System.IO.StreamWriter sw = new System.IO.StreamWriter(simulationPath + "\\command"))
            {
                foreach (string includePath in includeFileList)
                { 
                    sw.WriteLine("+incdir+" + getShortPath(includePath)); // path with space is not accepted
                }
                foreach (string absolutePath in filePathList)
                {
                    sw.WriteLine(absolutePath);
                }
            }

            shell = new ajkControls.CommandShell(new List<string> {
                "prompt icarusVerilogShell$G$_",
                "cd "+simulationPath
            });
            shell.LineReceived += receiveLineString;
            shell.Start();

            while(shell.GetLastLine() != "icarusVerilogShell>") {
                if (abort) return;
                System.Threading.Thread.Sleep(10);
            }
            shell.ClearLogs();
            shell.StartLogging();
            shell.Execute(Setup.BinPath + "iverilog -f command -o " + simName + ".o");
            shell.Execute("");
            while (shell.GetLastLine() != "icarusVerilogShell>") {
                if (abort) return;
                System.Threading.Thread.Sleep(10);
            }
            List<string> logs = shell.GetLogs();
            //if(logs.Count != 3 || logs[1] !="")
            //{
            //    return;
            //}
            shell.EndLogging();
            shell.Execute(Setup.BinPath + "vvp " + simName + ".o");


        }

        private void appendFiles(List<string> filePathList, List<string> includePathList, BuildingBlock module, codeEditor.Data.Project project)
        {
            string fileId = module.FileId;
            Data.VerilogFile file = module.File as Data.VerilogFile;
            if (file == null) return;
            string absolutePath = project.GetAbsolutePath(file.RelativePath);
            if (!filePathList.Contains(absolutePath)) filePathList.Add(absolutePath);

            if (file.VerilogParsedDocument == null) return;

            // includes
            foreach(var include in file.VerilogParsedDocument.IncludeFiles.Values)
            {
                string includePath = project.GetAbsolutePath(include.RelativePath);
                includePath = includePath.Substring(0, includePath.LastIndexOf('\\'));
                if (!includePathList.Contains(includePath)) includePathList.Add(includePath);
            }

            foreach(Verilog.ModuleItems.ModuleInstantiation instance in module.Instantiations.Values)
            {
                BuildingBlock subModule = project.GetPluginProperty().GetBuildingBlock(instance.SourceName);
                if (subModule != null) appendFiles(filePathList, includePathList, subModule, project);
            }

        }

    }
}
