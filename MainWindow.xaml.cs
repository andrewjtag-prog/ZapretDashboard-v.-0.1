using System.Windows;
using ZapretDashboard.ViewModels;

namespace ZapretDashboard
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        // Обработчик для кнопки "Strategies"
        private void Menu_Strategies(object sender, RoutedEventArgs e)
        {
            if (StrategiesPanel != null && ServicesPanel != null)
            {
                StrategiesPanel.Visibility = Visibility.Visible;
                ServicesPanel.Visibility = Visibility.Collapsed;
            }
        }

        // Обработчик для кнопки "Services"
        private void Menu_Services(object sender, RoutedEventArgs e)
        {
            if (StrategiesPanel != null && ServicesPanel != null)
            {
                StrategiesPanel.Visibility = Visibility.Collapsed;
                ServicesPanel.Visibility = Visibility.Visible;
            }
        }
    }
}