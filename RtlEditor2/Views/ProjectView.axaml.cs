using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using RtlEditor2.ViewModels;
using System.Collections.ObjectModel;

namespace RtlEditor2.Views
{
    public partial class ProjectView : UserControl
    {
        public ProjectView()
        {
            InitializeComponent();
            DataContext = new ProjectViewModel();
        }
    }
}
