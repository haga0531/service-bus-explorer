using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Input;
using ServiceBusExplorer.Core.Models;

namespace ServiceBusExplorer.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Find the TreeView and attach event handler
        var treeView = this.FindControl<TreeView>("NamespaceTreeView");
        treeView?.AddHandler(TreeViewItem.ExpandedEvent, TreeViewItem_Expanded);
    }

    private async void TreeViewItem_Expanded(object? sender, RoutedEventArgs e)
    {
        System.Console.WriteLine($"[TreeViewItem_Expanded] Event fired, sender: {sender}, source: {e.Source}");

        if (e.Source is TreeViewItem item && item.DataContext is NamespaceNode node)
        {
            System.Console.WriteLine($"[TreeViewItem_Expanded] Node expanded: {node.Name}, Type: {node.EntityType}, Children: {node.Children.Count}");

            // Call the command in ViewModel
            if (DataContext is MainWindowViewModel viewModel && viewModel.NodeExpandedCommand.CanExecute(node))
            {
                System.Console.WriteLine($"[TreeViewItem_Expanded] Executing NodeExpandedCommand for: {node.Name}");
                await ((IAsyncRelayCommand<NamespaceNode>)viewModel.NodeExpandedCommand).ExecuteAsync(node);
            }
        }
    }
}
