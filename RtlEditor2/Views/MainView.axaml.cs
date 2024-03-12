using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using RtlEditor2.Data;
using RtlEditor2.Models.Common;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RtlEditor2.Views;

public partial class MainView : UserControl
{
    private const string setupFileName = "codeEditor.json";

    public MainView()
    {
        Global.mainView = this;
        InitializeComponent();
        DataContext = new ViewModels.MainViewModel();

        // register text filetype
        FileTypes.TextFile textFileType = new FileTypes.TextFile();
        Global.FileTypes.Add(textFileType.ID, textFileType);

        // load pulgins
        List<CodeEditorPlugin.IPlugin> plugins = new List<CodeEditorPlugin.IPlugin>();
        foreach(var plugin in Global.Plugins.Values)
        {
            plugins.Add(plugin);
        }
        Global.Plugins.Clear();

        while (true)
        {
            int registered = 0;
            foreach (var plugin in plugins)
            {
                if (!Global.Plugins.ContainsKey(plugin.Id))
                {
                    bool complete = plugin.Register();
                    if (complete)
                    {
                        registered++;
                        Global.Plugins.Add(plugin.Id, plugin);
                        //Controller.AppendLog("Loading plugin ... " + plugin.Id);
                    }
                }
            }
            if (registered == 0) break;
        }



        Loaded += MainView_Loaded;

        // initialize pulgins
        List<string> initilalizedPulginName = new List<string>();
        while (true)
        {
            int initialized = 0;
            foreach (string pluginName in Global.Plugins.Keys)
            {
                if (initilalizedPulginName.Contains(pluginName)) continue;
                if (Global.Plugins[pluginName].Initialize())
                {
                    initialized++;
                    //Controller.AppendLog("Initializing plugin ... " + pluginName);
                    initilalizedPulginName.Add(pluginName);
                }
            }
            if (initialized == 0) break;
        }
    }

    private void MainView_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // read setup file
        if (System.IO.File.Exists(setupFileName))
        {
            Global.Setup.LoadSetup(setupFileName);
        }
    }

    internal void Controller_AddProject(Project project)
    {
        if (Global.Projects.ContainsKey(project.Name))
        {
            System.Diagnostics.Debugger.Break();
            return;
        }
        Global.Projects.Add(project.Name, project);
        addProject(project);
    }

    private async void addProject(Project project)
    {
        Global.navigateView.AddProject(project);
        Tools.ParseProjectForm pform = new Tools.ParseProjectForm(NavigateView.GetPeojectNode(project.Name));
        await pform.ShowDialog(Global.mainWindow);
    }
    // View controller interface //////////////////////////////////////////

    //internal void Controller_AddProject(Models.Editor.Data.Project project)
    //{
    //    if (Global.Projects.ContainsKey(project.Name))
    //    {
    //        System.Diagnostics.Debugger.Break();
    //        return;
    //    }
    //    Global.Projects.Add(project.Name, project);
    //    addProject(project);
    //}

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
    //private void addProject(Models.Editor.Data.Project project)
    //{
    //    Models.Common.Global.navigateView.AddProject(project);
    //    //Tools.ParseProjectForm pform = new Tools.ParseProjectForm(navigatePanel.GetPeojectNode(project.Name));
    //    //pform.ShowDialog(this);
    //}

}
