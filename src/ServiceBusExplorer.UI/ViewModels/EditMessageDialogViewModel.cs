using System;
using System.Text.Json;
using System.Xml.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ServiceBusExplorer.UI;

public partial class EditMessageDialogViewModel(Window window) : ObservableObject
{
    private readonly Window _window = window;

    [ObservableProperty]
    private string _originalMessageId = string.Empty;

    [ObservableProperty]
    private string _messageBody = string.Empty;

    [ObservableProperty]
    private string _contentType = "application/json";

    [ObservableProperty]
    private bool _deleteOriginal = false;

    [ObservableProperty]
    private bool _isOriginalDeadLetter = false;

    public bool IsJsonContent => ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) ?? false;
    public bool IsXmlContent => ContentType?.Contains("xml", StringComparison.OrdinalIgnoreCase) ?? false;

    public bool DialogResult { get; private set; }

    public void Initialize(string messageId, string messageBody, string contentType, bool isDeadLetter)
    {
        OriginalMessageId = messageId;
        MessageBody = messageBody;
        ContentType = contentType;
        IsOriginalDeadLetter = isDeadLetter;

        // Dead Letter メッセージの場合、デフォルトで削除オプションをオンにする
        DeleteOriginal = isDeadLetter;
    }

    [RelayCommand]
    private void FormatJson()
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(MessageBody);
            MessageBody = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EditMessageDialog] Failed to format JSON: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Validate()
    {
        try
        {
            if (IsJsonContent)
            {
                JsonDocument.Parse(MessageBody);
                Console.WriteLine("[EditMessageDialog] JSON is valid");
            }
            else if (IsXmlContent)
            {
                XDocument.Parse(MessageBody);
                Console.WriteLine("[EditMessageDialog] XML is valid");
            }

            // TODO: 成功メッセージを表示
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EditMessageDialog] Validation failed: {ex.Message}");
            // TODO: エラーメッセージを表示
        }
    }

    [RelayCommand]
    private void Send()
    {
        DialogResult = true;
        _window.Close();
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        _window.Close();
    }

    partial void OnContentTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsJsonContent));
        OnPropertyChanged(nameof(IsXmlContent));
    }
}
