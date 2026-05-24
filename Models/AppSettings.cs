namespace Aquila.Models;

public class AppSettings
{
    public string Theme             { get; set; } = "Light";
    public int    PollingIntervalMs { get; set; } = 1000;
    public bool   MinimizeToTray   { get; set; } = false;
    public bool   StartMinimized   { get; set; } = false;
    public double WindowLeft       { get; set; } = double.NaN;
    public double WindowTop        { get; set; } = double.NaN;
    public double WindowWidth      { get; set; } = 1600;
    public double WindowHeight     { get; set; } = 900;
    public bool   WindowMaximized  { get; set; } = false;
    public bool   DashboardMode         { get; set; } = false;
    public bool   EnableVerboseLogging  { get; set; } = false;

    public bool ShowCpuCard          { get; set; } = true;
    public bool ShowMemoryCard       { get; set; } = true;
    public bool ShowNetworkCard      { get; set; } = true;
    public bool ShowTemperaturesCard { get; set; } = true;
    public bool ShowPowerCard        { get; set; } = true;
    public bool ShowFansCard         { get; set; } = true;
    public bool ShowGpuCard          { get; set; } = true;
    public bool ShowStorageCard      { get; set; } = true;
}
