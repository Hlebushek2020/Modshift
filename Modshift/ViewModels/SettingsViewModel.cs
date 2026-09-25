using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Modshift.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _selectedLanguage = "Русский (Russian)";

    public ObservableCollection<string> AvailableLanguages { get; } = new()
    {
        "Русский (Russian)",
        "English"
    };

    [RelayCommand]
    private void CloseSettings(object? window)
    {
        if (window is Avalonia.Controls.Window avaloniaWindow)
        {
            avaloniaWindow.Close();
        }
    }
}