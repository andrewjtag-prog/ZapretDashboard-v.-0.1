using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using ZapretDashboard.Helpers;
using ZapretDashboard.Models;
using ZapretDashboard.Services;

namespace ZapretDashboard.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<StrategyItem> Strategies { get; } = new();
        public ObservableCollection<ServiceItem> Services { get; } = new();

        private string _statusText = "Not connected";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        private string _debugInfo = "";
        public string DebugInfo
        {
            get => _debugInfo;
            set { _debugInfo = value; OnPropertyChanged(); }
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); }
        }

        public ICommand ConnectCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand AddServiceCommand { get; }
        public ICommand RemoveServiceCommand { get; }

        private readonly ZapretService _zapret;
        private StrategyItem? _activeStrategy;

        public MainViewModel()
        {
            _zapret = new ZapretService();

            ConnectCommand = new RelayCommand<StrategyItem>(Connect);
            StopCommand = new RelayCommand(Stop);
            AddServiceCommand = new RelayCommand<string>(AddService);
            RemoveServiceCommand = new RelayCommand<ServiceItem>(RemoveService);

            LoadStrategies();
            LoadServices();

            // Включаем автозапуск приложения с Windows
            SetStartup(true);

            // Автоматически подключаем последнюю стратегию (если она была)
            LoadLastActiveStrategy();
        }

        private void SetStartup(bool enable)
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (exePath == null) return;

                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);

                if (enable)
                    key?.SetValue("ZapretDashboard", $"\"{exePath}\"");
                else
                    key?.DeleteValue("ZapretDashboard", false);
            }
            catch { }
        }

        private void LoadLastActiveStrategy()
        {
            try
            {
                string configPath = Path.Combine(_zapret.WorkingDirectory, "last_strategy.txt");
                if (File.Exists(configPath))
                {
                    string lastStrategyName = File.ReadAllText(configPath).Trim();
                    var strategy = Strategies.FirstOrDefault(s => s.Name == lastStrategyName);
                    if (strategy != null)
                    {
                        Connect(strategy);
                    }
                }
            }
            catch { }
        }

        private void LoadStrategies()
        {
            var files = _zapret.GetStrategyFiles();

            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var resNames = asm.GetManifestResourceNames();
            string resList = string.Join(" | ", resNames.Take(3));

            if (resNames.Length == 0) resList = "НЕТ РЕСУРСОВ В EXE!";
            DebugInfo = $"Found: {files.Length} | Res: {resList}";

            Strategies.Clear();
            foreach (var file in files)
            {
                Strategies.Add(new StrategyItem
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    FilePath = file
                });
            }
        }

        private void LoadServices()
        {
            Services.Clear();
            string listFile = Path.Combine(_zapret.WorkingDirectory, "lists", "list-general-user.txt");

            if (File.Exists(listFile))
            {
                var lines = File.ReadAllLines(listFile);
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                    {
                        Services.Add(new ServiceItem { Name = line, Domain = line });
                    }
                }
            }
        }

        private void AddService(string? domain)
        {
            if (string.IsNullOrWhiteSpace(domain)) return;

            domain = domain.Trim().ToLower();
            if (!Services.Any(s => s.Domain == domain))
            {
                Services.Add(new ServiceItem { Name = domain, Domain = domain });
                SaveServices();
                RestartActiveStrategy();
            }
        }

        private void RemoveService(ServiceItem? service)
        {
            if (service == null) return;
            Services.Remove(service);
            SaveServices();
            RestartActiveStrategy();
        }

        private void SaveServices()
        {
            string listsDir = Path.Combine(_zapret.WorkingDirectory, "lists");
            if (!Directory.Exists(listsDir)) Directory.CreateDirectory(listsDir);

            string listFile = Path.Combine(listsDir, "list-general-user.txt");
            var lines = Services.Select(s => s.Domain).ToList();
            File.WriteAllLines(listFile, lines);
        }

        private void RestartActiveStrategy()
        {
            if (_activeStrategy != null)
            {
                Connect(_activeStrategy);
            }
        }

        private async void Connect(StrategyItem strategy)
        {
            if (strategy == null) return;

            // Сохраняем выбор
            try
            {
                string configPath = Path.Combine(_zapret.WorkingDirectory, "last_strategy.txt");
                File.WriteAllText(configPath, strategy.Name);
            }
            catch { }

            StatusText = "Stopping previous...";
            _zapret.Stop();
            foreach (var s in Strategies) s.IsActive = false;
            await Task.Delay(500);

            StatusText = $"Starting: {strategy.Name}...";
            strategy.IsActive = true;
            _activeStrategy = strategy;

            bool ok = await _zapret.StartAsync(strategy.FilePath);

            if (ok)
            {
                IsConnected = true;
                StatusText = $"Active: {strategy.Name}";
            }
            else
            {
                strategy.IsActive = false;
                StatusText = "Start failed";
            }
        }

        private void Stop()
        {
            _zapret.Stop();
            foreach (var s in Strategies) s.IsActive = false;
            IsConnected = false;
            _activeStrategy = null;
            StatusText = "Not connected";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}