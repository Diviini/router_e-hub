// EmitterHub.UI/Converters/NullToBoolConverter.cs
using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace EmitterHub.UI.Converters
{
    /// <summary>
    /// Convertit une valeur null → false, non-null → true.
    /// </summary>
    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is not null;
        }

        public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
