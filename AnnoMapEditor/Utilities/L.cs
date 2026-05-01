using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using System;
using System.Globalization;

namespace AnnoMapEditor.Utilities
{
    /// <summary>
    /// XAML markup extension: {l:L Key=main.save_mod} produces a binding to
    /// Localizer.Current.Version with a converter that resolves Key. When the
    /// language changes, Version increments and every binding re-evaluates.
    /// </summary>
    public class L : MarkupExtension
    {
        public string Key { get; set; } = string.Empty;

        public L() { }
        public L(string key) { Key = key; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return new Binding(nameof(Localizer.Version))
            {
                Source = Localizer.Current,
                Mode = BindingMode.OneWay,
                Converter = LocalizerConverter.Instance,
                ConverterParameter = Key,
                FallbackValue = Key
            };
        }
    }

    internal class LocalizerConverter : IValueConverter
    {
        public static readonly LocalizerConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Localizer.Current.Get(parameter as string ?? string.Empty);

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
