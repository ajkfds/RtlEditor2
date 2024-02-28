using Avalonia.Controls;
using RtlEditor2.Models.Common;

namespace RtlEditor2.Views
{
    public partial class TreeControlView : UserControl
    {
        public TreeControlView()
        {
            InitializeComponent();
            // DataContext��ViewModel�̐ݒ�
            Tree.DataContext = new ViewModels.TreeControlViewModel();
        }

        // �N���b�N�n���h��
        private void TreeView_Tapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            TreeNode? node = getTreeNode(e.Source);
            if (node == null)
            {
                System.Diagnostics.Debug.Print(e.ToString());
            }
            if (node == null) return;
            node.OnClicked();
        }

        // �I���A�C�e���ύX�n���h��
        private void TreeView_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
        {
            TreeNode node = getTreeNode(Tree.SelectedItem);
            if (node == null) return;
            node.OnSelected();
        }

        // �_�u���N���b�N �C�x���g�n���h��
        private void TreeView_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            TreeNode node = getTreeNode(e.Source);
            if (node == null) return;
            node.OnDoubleClicked();
        }

        // �I�u�W�F�N�g����TreeNode���擾����
        private TreeNode? getTreeNode(object? target)
        {
            if (target == null) return null;
            if (target is TreeNode)
            {
                return target as TreeNode;
            }
            if (target is TextBlock)
            { // target��TextBock�̏ꍇ�͐eobject��T������
                TextBlock? textBlock = target as TextBlock;
                return getTreeNode(textBlock.Parent);
            }
            else if (target is StackPanel)
            { // target��StackPanel�̏ꍇ�͐eobject��T������
                StackPanel? stackPanel = target as StackPanel;
                return getTreeNode(stackPanel.Parent);
            }
            else if (target is TreeViewItem)
            { // target��TreeViewItem�̏ꍇ��Bind����Ă���TreeNode���擾����
                TreeViewItem? treeViewItem = target as TreeViewItem;
                if (treeViewItem == null) return null;
                return treeViewItem.DataContext as TreeNode;
            }
            else
            { // �ق���object��������Ȃ������ꍇ
                return null;
            }
        }
    }
}
