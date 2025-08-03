using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ServiceBusExplorer.Core.Models;

namespace ServiceBusExplorer.UI.Converters;

public class LogLevelColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Info => Brushes.Black,
                LogLevel.Warning => Brushes.Orange,
                LogLevel.Error => Brushes.Red,
                _ => Brushes.Gray
            };
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
