using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media;

namespace RtlEditor2.CodeEditor
{
    public class CodeDrawStyle
    {
        public CodeDrawStyle()
        {
            colors = new Color[16]
            {
                Color.FromRgb(212,212,212), // 0
                Color.FromRgb(255,255,255), // 1
                Color.FromRgb(255,255,255), // 2
                Color.FromRgb(255,255,255), // 3
                Color.FromRgb(255,255,255), // 4
                Color.FromRgb(255,255,255), // 5
                Color.FromRgb(255,255,255), // 6
                Color.FromRgb(255,255,255), // 7
                Color.FromRgb(255,255,255), // 8
                Color.FromRgb(255,255,255), // 9
                Color.FromRgb(255,255,255), // 10
                Color.FromRgb(255,255,255), // 11
                Color.FromRgb(255,255,255), // 12
                Color.FromRgb(255,255,255), // 13
                Color.FromRgb(255,255,255), // 14
                Color.FromRgb(255,255,255)  // 15
            };
        }

        protected Color[] colors;

        public virtual Color[] ColorPallet
        {
            get
            {
                return colors;
            }
        }

        public virtual Color[] MarkColor
        {
            get
            {
                return new Color[8]
                    {
                        Color.FromArgb(128,255,0,0),    // 0
                        Color.FromArgb(128,255,0,0),    // 1
                        Color.FromArgb(128,255,0,0),    // 2
                        Color.FromArgb(128,255,0,0),    // 3
                        Color.FromArgb(128,255,0,0),    // 4
                        Color.FromArgb(128,255,0,0),    // 5
                        Color.FromArgb(128,255,0,0),    // 6
                        Color.FromArgb(128,255,0,0)    // 7
                    };
            }
        }

        //public virtual CodeTextbox.MarkStyleEnum[] MarkStyle
        //{
        //    get
        //    {
        //        return new CodeTextbox.MarkStyleEnum[8]
        //            {
        //                CodeTextbox.MarkStyleEnum.wave,    // 0
        //                CodeTextbox.MarkStyleEnum.underLine,
        //                CodeTextbox.MarkStyleEnum.underLine,
        //                CodeTextbox.MarkStyleEnum.underLine,
        //                CodeTextbox.MarkStyleEnum.underLine,
        //                CodeTextbox.MarkStyleEnum.underLine,
        //                CodeTextbox.MarkStyleEnum.underLine,
        //                CodeTextbox.MarkStyleEnum.fill              // 7 for selection highlight
        //            };
        //    }
        //}
    }
}