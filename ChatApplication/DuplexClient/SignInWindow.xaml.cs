using System;
using System.ServiceModel;
using System.Windows;
using SharedLibrary;

namespace DuplexClient
{
    public partial class SignInWindow : Window
    {
        public SignInWindow()
        {
            InitializeComponent();
        }

        private void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            string userId = UserIdTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(userId))
            {
                MessageBox.Show("Enter a User ID.", "Sign In", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                MainWindow mainWindow = new MainWindow(userId);
                mainWindow.Show();
                Close();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Sign In Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (TimeoutException)
            {
                MessageBox.Show("The server did not respond in time.", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show(ex.Message, "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
