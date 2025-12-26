using Aquila.Models;
using Aquila.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks; // Adiciona este using!

namespace Aquila.ViewModels.Pages
{
    public partial class ExplorerViewModel(HardwareMonitorService monitor, UiService uiservice) : ObservableObject
    {
        private readonly HardwareMonitorService _monitor = monitor;
        private readonly UiService _uiService = uiservice;


    }
}