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

namespace RtlEditor2.Models
{
    public class Node
    {
        public ObservableCollection<Node>? SubNodes { get; }
        public string Title { get; }

        public Node(string title)
        {
            Title = title;
        }

        public void test()
        {
        }

        Bitmap defaultImage = new Bitmap(AssetLoader.Open(new Uri("avares://RtlEditor2/Assets/Icons/paper.png")));

        public IImage Image
        {
            get {
                return defaultImage;
            }
        }


        public Node(string title, ObservableCollection<Node> subNodes)
        {
            Title = title;
            SubNodes = subNodes;
        }
    }
}
