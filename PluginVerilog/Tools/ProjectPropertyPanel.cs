using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace pluginVerilog.Tools
{
    public partial class ProjectPropertyPanel : UserControl
    {
        public ProjectPropertyPanel(codeEditor.Data.Project project)
        {
            InitializeComponent();

            this.project = project;
            ProjectProperty property = project.ProjectProperties[Plugin.StaticID] as ProjectProperty;

            foreach (var macro in property.Macros.Values)
            {
                macros.Add(macro.Name, macro.MacroText);
            }
            setTxt();
        }
        codeEditor.Data.Project project;

        private Dictionary<string, string> macros = new Dictionary<string, string>();

        private void setTxt()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var macro in macros)
            {
                sb.Append("`define ");
                sb.Append(macro.Key);
                if(macro.Value != "")
                {
                    sb.Append(" ");
                    sb.Append(macro.Value);
                }
                sb.Append("\r\n");
            }
            macroTxt.Text = sb.ToString();
        }

        private void readTxt()
        {
            macros.Clear();
            string[] macroTxts = macroTxt.Text.Split('\n');
            foreach (string line in macroTxts)
            {
                string text = line.Trim(new char[] { '\n', '\r', ' ', '\t' });
                text = text.Replace("\t", " ");
                if (!text.StartsWith("`define ")) continue;
                text = text.Substring("`define ".Length);
                string name, value;
                if(text.Contains(" "))
                {
                    name = text.Substring(0, text.IndexOf(" "));
                    value = text.Substring(text.IndexOf(" "));
                }
                else
                {
                    name = text;
                    value = "";
                }
                if (!macros.ContainsKey(name)) macros.Add(name, value);
            }

        }

        public void PropertyAccept()
        {
            readTxt();
            setTxt();
            ProjectProperty property = project.ProjectProperties[Plugin.StaticID] as ProjectProperty;

            property.Macros.Clear();
            foreach(var macro in macros)
            {
                property.Macros.Add(macro.Key, Verilog.Macro.Create(macro.Key,macro.Value));
            }
        }
        public void PropertyCancel()
        {

        }

        private void MacroTxt_Leave(object sender, EventArgs e)
        {
            readTxt();
            setTxt();
        }
    }
}
