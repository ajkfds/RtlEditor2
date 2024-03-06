using Avalonia.Media;
using HarfBuzzSharp;
using RtlEditor2.Data;
using RtlEditor2.Models.Common;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RtlEditor2.NavigatePanel
{
    public class NavigatePanelNode : AjkAvaloniaLibs.Contorls.TreeNode
    {
        public override IImage? Image
        {
            get
            {
                return AjkAvaloniaLibs.Libs.Icons.GetSvgBitmap("RtlEditor2/Assets/Icons/paper.svg");
            }
        }

        protected NavigatePanelNode()
        {
        }

        public NavigatePanelNode(Item item)
        {
            itemRef = new WeakReference<Item>(item);
            Name = item.Name;
            if (NavigatePanelNodeCreated != null) NavigatePanelNodeCreated(this);
        }

        public new string Name { get; protected set; }

        private bool link = false;

        public bool Link
        {
            get
            {
                return link;
            }
            set
            {
                link = value;
            }
        }

        private WeakReference<Item> itemRef;
        public Item Item
        {
            get
            {
                Item ret;
                if (!itemRef.TryGetTarget(out ret)) return null;
                return ret;
            }
        }

        public static Action<NavigatePanelNode>? NavigatePanelNodeCreated;

        /// <summary>
        /// update this node and children
        /// </summary>
        public virtual void Update()
        {
        }

        public virtual void Dispose()
        {

        }

        /// <summary>
        /// update all nodes under this node
        /// </summary>
        public virtual void HierarchicalUpdate()
        {
            HierarchicalUpdate(0);
        }

        public virtual void HierarchicalUpdate(int depth)
        {
            Update();
            if (depth > 100) return;
            foreach (NavigatePanelNode node in Nodes)
            {
                node.HierarchicalUpdate(depth + 1);
            }
        }
        public override void OnExpand()
        {
            HierarchicalVisibleUpdate();
        }

        public override void OnCollapse()
        {
            HierarchicalVisibleUpdate();
        }


        public virtual void HierarchicalVisibleUpdate()
        {
            HierarchicalVisibleUpdate(0, IsExpanded);
        }

        public virtual void HierarchicalVisibleUpdate(int depth, bool expanded)
        {
            Update();
            if (depth > 100) return;
            if (!expanded) return;
            foreach (NavigatePanelNode node in Nodes)
            {
                node.HierarchicalVisibleUpdate(depth + 1, node.IsExpanded);
            }
        }

        public virtual void Clicked()
        {

        }

        public virtual void DoubleClicked()
        {
        }

        //public override void DrawNode(Graphics graphics, int x, int y, Font font, Color color, Color backgroundColor, Color selectedColor, int lineHeight, bool selected)
        //{
        //    if (Link) graphics.DrawImage(Global.IconImages.Link.GetImage(lineHeight, ajkControls.Primitive.IconImage.ColorStyle.Blue), new Point(x, y));
        //    base.DrawNode(graphics, x, y, font, color, backgroundColor, selectedColor, lineHeight, selected);
        //}

        public virtual void ShowProperyForm()
        {

        }


    }
}
