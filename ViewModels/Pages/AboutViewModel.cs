using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Reflection;

namespace Aquila.ViewModels.Pages
{
    public  partial class AboutViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _appVersion = String.Empty;

        public AboutViewModel()
        {
            _appVersion = GetAssemblyVersion();
        }

        private string GetAssemblyVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? String.Empty;
        }
    }
}
