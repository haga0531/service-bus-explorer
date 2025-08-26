using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ServiceBusExplorer.Infrastructure;

namespace ServiceBusExplorer.UI;

public partial class SendMessageDialogViewModel : ObservableObject
{
    [ObservableProperty] private string destinationPath = string.Empty;
    [ObservableProperty] private string? label;
    [ObservableProperty] private string? contentType;
    [ObservableProperty] private int messageFormat; // 0: Plain Text, 1: JSON, 2: XML
    [ObservableProperty] private string messageBody = string.Empty;
    [ObservableProperty] private bool isSending = false;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private bool isFormatButtonVisible;
    [ObservableProperty] private bool isValidJson;
    [ObservableProperty] private bool isValidXml;

    public IRelayCommand FormatJsonCommand { get; }

    private readonly Func<string, IMessageSendProvider> _sendProviderFactory;
    private readonly string _connectionString;
    private readonly string _entityPath;

    public IRelayCommand CancelCommand { get; }
    public IRelayCommand SendCommand { get; }

    public event EventHandler<bool>? CloseRequested;

    public SendMessageDialogViewModel(
        Func<string, IMessageSendProvider> sendProviderFactory,
        string connectionString,
        string entityPath)
    {
        _sendProviderFactory = sendProviderFactory;
        _connectionString = connectionString;
        _entityPath = entityPath;
        DestinationPath = entityPath;

        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
        SendCommand = new AsyncRelayCommand(SendMessageAsync);
        FormatJsonCommand = new RelayCommand(FormatMessageBody);

        // Set default content type based on format
        MessageFormat = 0; // Plain Text by default
    }

    partial void OnMessageFormatChanged(int value)
    {
        ContentType = value switch
        {
            1 => "application/json",
            2 => "application/xml",
            _ => "text/plain"
        };

        // Update format button visibility
        IsFormatButtonVisible = value != 0;

        // Auto-format if format is changed and body is not empty
        if (!string.IsNullOrWhiteSpace(MessageBody))
        {
            FormatMessageBody();
        }

        UpdateValidationState();
    }

    partial void OnMessageBodyChanged(string value)
    {
        // Clear validation error when user starts typing
        if (ErrorMessage?.StartsWith("Invalid") == true)
        {
            ErrorMessage = null;
        }

        UpdateValidationState();
    }

    private void FormatMessageBody()
    {
        if (string.IsNullOrWhiteSpace(MessageBody))
        {
            return;
        }

        try
        {
            switch (MessageFormat)
            {
                case 1: // JSON
                    var jsonDoc = JsonDocument.Parse(MessageBody);
                    MessageBody = JsonSerializer.Serialize(jsonDoc.RootElement, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    ErrorMessage = null;
                    break;

                case 2: // XML
                    var xmlDoc = XDocument.Parse(MessageBody);
                    MessageBody = xmlDoc.ToString();
                    ErrorMessage = null;
                    break;
            }

            UpdateValidationState();
        }
        catch (JsonException ex)
        {
            ErrorMessage = $"Invalid JSON: {ex.Message}";
            UpdateValidationState();
        }
        catch (XmlException ex)
        {
            ErrorMessage = $"Invalid XML: {ex.Message}";
            UpdateValidationState();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Format error: {ex.Message}";
            UpdateValidationState();
        }
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(MessageBody))
        {
            ErrorMessage = "Please enter a message body.";
            return;
        }

        // Validate message format
        if (!ValidateMessageFormat())
        {
            return;
        }

        try
        {
            IsSending = true;
            ErrorMessage = null;

            await using var provider = _sendProviderFactory(_connectionString);
            await provider.SendMessageAsync(
                _entityPath,
                null, // subscription
                MessageBody,
                null, // properties
                ContentType,
                Label);

            CloseRequested?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to send message: {ex.Message}";
        }
        finally
        {
            IsSending = false;
        }
    }

    private bool ValidateMessageFormat()
    {
        try
        {
            switch (MessageFormat)
            {
                case 1: // JSON
                    JsonDocument.Parse(MessageBody);
                    break;

                case 2: // XML
                    XDocument.Parse(MessageBody);
                    break;
            }

            ErrorMessage = null;
            return true;
        }
        catch (JsonException ex)
        {
            ErrorMessage = $"Invalid JSON: {ex.Message}";
            return false;
        }
        catch (XmlException ex)
        {
            ErrorMessage = $"Invalid XML: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Validation error: {ex.Message}";
            return false;
        }
    }

    private void UpdateValidationState()
    {
        IsValidJson = false;
        IsValidXml = false;

        if (string.IsNullOrWhiteSpace(MessageBody))
        {
            return;
        }

        if (string.IsNullOrEmpty(ErrorMessage))
        {
            if (MessageFormat == 1) // JSON
            {
                try
                {
                    JsonDocument.Parse(MessageBody);
                    IsValidJson = true;
                }
                catch
                {
                    IsValidJson = false;
                }
            }
            else if (MessageFormat == 2) // XML
            {
                try
                {
                    XDocument.Parse(MessageBody);
                    IsValidXml = true;
                }
                catch
                {
                    IsValidXml = false;
                }
            }
        }
    }
}
