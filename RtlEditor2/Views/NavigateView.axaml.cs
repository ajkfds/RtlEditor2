using AjkAvaloniaLibs.Contorls;
using Avalonia.Controls;
using RtlEditor2.Data;
using RtlEditor2.NavigatePanel;

namespace RtlEditor2.Views
{
    public partial class NavigateView : UserControl
    {
        public NavigateView()
        {
            InitializeComponent();

            Global.navigateView = this;
        }

        public void AddProject(Project project)
        {
            ProjectNode pNode = new NavigatePanel.ProjectNode(project);
            TreeControl.Nodes.Add(pNode);

            pNode.Update();
        }

        public ProjectNode GetPeojectNode(string projectName)
        {
            ProjectNode ret = null;
            foreach (NavigatePanel.NavigatePanelNode node in TreeControl.Nodes)
            {
                if (node is ProjectNode)
                {
                    ProjectNode pnode = node as ProjectNode;
                    if (pnode.Project.Name == projectName) ret = pnode;
                }
            }
            return ret;
        }
    }
}
