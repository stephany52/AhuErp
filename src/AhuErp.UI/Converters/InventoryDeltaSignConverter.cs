using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AhuErp.UI.Converters
{
    /// <summary>
    /// Bug #5. Конвертер знака количества в <see cref="Core.Models.InventoryTransaction"/>:
    /// положительное — приход (зелёный), отрицательное — расход (красный),
    /// ноль — нейтральный (серый). Применяется в DataGrid «Последние движения»
    /// раздела «Склад / ТМЦ» для двух колонок:
    /// <list type="bullet">
    ///   <item><c>ConverterParameter=Label</c> — текстовая метка «Приход» / «Расход».</item>
    ///   <item><c>ConverterParameter=Foreground</c> — цвет (Brush) для подсветки.</item>
    /// </list>
    /// </summary>
    [ValueConversion(typeof(int), typeof(object))]
    public sealed class InventoryDeltaSignConverter : IValueConverter
    {
        public static readonly Brush IncomeBrush = new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69));
        public static readonly Brush OutcomeBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
        public static readonly Brush NeutralBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));

        static InventoryDeltaSignConverter()
        {
            IncomeBrush.Freeze();
            OutcomeBrush.Freeze();
            NeutralBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int delta = ToInt(value);
            string mode = (parameter as string) ?? "Label";

            if (string.Equals(mode, "Foreground", StringComparison.OrdinalIgnoreCase))
            {
                if (delta > 0) return IncomeBrush;
                if (delta < 0) return OutcomeBrush;
                return NeutralBrush;
            }

            // Label
            if (delta > 0) return "Приход";
            if (delta < 0) return "Расход";
            return "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static int ToInt(object value)
        {
            switch (value)
            {
                case int i: return i;
                case long l: return l > int.MaxValue ? int.MaxValue : l < int.MinValue ? int.MinValue : (int)l;
                case short s: return s;
                case decimal d: return (int)d;
                case double f: return (int)f;
                default:
                    return value != null
                        && int.TryParse(System.Convert.ToString(value, CultureInfo.InvariantCulture),
                                        NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                        ? parsed : 0;
            }
        }
    }
}
