using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;

namespace VideoCenter
{
    public partial class SimpleDialog : Window
    {


        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public bool? Result { get; set; }

        // Make the constructor private
        private SimpleDialog(string message, bool showCancel = false, FrameworkElement? anchor = null)
        {
            InitializeComponent();
            CancelButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;

            // Set message with clickable links
            SetMessageWithLinks(message);

            // Positioning logic
            Loaded += (s, e) =>
            {
                RemoveCloseButton();

                if (anchor != null)
                {
                    var point = anchor.PointToScreen(new Point(0, anchor.ActualHeight));
                    Left = point.X;
                    Top = point.Y;
                }
                else if (Application.Current?.MainWindow is Window main)
                {
                    Left = main.Left + (main.Width - ActualWidth) / 2;
                    Top = main.Top + (main.Height - ActualHeight) / 2;
                }
            };
            WindowStartupLocation = WindowStartupLocation.Manual;
        }

        private void RemoveCloseButton()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            const int GWL_STYLE = -16;
            const int WS_SYSMENU = 0x80000;
            int style = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, style & ~WS_SYSMENU);
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

        // Static methods for showing the dialog

        public static bool? Show(string title, string message, bool isConfirmation, Window? owner, FrameworkElement? placementTarget)
        {
            var dialog = new SimpleDialog(message, isConfirmation, placementTarget);
            dialog.Title = title;

            if (owner != null)
                dialog.Owner = owner;

            return dialog.ShowDialog();
        }

        public static bool? Info(string message, Window? owner = null, FrameworkElement? placementTarget = null)
        {
            return Show("Information", message, false, owner, placementTarget);
        }

        public static bool? Warning(string message, Window? owner = null, FrameworkElement? placementTarget = null)
        {
            return Show("Warning", message, false, owner, placementTarget);
        }

        public static bool? Error(string message, Window? owner = null, FrameworkElement? placementTarget = null)
        {
            return Show("Error", message, false, owner, placementTarget);
        }

        public static bool? Confirm(string message, Window? owner = null, FrameworkElement? placementTarget = null)
        {
            return Show("Please confirm", message, true, owner, placementTarget);
        }
    }
}
