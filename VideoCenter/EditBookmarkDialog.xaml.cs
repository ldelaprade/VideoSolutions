using System;
using System.Windows;

namespace VideoCenter
{
    public partial class EditBookmarkDialog : Window
    {
        public TimeSpan BookmarkTime { get; private set; }

        public EditBookmarkDialog(TimeSpan initialTime)
        {
            InitializeComponent();
            Hours.Value = initialTime.Hours;
            Minutes.Value = initialTime.Minutes;
            Seconds.Value = initialTime.Seconds;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            BookmarkTime = new TimeSpan(
                (int)Hours.Value.GetValueOrDefault(),
                (int)Minutes.Value.GetValueOrDefault(),
                (int)Seconds.Value.GetValueOrDefault());
            DialogResult = true;
        }
    }
}