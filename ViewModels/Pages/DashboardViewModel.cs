using Aquila.Models;
using Aquila.Models.Nodes;
using Aquila.Services;
using Aquila.Views.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace Aquila.ViewModels.Pages;

public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly AquilaService _aquila;
    private readonly SettingsService _settings;
    private readonly DispatcherTimer _clockTimer;

    public HardwareNode Hardware => _aquila.State.Hardware;

    [ObservableProperty] private List<FanRowItem> _fanRows = [];

    private bool GpuVisible     => _settings.Current.ShowGpuCard;
    private bool StorageVisible => _settings.Current.ShowStorageCard;

    public GpuNode? Gpu1 => GpuVisible && Hardware.Gpus.Count > 0 ? Hardware.Gpus[0] : null;
    public GpuNode? Gpu2 => GpuVisible && Hardware.Gpus.Count > 1 ? Hardware.Gpus[1] : null;

    public StorageNode? Storage1 => StorageVisible && Hardware.Storages.Count > 0 ? Hardware.Storages[0] : null;
    public StorageNode? Storage2 => StorageVisible && Hardware.Storages.Count > 1 ? Hardware.Storages[1] : null;
    public StorageNode? Storage3 => StorageVisible && Hardware.Storages.Count > 2 ? Hardware.Storages[2] : null;
    public StorageNode? Storage4 => StorageVisible && Hardware.Storages.Count > 3 ? Hardware.Storages[3] : null;

    public string SystemUptime
    {
        get
        {
            var t = TimeSpan.FromMilliseconds(Environment.TickCount64);
            return t.Days > 0
                ? $"{t.Days}d {t.Hours:D2}h {t.Minutes:D2}m"
                : $"{t.Hours:D2}h {t.Minutes:D2}m";
        }
    }

    public string CurrentDateTime => DateTime.Now.ToString("ddd, d MMM  HH:mm");

    public Visibility DashboardControls    => _settings.Current.DashboardMode         ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowCpuCard          => _settings.Current.ShowCpuCard          ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowMemoryCard       => _settings.Current.ShowMemoryCard       ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowNetworkCard      => _settings.Current.ShowNetworkCard      ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowTemperaturesCard => _settings.Current.ShowTemperaturesCard ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowPowerCard        => _settings.Current.ShowPowerCard        ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowFansCard         => _settings.Current.ShowFansCard         ? Visibility.Visible : Visibility.Collapsed;

    public bool IsDashboardWindowVisible =>
        Application.Current.Windows.OfType<DashboardWindow>().Any(w => w.IsVisible);

    public SymbolRegular DashboardToggleIcon =>
        IsDashboardWindowVisible ? SymbolRegular.Dismiss24 : SymbolRegular.ArrowExpand24;

    public string DashboardToggleTooltip =>
        IsDashboardWindowVisible ? "Close dashboard" : "Open dashboard";

    [RelayCommand]
    private void ToggleDashboard()
    {
        var dw = App.Services.GetRequiredService<DashboardWindow>();
        if (dw.IsVisible)
        {
            dw.Hide();
            _settings.Current.DashboardMode  = false;
            _settings.Current.MinimizeToTray = false;
            _settings.Save();
        }
        else
        {
            if (!_settings.Current.DashboardMode)
            {
                _settings.Current.DashboardMode  = true;
                _settings.Current.MinimizeToTray = true;
                _settings.Save();
            }
            dw.Show();
        }
        NotifyDashboardToggle();
    }

    private void NotifyDashboardToggle()
    {
        OnPropertyChanged(nameof(IsDashboardWindowVisible));
        OnPropertyChanged(nameof(DashboardToggleIcon));
        OnPropertyChanged(nameof(DashboardToggleTooltip));
    }

    public DashboardViewModel(AquilaService aquila, SettingsService settings)
    {
        _aquila = aquila;
        _settings = settings;
        _aquila.DataUpdated += OnDataUpdated;
        _settings.Changed += OnSettingsChanged;

        _clockTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _clockTimer.Tick += (_, _) =>
        {
            OnPropertyChanged(nameof(SystemUptime));
            OnPropertyChanged(nameof(CurrentDateTime));
        };
        _clockTimer.Start();

        OnDataUpdated();
    }

    private void OnDataUpdated()
    {
        UpdateFans();

        OnPropertyChanged(nameof(Gpu1));
        OnPropertyChanged(nameof(Gpu2));
        OnPropertyChanged(nameof(Storage1));
        OnPropertyChanged(nameof(Storage2));
        OnPropertyChanged(nameof(Storage3));
        OnPropertyChanged(nameof(Storage4));
        NotifyDashboardToggle();
    }

    private void UpdateFans()
    {
        var mb = Hardware.Motherboard;
        FanRows = mb.Fan
            .Where(f => f.Value > 0)
            .Select(f => new FanRowItem(f, mb.Control[f.Name ?? string.Empty]))
            .ToList();
    }

    private void OnSettingsChanged()
    {
        OnPropertyChanged(nameof(DashboardControls));
        OnPropertyChanged(nameof(ShowCpuCard));
        OnPropertyChanged(nameof(ShowMemoryCard));
        OnPropertyChanged(nameof(ShowNetworkCard));
        OnPropertyChanged(nameof(ShowTemperaturesCard));
        OnPropertyChanged(nameof(ShowPowerCard));
        OnPropertyChanged(nameof(ShowFansCard));
        OnPropertyChanged(nameof(Gpu1));
        OnPropertyChanged(nameof(Gpu2));
        OnPropertyChanged(nameof(Storage1));
        OnPropertyChanged(nameof(Storage2));
        OnPropertyChanged(nameof(Storage3));
        OnPropertyChanged(nameof(Storage4));
    }

    public void Dispose()
    {
        _clockTimer.Stop();
        _aquila.DataUpdated -= OnDataUpdated;
        _settings.Changed -= OnSettingsChanged;
    }
}

// GpuCardData is gone — the GpuCard control takes a GpuNode directly.
// FanRowItem lives in Aquila.Models (Models/FanRowItem.cs) — shared with the FansCard control.
