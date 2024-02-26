using RtlEditor2.Models.Common;
using RtlEditor2.Models.Editor.Data;
using System;

namespace RtlEditor2.ViewModels;

public class MainViewModel : ViewModelBase
{
    private const string setupFileName = "codeEditor.json";

    public MainViewModel()
    {
        Models.Common.Global.mainForm = this;

        // read setup file
        if (System.IO.File.Exists(setupFileName))
        {
            Global.Setup.LoadSetup(setupFileName);
        }
    }

    // View controller interface //////////////////////////////////////////

    internal void Controller_AddProject(Models.Editor.Data.Project project)
    {
        if (Global.Projects.ContainsKey(project.Name))
        {
            System.Diagnostics.Debugger.Break();
            return;
        }
        Global.Projects.Add(project.Name, project);
        addProject(project);
    }

    //internal System.Windows.Forms.MenuStrip Controller_GetMenuStrip()
    //{
    //    return menuStrip;
    //}

    //// tabs
    //internal void Controller_AddTabPage(ajkControls.TabControl.TabPage tabPage)
    //{
    //    mainTab.TabPages.Add(tabPage);
    //}

    //internal void Controller_RemoveTabPage(ajkControls.TabControl.TabPage tabPage)
    //{
    //    mainTab.TabPages.Remove(tabPage);
    //}

    //// code editor

    //internal void Controller_RefreshCodeEditor()
    //{
    //    if (InvokeRequired)
    //    {
    //        editorPage.CodeEditor.Invoke(new Action(editorPage.CodeEditor.Refresh));
    //    }
    //    else
    //    {
    //        editorPage.CodeEditor.Refresh();
    //    }
    //}

    //////////////
    private void addProject(Models.Editor.Data.Project project)
    {
        Models.Common.Global.navigateView.AddProject(project);
        //Tools.ParseProjectForm pform = new Tools.ParseProjectForm(navigatePanel.GetPeojectNode(project.Name));
        //pform.ShowDialog(this);
    }


}
