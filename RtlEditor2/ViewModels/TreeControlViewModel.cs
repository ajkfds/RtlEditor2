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
    internal class TreeControlViewModel : ViewModelBase
    {
        public ObservableCollection<TreeNode> Nodes { get; } = new ObservableCollection<TreeNode>();

        public TreeControlViewModel()
        {
            TreeNode node = new TreeNode("AAA");
            Nodes.Add(node);

            TreeNode node2 = new TreeNode("BBB");
            node.Nodes.Add(node2);
        }

        private bool isEx;
        public bool IsExpanded
        {
            get
            {
                return isEx;
            }
            set
            {
                isEx = value;
            }
        }
    }
}
