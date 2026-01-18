using GameSaveManager.Core.Services;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GameSaveManager
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? m_window;

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            var settingsService = new SettingsService();
            await settingsService.LoadAsync();

            m_window = new MainWindow();
            System.Diagnostics.Debug.WriteLine($"[BOOT] Window = {m_window.GetType().FullName}");
            m_window.Activate();
        }
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>

    }
}
//test 