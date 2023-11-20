using Avalonia.Controls;

namespace MathCalc.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Main_Closing(object sender, WindowClosingEventArgs e)
        {
            MainView.Server?.Stop();
            MainView.Client?.Disconnect();
        }
    }
}