using ExCSS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RtlEditor2.Models.Common
{
    public static class Global
    {
        //public static Dictionary<string, FileTypes.FileType> FileTypes = new Dictionary<string, FileTypes.FileType>();
        public static Dictionary<string, Editor.Data.Project> Projects = new Dictionary<string, Editor.Data.Project>();
        //public static Dictionary<string, codeEditorPlugin.IPlugin> Plugins = new Dictionary<string, codeEditorPlugin.IPlugin>();

        public static Setup Setup = new Setup();
        //public static Dictionary<string, codeEditorPlugin.PluginSetup> PluginSetups = new Dictionary<string, codeEditorPlugin.PluginSetup>();
        internal static ViewModels.MainViewModel mainForm;
        internal static ViewModels.NavigateViewModel navigateView;

        //public static ajkControls.CodeTextbox.CodeDrawStyle DefaultDrawStyle = new codeEditor.CodeDrawStyle();
        //public static IWshRuntimeLibrary.WshShell WshShell = new IWshRuntimeLibrary.WshShell();

        //public static class IconImages
        //{
        //    public static ajkControls.Primitive.IconImage Terminal = new ajkControls.Primitive.IconImage(Properties.Resources.terminal);
        //    public static ajkControls.Primitive.IconImage Text = new ajkControls.Primitive.IconImage(Properties.Resources.text);
        //    public static ajkControls.Primitive.IconImage SaveFile = new ajkControls.Primitive.IconImage(Properties.Resources.saveFile);
        //    public static ajkControls.Primitive.IconImage Wave0 = new ajkControls.Primitive.IconImage(Properties.Resources.wave0);
        //    public static ajkControls.Primitive.IconImage Wave1 = new ajkControls.Primitive.IconImage(Properties.Resources.wave1);
        //    public static ajkControls.Primitive.IconImage Wave2 = new ajkControls.Primitive.IconImage(Properties.Resources.wave2);
        //    public static ajkControls.Primitive.IconImage Wave3 = new ajkControls.Primitive.IconImage(Properties.Resources.wave3);
        //    public static ajkControls.Primitive.IconImage Wave4 = new ajkControls.Primitive.IconImage(Properties.Resources.wave4);
        //    public static ajkControls.Primitive.IconImage Wave5 = new ajkControls.Primitive.IconImage(Properties.Resources.wave5);
        //    public static ajkControls.Primitive.IconImage Play = new ajkControls.Primitive.IconImage(Properties.Resources.play);
        //    public static ajkControls.Primitive.IconImage Pause = new ajkControls.Primitive.IconImage(Properties.Resources.pause);
        //    public static ajkControls.Primitive.IconImage Git = new ajkControls.Primitive.IconImage(Properties.Resources.tree);
        //    public static ajkControls.Primitive.IconImage NewBadge = new ajkControls.Primitive.IconImage(Properties.Resources.newBadge);
        //    public static ajkControls.Primitive.IconImage IgnoreBadge = new ajkControls.Primitive.IconImage(Properties.Resources.ignore);
        //    public static ajkControls.Primitive.IconImage Link = new ajkControls.Primitive.IconImage(Properties.Resources.link);
        //}

        //public static class ColorMap
        //{
        //    public static System.Drawing.Color DarkBackground = System.Drawing.Color.FromArgb(0x20, 0x38, 0x64);
        //    public static System.Drawing.Color LightBackground = System.Drawing.Color.FromArgb(0x32, 0x59, 0xa0);
        //    public static System.Drawing.Color SelectedBackground = System.Drawing.Color.FromArgb(32, 56, 100); //;System.Drawing.Color.FromArgb(0xa9, 0xba, 0xda);
        //    public static System.Drawing.Color Foreground = System.Drawing.Color.White;
        //}

        //// for debug
        //public static System.Runtime.Serialization.ObjectIDGenerator IdGenerator = new System.Runtime.Serialization.ObjectIDGenerator();
        //public static bool StopParse = false;
    }
}
