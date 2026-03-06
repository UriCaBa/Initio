using System.Windows;
using System.Windows.Controls;

namespace NewPCSetupWPF.Controls;

public partial class SidebarPane : UserControl
{
    public static readonly RoutedEvent ActivateWindowsRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(ActivateWindowsRequested),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(SidebarPane));

    public SidebarPane()
    {
        InitializeComponent();
    }

    public event RoutedEventHandler ActivateWindowsRequested
    {
        add => AddHandler(ActivateWindowsRequestedEvent, value);
        remove => RemoveHandler(ActivateWindowsRequestedEvent, value);
    }

    private void ActivateWindows_Click(object sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(ActivateWindowsRequestedEvent));
    }
}
