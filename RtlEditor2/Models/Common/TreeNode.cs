using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using SkiaSharp;
using Avalonia.Svg.Skia;
using HarfBuzzSharp;
using RtlEditor2.Models.Editor.NavigatePanel;
using SkiaSharp;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RtlEditor2.Models.Common
{
    public class TreeNode : INotifyPropertyChanged
    {
        public TreeNode() { }

        public TreeNode(string text)
        {
            Text = text;
        }

        // 初期アイコン (Assets内のpngファイル)
        static IImage defaultBitmap = Icons.GetIconBitmap("paper");
        
        // アイコン画像
        private IImage bitmap = defaultBitmap;
        public virtual IImage Image
        {
            get
            {
                return bitmap;
            }
            set
            {
                bitmap = value;
            }
        }

        // ノード展開状態
        private bool _IsExpanded = false;
        public bool IsExpanded
        {
            get { return _IsExpanded; }
            set
            {
                bool prev = _IsExpanded;
                _IsExpanded = value;
                NotifyPropertyChanged();
                if (!prev & _IsExpanded)
                {
                    OnExpand();
                }
                if (prev & !_IsExpanded)
                {
                    OnCollapse();
                }
            }
        }

        // ノード展開時に呼ばれる
        public virtual void OnExpand()
        {
            Text = "Expanded";
        }

        // ノードを閉じたときに呼ばれる
        public virtual void OnCollapse()
        {
            Text = "Collapsed";
        }

        // ノードが選択されたときに呼ばれる
        public virtual void OnSelected()
        {
            Text = "Selected";
        }

        // ノードがクリックされたときに呼ばれる
        public virtual void OnClicked()
        {
            Text = "Clicked";
        }

        // ノードがダブルクリックされたときに呼ばれる
        public virtual void OnDoubleClicked()
        {
            Text = "DoubleClicked";
        }

        // ノードテキスト
        private string _Text = "";
        public virtual string Text
        {
            get { return _Text; }
            set { _Text = value; NotifyPropertyChanged(); }
        }

        // 子ノード
        private ObservableCollection<TreeNode> nodes = new ObservableCollection<TreeNode>();
        public ObservableCollection<TreeNode> Nodes
        {
            get { return nodes; }
            set { nodes = value; NotifyPropertyChanged(); }
        }

        // 双方向BIndingのためのViewModelへのProperty変更通知
        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
