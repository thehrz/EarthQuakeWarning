using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EarthquakeWaring.App.Infrastructure.Models.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{

    private bool _isHideSettings = false;
    public bool IsHideSettings
    {
        get => _isHideSettings;
        set => SetField(ref _isHideSettings, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}