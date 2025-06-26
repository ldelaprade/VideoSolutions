using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace VideoCenter
{
    public partial class SimpleDialog : Window
    {
        public bool? Result { get; private set; }

        public SimpleDialog(string message, bool showCancel = false, FrameworkElement? anchor = null)
        {
            InitializeComponent();
            CancelButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;

            // Set message with clickable links
            SetMessageWithLinks(message);

            // Positioning logic
            WindowStartupLocation = WindowStartupLocation.Manual;
            if (anchor != null)
            {
                // Try to position below the anchor element
                var point = anchor.PointToScreen(new Point(0, anchor.ActualHeight));
                Left = point.X;
                Top = point.Y;
            }
            else if (Application.Current?.MainWindow is Window main)
            {
                // Center in main window
                Left = main.Left + (main.Width - Width) / 2;
                Top = main.Top + (main.Height - Height) / 2;
            }
        }

        private void SetMessageWithLinks(string message)
        {
            MessageTextBlock.Inlines.Clear();
            // Regex for URLs (http/https)
            var urlRegex = new Regex(@"(https?://[^\s]+)", RegexOptions.IgnoreCase);
            int lastPos = 0;
            foreach (Match match in urlRegex.Matches(message))
            {
                // Add text before the URL
                if (match.Index > lastPos)
                {
                    MessageTextBlock.Inlines.Add(new Run(message.Substring(lastPos, match.Index - lastPos)));
                }
                // Add the URL as a Hyperlink
                var link = new Hyperlink(new Run(match.Value))
                {
                    NavigateUri = new System.Uri(match.Value)
                };
                link.RequestNavigate += (s, e) =>
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                    }
                    catch { }
                };
                MessageTextBlock.Inlines.Add(link);
                lastPos = match.Index + match.Length;
            }
            // Add any remaining text after the last URL
            if (lastPos < message.Length)
            {
                MessageTextBlock.Inlines.Add(new Run(message.Substring(lastPos)));
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            DialogResult = false;
        }

        public static bool? ShowDialog(string licenseText, FrameworkElement sender, Window owner)
        {
            var dialog = new SimpleDialog(licenseText, false, sender)
            {
                Owner = owner
            };
            dialog.ShowDialog();
            return dialog.Result;
        }
    }
}
