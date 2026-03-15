namespace Luso {
    public partial class App : Application {
        public App() {
            InitializeComponent();

            // Force dark mode globally (Fluent 2 dark theme by default)
            UserAppTheme = AppTheme.Dark;

            MainPage = new AppShell();
        }
    }
}