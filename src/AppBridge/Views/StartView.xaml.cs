using AppBridge.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace AppBridge.Views;

/// <summary>The first screen: transfer now, update and restore on one iPhone, or transfer later.</summary>
public sealed partial class StartView : UserControl
{
    public StartView(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public MainViewModel ViewModel { get; }
}
