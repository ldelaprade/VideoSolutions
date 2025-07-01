using System;
using System.Windows;

namespace VideoCenter
{
    public partial class EditBookmarkDialog : Window
    {
        public TimeSpan BookmarkTime { get; private set; }
        public string BookmarkDescription { get; set; }

        public EditBookmarkDialog(TimeSpan initialTime, string? initialDescription)
        {
            InitializeComponent();
            Hours.Value = initialTime.Hours;
            Minutes.Value = initialTime.Minutes;
            Seconds.Value = initialTime.Seconds;
            Millis.Value = initialTime.Milliseconds;
            BookmarkDescription = initialDescription ?? "";
            DataContext = this;

        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            BookmarkTime = new TimeSpan(0, // days are not used in bookmarks
                (int)Hours.Value.GetValueOrDefault(),
                (int)Minutes.Value.GetValueOrDefault(),
                (int)Seconds.Value.GetValueOrDefault(),
                (int)Millis.Value.GetValueOrDefault());
            DialogResult = true;
        }
    }
}