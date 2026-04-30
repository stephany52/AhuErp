using System;
using System.Globalization;
using System.Windows.Data;

namespace AhuErp.UI.Converters
{
    public sealed class BooleanDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return b ? "Да" : "Нет";
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
