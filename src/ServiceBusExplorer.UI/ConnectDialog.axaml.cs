using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ServiceBusExplorer.Core.Models;
using ServiceBusExplorer.Infrastructure.Models;

namespace ServiceBusExplorer.UI;

/// <summary>
///     Code-behind for <see cref="ConnectDialog.axaml"/>.
///     Only needs the auto-generated <c>InitializeComponent()</c>.
/// </summary>
public partial class ConnectDialog : Window
{
    public ConnectDialog()
    {
        InitializeComponent();

        // DataContext がセットされたあとにイベントを購読
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ConnectDialogViewModel vm)
            {
                vm.CloseRequested -= OnCloseRequested;  // 二重登録防止
                vm.CloseRequested += OnCloseRequested;
            }
        };
    }

    private void OnCloseRequested(object? sender, (IReadOnlyList<NamespaceEntity>? entities, ServiceBusAuthContext? authContext) result)
    {
        Close(result);   // ★ ShowDialog<T> へ返す
    }

    private void InitializeComponent() =>
        AvaloniaXamlLoader.Load(this);
}
