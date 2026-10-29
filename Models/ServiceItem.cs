using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZapretDashboard.Models
{
    public class ServiceItem : INotifyPropertyChanged
    {
        private string _name = "";
        private string _domain = "";

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Domain
        {
            get => _domain;
            set { _domain = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}