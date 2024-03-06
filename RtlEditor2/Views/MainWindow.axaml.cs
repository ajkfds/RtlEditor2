using Avalonia.Controls;

namespace RtlEditor2.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        Global.mainWindow = this;
        InitializeComponent();

    }


}
