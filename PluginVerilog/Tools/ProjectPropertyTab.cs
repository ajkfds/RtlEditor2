using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ajkControls.Primitive;

namespace pluginVerilog.Tools
{
    public class ProjectPropertyTab : codeEditor.Tools.ProjectPropertyTab
    {
        public ProjectPropertyTab(codeEditor.Data.Project project)
        {
            panel = new ProjectPropertyPanel(project);

            Controls.Add(this.panel);
            Location = new System.Drawing.Point(4, 29);
            Name = "Verilog";
            Padding = new System.Windows.Forms.Padding(3);
            Size = new System.Drawing.Size(620, 439);
            TabIndex = 0;
            Text = "Verilog";
            UseVisualStyleBackColor = true;
            IconImage = new IconImage(Properties.Resources.verilog);

            panel.Dock = System.Windows.Forms.DockStyle.Fill;
            panel.Name = "codeEditor";
        }

        public static void ProjectPropertyFromCreated(codeEditor.Tools.ProjectPropertyForm form, codeEditor.Data.Project project)
        {
            form.AppendTab(new ProjectPropertyTab(project));
        }

        ProjectPropertyPanel panel;

        public override void PropertyAccept()
        {
            panel.PropertyAccept();
        }

        public override void PropertyCancel()
        {
            panel.PropertyCancel();
        }

    }
}
