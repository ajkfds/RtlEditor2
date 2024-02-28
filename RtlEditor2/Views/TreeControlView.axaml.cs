using Avalonia.Controls;
using RtlEditor2.Models.Common;

namespace RtlEditor2.Views
{
    public partial class TreeControlView : UserControl
    {
        public TreeControlView()
        {
            InitializeComponent();
            // DataContextのViewModelの設定
            Tree.DataContext = new ViewModels.TreeControlViewModel();
        }

        // クリックハンドラ
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

        // 選択アイテム変更ハンドラ
        private void TreeView_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
        {
            TreeNode node = getTreeNode(Tree.SelectedItem);
            if (node == null) return;
            node.OnSelected();
        }

        // ダブルクリック イベントハンドラ
        private void TreeView_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            TreeNode node = getTreeNode(e.Source);
            if (node == null) return;
            node.OnDoubleClicked();
        }

        // オブジェクトからTreeNodeを取得する
        private TreeNode? getTreeNode(object? target)
        {
            if (target == null) return null;
            if (target is TreeNode)
            {
                return target as TreeNode;
            }
            if (target is TextBlock)
            { // targetがTextBockの場合は親objectを探索する
                TextBlock? textBlock = target as TextBlock;
                return getTreeNode(textBlock.Parent);
            }
            else if (target is StackPanel)
            { // targetがStackPanelの場合は親objectを探索する
                StackPanel? stackPanel = target as StackPanel;
                return getTreeNode(stackPanel.Parent);
            }
            else if (target is TreeViewItem)
            { // targetがTreeViewItemの場合はBindされているTreeNodeを取得する
                TreeViewItem? treeViewItem = target as TreeViewItem;
                if (treeViewItem == null) return null;
                return treeViewItem.DataContext as TreeNode;
            }
            else
            { // ほしいobjectが見つからなかった場合
                return null;
            }
        }
    }
}
