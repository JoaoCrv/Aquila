namespace Aquila.Models;

public class AppSettings
{
    public string Theme             { get; set; } = "Light";
    public int    PollingIntervalMs { get; set; } = 1000;
}
