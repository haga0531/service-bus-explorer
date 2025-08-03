using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ServiceBusExplorer.UI.Converters;

public class StatusColorConverter : IValueConverter
{
    public static readonly StatusColorConverter Instance = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isDeadLetter)
        {
            return isDeadLetter ? Brushes.Red : Brushes.Green;
        }
        return Brushes.Black;
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
} 
