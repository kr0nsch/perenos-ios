using System.ComponentModel;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace Perenos.Services;

public sealed class LocalizedText : INotifyPropertyChanged
{
    public static LocalizedText Instance { get; } = new();

    private LocalizedText() => Loc.Changed += (_, _) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));

    public string this[string key] => Loc.Get(key);

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class TextExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public TextExtension() : this("") { }

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = LocalizedText.Instance, Mode = BindingMode.OneWay };
}
