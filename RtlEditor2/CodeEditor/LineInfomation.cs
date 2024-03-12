using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace RtlEditor2.CodeEditor
{
    public class LineInfomation
    {
        public List<Color> Colors = new List<Color>();
        private static Dictionary<byte, SolidColorBrush> SolidBrushes = new Dictionary<byte, SolidColorBrush>();  

        public class Color
        {
            public Color(int offeset,int length,byte colorIndex)
            {
                this.Offset = offeset;
                this.Length = length;
                if (!SolidBrushes.ContainsKey(colorIndex))
                {
                    SolidBrushes.Add(colorIndex, new SolidColorBrush(Global.DefaultDrawStyle.ColorPallet[colorIndex]));
                }
                this.Brush = SolidBrushes[colorIndex];
            }
            public int Offset;
            public int Length;
            public IBrush Brush;
        }

        public class Effect
        {
            public int Offset;
            public int Length;
            public TextDecoration Delocation;
        }
    }
}
