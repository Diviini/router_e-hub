// EmitterHub.UI/Converters/ValueToBrushConverter.cs
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace EmitterHub.UI.Converters
{
    public class ValueToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is byte b && b > 0)
                return Brushes.White;      // ou toute autre couleur « remplie »
            return Brushes.Transparent;   // vide
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
