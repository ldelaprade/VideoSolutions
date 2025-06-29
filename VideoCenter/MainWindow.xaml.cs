using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using System.Windows.Threading;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace VideoCenter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private DispatcherTimer _timer;
        private bool _isDraggingSeekBar = false;
        private bool _isFullscreen = false;
        private bool _isPaused = false;
        private bool _videoEndReached = false;
        private WindowState _previousWindowState;
        private WindowStyle _previousWindowStyle;
        private ResizeMode _previousResizeMode;

        public class VideoBookmark
        {
            public long Time { get; set; }
            public string DisplayText => TimeSpan.FromMilliseconds(Time).ToString(@"hh\:mm\:ss\.fff");
        }

        private ObservableCollection<VideoBookmark> _bookmarks = new();

        public MainWindow()
        {
            InitializeComponent();

            var version = Assembly.GetExecutingAssembly().GetName().Version;

            this.Title = version != null
                ? $"Video Center {version.Major}.{version.Minor}"
                : "Video Center ?.?";

            // Preferred path to the VLC native libraries
            var preferredLibVlcPath = Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64");

            // Fallback path (base directory)
            var fallbackLibVlcPath = AppContext.BaseDirectory;

            // Determine which path to use
            string libVlcPathToUse = Directory.Exists(preferredLibVlcPath) && File.Exists(Path.Combine(preferredLibVlcPath, "libvlc.dll")) ?
                preferredLibVlcPath
                :
                fallbackLibVlcPath;

            // Initialize LibVLC with the selected path
            Core.Initialize(libVlcPathToUse);

            _libVLC = new LibVLC();

            _mediaPlayer = new MediaPlayer(_libVLC);

            videoView.MediaPlayer = _mediaPlayer;

            // Set initial volume to 0 for both MediaPlayer and Slider
            _mediaPlayer.Volume = 0;
            VolumeSlider.Value = 0;

            _mediaPlayer.LengthChanged += MediaPlayer_LengthChanged;
            _mediaPlayer.TimeChanged += MediaPlayer_TimeChanged;
            _mediaPlayer.EndReached += MediaPlayer_EndReached;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(500);
            _timer.Tick += Timer_Tick;
            _timer.Start();

            BookmarksListBox.ItemsSource = _bookmarks;
        }

        private void MediaPlayer_EndReached(object? sender, EventArgs e)
        {
            _videoEndReached = true;
        }

        private void RefreshButtonsState()
        {
            PauseButton.Content = _isPaused ? "Resume" : "Pause";
            FullscreenButton.Content = _isFullscreen ? "Exit fullscreen" : "Fullscreen";

            PlayButton.IsEnabled = (_mediaPlayer.Media != null);
            PauseButton.IsEnabled = (_mediaPlayer.Media != null);
            StopButton.IsEnabled = (_mediaPlayer.Media != null);
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Video Files|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.ts|All Files|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                _mediaPlayer.Stop();
                using var media = new Media(_libVLC, dialog.FileName, FromType.FromPath);
                _mediaPlayer.Play(media);
                VideoPathText.Text = dialog.FileName; // Update the video path display

                // Load bookmarks if a .bmk file exists
                string bookmarksFile = dialog.FileName + ".bmk";
                _bookmarks.Clear();
                if (File.Exists(bookmarksFile))
                {
                    try
                    {
                        var lines = File.ReadAllLines(bookmarksFile);
                        foreach (var line in lines)
                        {
                            if (long.TryParse(line, out long time))
                            {
                                _bookmarks.Add(new VideoBookmark { Time = time });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to load bookmarks:\n{ex.Message}", "Load Bookmarks", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer.Media != null)
                _mediaPlayer.Play();
            _isPaused = false;
            _videoEndReached = false;
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer.CanPause && !_videoEndReached)
            {
                _mediaPlayer.Pause();
                _isPaused = !_isPaused;
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayer.Stop();
            _isPaused = false;
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_mediaPlayer != null)
                _mediaPlayer.Volume = (int)VolumeSlider.Value;
        }

        private void MediaPlayer_LengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                SeekBar.Maximum = e.Length;
            });
        }

        private void MediaPlayer_TimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_isDraggingSeekBar)
                {
                    SeekBar.Value = e.Time;
                }
                UpdateTimeText();
            });
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_mediaPlayer.Media != null && !_isDraggingSeekBar)
            {
                SeekBar.Value = _mediaPlayer.Time;
                UpdateTimeText();
            }
            RefreshButtonsState();
        }

        private void SeekBar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSeekBar = true;
        }

        private void SeekBar_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_mediaPlayer.Media != null)
            {
                _mediaPlayer.Time = (long)SeekBar.Value;
            }
            _isDraggingSeekBar = false;
        }

        private void FullscreenButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isFullscreen)
            {
                _previousWindowState = WindowState;
                _previousWindowStyle = WindowStyle;
                _previousResizeMode = ResizeMode;
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                ResizeMode = ResizeMode.NoResize;
                _isFullscreen = true;
            }
            else
            {
                WindowStyle = _previousWindowStyle;
                WindowState = _previousWindowState;
                ResizeMode = _previousResizeMode;
                _isFullscreen = false;
            }
            // Force redraw
            this.InvalidateVisual();
            this.UpdateLayout();
        }

        private void UpdateTimeText()
        {
            var current = TimeSpan.FromMilliseconds(_mediaPlayer.Time);
            var total = TimeSpan.FromMilliseconds(_mediaPlayer.Length);
            TimeText.Text = $"{current:hh\\:mm\\:ss} / {total:hh\\:mm\\:ss}";
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            string licensePath = Path.Combine(AppContext.BaseDirectory, "VLC-LICENSE.txt");
            string licenseText;

            try
            {
                licenseText = File.ReadAllText(licensePath);
            }
            catch
            {
                licenseText = "VLC-LICENSE.txt not found.";
            }

            var dialog = new SimpleDialog(licenseText, false, sender as FrameworkElement) {  Owner = this  };
            dialog.ShowDialog();

        }

        private void AddBookmarkButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer != null && _mediaPlayer.Media != null)
            {
                long currentTime = _mediaPlayer.Time;

                // Create new bookmark
                var newBookmark = new VideoBookmark { Time = currentTime };

                // Check for duplicate
                foreach (var bm in _bookmarks)
                {
                    if (bm.Time == newBookmark.Time)
                    {
                        var dialog = new SimpleDialog($"A bookmark at {TimeSpan.FromMilliseconds(currentTime):hh\\:mm\\:ss} already exists.", 
                            false, sender as FrameworkElement) { Owner = this };
                        dialog.ShowDialog();
                        return;
                    }
                }

                // Find the correct index to insert (keep list sorted by Time)
                int insertIndex = 0;
                while (insertIndex < _bookmarks.Count && _bookmarks[insertIndex].Time < newBookmark.Time)
                {
                    insertIndex++;
                }
                _bookmarks.Insert(insertIndex, newBookmark);
            }
        }

        private void BookmarksListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (BookmarksListBox.SelectedItem is VideoBookmark bookmark)
            {
                if (!_isPaused)
                { 
                    _mediaPlayer.Stop();
                    _mediaPlayer.Play();
                    _videoEndReached = false;
                }
                _mediaPlayer.Time = bookmark.Time;
            }
        }

        private void BookmarksListBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement(BookmarksListBox, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null)
            {
                BookmarksListBox.SelectedItem = item.DataContext;

                var contextMenu = new ContextMenu();

                var removeMenuItem = new MenuItem { Header = "Remove Bookmark" };
                removeMenuItem.Click += (s, args) =>
                {
                    if (item.DataContext is VideoBookmark bookmark)
                    {
                        var dialog = new SimpleDialog($"Remove bookmark at {bookmark.DisplayText}?", true, item as FrameworkElement) { Owner = this };
                        if (dialog.ShowDialog() == true)
                        {
                            _bookmarks.Remove(bookmark);
                        }
                    }
                };

                var editMenuItem = new MenuItem { Header = "Edit Bookmark" };
                editMenuItem.Click += (s, args) =>
                {
                    if (item.DataContext is VideoBookmark bookmark)
                    {
                        var initialTime = TimeSpan.FromMilliseconds(bookmark.Time);
                        var dialog = new EditBookmarkDialog(initialTime) { Owner = this };
                        if (dialog.ShowDialog() == true)
                        {
                            bookmark.Time = (long)dialog.BookmarkTime.TotalMilliseconds;
                            ReorderAndDeduplicateBookmarks();
                            BookmarksListBox.Items.Refresh();
                        }
                    }
                };

                contextMenu.Items.Add(removeMenuItem);
                contextMenu.Items.Add(editMenuItem);

                // Show the context menu at the mouse position
                contextMenu.IsOpen = true;
                e.Handled = true;
            }
        }

        private void ReorderAndDeduplicateBookmarks()
        {
            // Order by Time and remove duplicates (keep first occurrence)
            var orderedDistinct = new ObservableCollection<VideoBookmark>();
            var seenTimes = new HashSet<long>();

            foreach (var bm in _bookmarks)
            {
                if (seenTimes.Add(bm.Time))
                {
                    orderedDistinct.Add(bm);
                }
            }

            // Sort the collection
            var sorted = new ObservableCollection<VideoBookmark>(orderedDistinct.OrderBy(b => b.Time));

            // Update the original collection
            _bookmarks.Clear();
            foreach (var bm in sorted)
                _bookmarks.Add(bm);
        }

        private void SaveBookmarksButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the current video path from the UI
            string videoPath = VideoPathText.Text;
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                MessageBox.Show("No video is currently loaded.", "Save Bookmarks", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Build the bookmarks file path
            string bookmarksFile = videoPath + ".bmk";

            try
            {
                // Save each bookmark time (in milliseconds) as a line
                var lines = new List<string>();
                foreach (var bm in _bookmarks)
                {
                    lines.Add(bm.Time.ToString());
                }
                File.WriteAllLines(bookmarksFile, lines, Encoding.UTF8);

                MessageBox.Show($"Bookmarks saved to:\n{bookmarksFile}", "Save Bookmarks", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save bookmarks:\n{ex.Message}", "Save Bookmarks", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}