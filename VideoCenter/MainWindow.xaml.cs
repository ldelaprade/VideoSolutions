using LibVLCSharp.Shared;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

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
        private double _lastLeft;
        private double _lastTop;
        private double _lastWidth;
        private double _lastHeight;

        public class VideoBookmark
        {
            public long Time { get; set; }
            public string? Description { get; set; }
            public string DisplayText => $"{TimeSpan.FromMilliseconds(Time):hh\\:mm\\:ss\\.fff} {(string.IsNullOrWhiteSpace(Description) ? "" : "- " + Description)}";
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

            // Check for command-line argument (video file path and window state)
            string[] args = Environment.GetCommandLineArgs();
            var toFullScreen = false;
            if (args.Length > 1 && File.Exists(args[1]))
            {
                // Restore window state if provided
                if (args.Length >= 7)
                {
                    toFullScreen = (args[6] == "1");

                    if (double.TryParse(args[2], out double left)) _lastLeft = left;
                    if (double.TryParse(args[3], out double top)) _lastTop = top;
                    if (double.TryParse(args[4], out double width)) _lastWidth = width;
                    if (double.TryParse(args[5], out double height)) _lastHeight = height;

                    this.Left = _lastLeft;
                    this.Top = _lastTop;
                    this.Width = _lastWidth;
                    this.Height = _lastHeight;

                }
                OpenVideo(args[1]);
            }

            // Initialize with current values
            _lastLeft = this.Left;
            _lastTop = this.Top;
            _lastWidth = this.Width;
            _lastHeight = this.Height;

            this.LocationChanged += MainWindow_LocationOrSizeChanged;
            this.SizeChanged += MainWindow_LocationOrSizeChanged;

            if (toFullScreen) ToFullscreen();
        }

        private void MainWindow_LocationOrSizeChanged(object? sender, EventArgs e)
        {
            if (!_isFullscreen && this.WindowState == WindowState.Normal)
            {
                _lastLeft = this.Left;
                _lastTop = this.Top;
                _lastWidth = this.Width;
                _lastHeight = this.Height;
            }
        }

        private void OpenVideo(string videoPath)
        {

            if (!File.Exists(videoPath))
            {
                SimpleDialog.Info("Video file not found.");
                return;
            }

            VideoPathText.Text = videoPath;

            // Load bookmarks if a .bmk file exists
            string bookmarksFile = videoPath + ".bmk";
            _bookmarks.Clear();
            if (File.Exists(bookmarksFile))
            {
                try
                {
                    var lines = File.ReadAllLines(bookmarksFile);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('|');
                        if (long.TryParse(parts[0], out long time))
                        {
                            var desc = parts.Length > 1 ? parts[1] : "";
                            _bookmarks.Add(new VideoBookmark { Time = time, Description = desc });
                        }
                    }
                }
                catch (Exception ex)
                {
                    SimpleDialog.Warning($"Failed to load bookmarks:\n{ex.Message}");
                }
            }

            try
            {
                var media = new Media(_libVLC, videoPath, FromType.FromPath, ":video-background=0xFFFFFF", ":video-foreground=0xFFFFFF");
                _mediaPlayer.Play(media);
            }
            catch (Exception ex)
            {
                // Handle VLC playback errors
                SimpleDialog.Error($"Error playing video: {ex.Message}");
            }

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
                if (_mediaPlayer.Media == null)
                {
                    OpenVideo(dialog.FileName);
                }
                else
                {
                    // Start a new instance of the app with the selected file as argument
                    // Because closing current video makes VLC crash sometimes (looks like it is impossible to cleanly avoid VLC deadlocks)
                    string videoPath = dialog.FileName;
                    string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!;

                    // Gather window state
                    var left = _lastLeft;
                    var top = _lastTop;
                    var width = _lastWidth;
                    var height = _lastHeight;
                    var isFullscreen = _isFullscreen ? "1" : "0";

                    // Use ProcessStartInfo for better control (quotes for spaces in path)
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = $"\"{videoPath}\" {left} {top} {width} {height} {isFullscreen}"

                    };
                    System.Diagnostics.Process.Start(psi);

                    // Shutdown current app
                    Thread.Sleep(1000); // Wait for the new instance to start
                    Application.Current.Shutdown();
                }
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer.Media != null) _mediaPlayer.Play();
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
            try
            {
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        if (!_isDraggingSeekBar)
                        {
                            SeekBar.Value = e.Time;
                        }
                        UpdateTimeText();
                    }
                    catch (Exception ex)
                    {
                        // Optionally log or handle UI thread exceptions here
                        SimpleDialog.Warning($"Error updating time: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                // Optionally log or handle Dispatcher.Invoke exceptions here
                SimpleDialog.Error($"Dispatcher error: {ex.Message}");
            }
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

        private void ToFullscreen()
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
        private void FullscreenButton_Click(object sender, RoutedEventArgs e)
        {
            ToFullscreen();
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

            SimpleDialog.Info(licenseText);
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
                        SimpleDialog.Warning($"A bookmark at {TimeSpan.FromMilliseconds(currentTime):hh\\:mm\\:ss} already exists.", this, sender as FrameworkElement);
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


        private void EditBookmarkItem(VideoBookmark bookmark)
        {
            var initialTime = TimeSpan.FromMilliseconds(bookmark.Time);
            var dialog = new EditBookmarkDialog(initialTime, bookmark.Description) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                bookmark.Time = (long)dialog.BookmarkTime.TotalMilliseconds;
                bookmark.Description = dialog.BookmarkDescription;
                ReorderAndDeduplicateBookmarks();
                BookmarksListBox.Items.Refresh();
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
                        if (SimpleDialog.Confirm($"Remove bookmark at {bookmark.DisplayText}?", this, item as FrameworkElement) == true)
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
                        EditBookmarkItem(bookmark);
                    }
                };

                contextMenu.Items.Add(removeMenuItem);
                contextMenu.Items.Add(editMenuItem);

                // Show the context menu at the mouse position
                contextMenu.IsOpen = true;
                e.Handled = true;
            }
        }

        private void BookmarksListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (BookmarksListBox.SelectedItem is VideoBookmark bookmark)
            {
                EditBookmarkItem(bookmark);
            }
        }

        private void BookmarksListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && BookmarksListBox.SelectedItem is VideoBookmark bookmark)
            {
                if (SimpleDialog.Confirm($"Remove bookmark at {bookmark.DisplayText}?") == true)
                {
                    _bookmarks.Remove(bookmark);
                }
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
                SimpleDialog.Info("No video is currently loaded.", this, sender as FrameworkElement);
                return;
            }

            // Build the bookmarks file path
            string bookmarksFile = videoPath + ".bmk";

            try
            {
                // Save each bookmark as: time|description
                var lines = new List<string>();
                foreach (var bm in _bookmarks)
                {
                    lines.Add($"{bm.Time}|{bm.Description ?? ""}");
                }
                File.WriteAllLines(bookmarksFile, lines, Encoding.UTF8);
                SimpleDialog.Info($"Bookmarks saved to:\n{bookmarksFile}", this, sender as FrameworkElement);
            }
            catch (Exception ex)
            {
                SimpleDialog.Error($"Failed to save bookmarks:\n{ex.Message}", this, sender as FrameworkElement);
            }

        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (_isFullscreen && e.Key == Key.Escape)
            {
                ToFullscreen(); // This toggles fullscreen off
                e.Handled = true;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
