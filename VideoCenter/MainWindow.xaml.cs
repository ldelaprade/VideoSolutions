using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using Microsoft.Win32;
using System;
using System.IO;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Collections.ObjectModel;

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
        private WindowState _previousWindowState;
        private WindowStyle _previousWindowStyle;
        private ResizeMode _previousResizeMode;

        public class VideoBookmark
        {
            public long Time { get; set; }
            public string DisplayText => TimeSpan.FromMilliseconds(Time).ToString(@"hh\:mm\:ss");
        }

        private ObservableCollection<VideoBookmark> _bookmarks = new();

        public MainWindow()
        {
            InitializeComponent();
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

            //_libVLC = new LibVLC("--video-background=0x000000"); // Black background
            //_libVLC = new LibVLC("--no - audio");
            _libVLC = new LibVLC();

            _mediaPlayer = new MediaPlayer(_libVLC);

            videoView.MediaPlayer = _mediaPlayer;

            _mediaPlayer.LengthChanged += MediaPlayer_LengthChanged;
            _mediaPlayer.TimeChanged += MediaPlayer_TimeChanged;

            VolumeSlider.Value = _mediaPlayer.Volume;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(500);
            _timer.Tick += Timer_Tick;
            _timer.Start();

            BookmarksListBox.ItemsSource = _bookmarks;
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
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer.Media != null)
                _mediaPlayer.Play();
            _isPaused = false;
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer.CanPause)
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

        private void MediaPlayer_LengthChanged(object sender, MediaPlayerLengthChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                SeekBar.Maximum = e.Length;
            });
        }

        private void MediaPlayer_TimeChanged(object sender, MediaPlayerTimeChangedEventArgs e)
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

        private void Timer_Tick(object sender, EventArgs e)
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
            string licensePath = "VLC-LICENSE.txt";
            string licenseText;

            try
            {
                licenseText = File.ReadAllText(licensePath);
            }
            catch
            {
                licenseText = "VLC-LICENSE.txt not found.";
            }

            MessageBox.Show(licenseText, "About Video Center", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddBookmarkButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer != null && _mediaPlayer.Media != null)
            {
                var bookmark = new VideoBookmark { Time = _mediaPlayer.Time };
                _bookmarks.Add(bookmark);
            }
        }

        private void BookmarksListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (BookmarksListBox.SelectedItem is VideoBookmark bookmark)
            {
                _mediaPlayer.Time = bookmark.Time;
            }
        }
    }
}