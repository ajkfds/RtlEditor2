using Avalonia.Controls;
using RtlEditor2.Models.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RtlEditor2.ViewModels
{
    public class NavigateViewModel : ViewModelBase
    {
        public ObservableCollection<TreeNode> Nodes { get; } = new ObservableCollection<TreeNode>();

        //public NavigateViewModel NavigateView
        //{
        //    get { return NavigateView; }
        //}

        public NavigateViewModel()
        {
            TreeNode sampleNode = new TreeNode("sample");

            Nodes.Add(sampleNode);

            Models.Common.Global.navigateView = this;
        }

        public void AddProject(Models.Editor.Data.Project project)
        {
            Models.Editor.NavigatePanel.ProjectNode pNode = new Models.Editor.NavigatePanel.ProjectNode(project);
            Nodes.Add(pNode);

            pNode.Update();

//            pNode.Update();
//            treeView.Refresh();
        }
    }
}
