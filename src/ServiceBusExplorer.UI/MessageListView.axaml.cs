using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ServiceBusExplorer.UI;

/// <summary>
///     Code-behind for <see cref="MessageListView.axaml"/>.
/// </summary>
public partial class MessageListView : UserControl
{
    public MessageListView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
