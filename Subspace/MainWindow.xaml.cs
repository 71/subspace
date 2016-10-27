using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;

namespace Subspace
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public Dictionary<string, string> Languages
        {
            get { return (Dictionary<string, string>)GetValue(LanguagesProperty); }
            set { SetValue(LanguagesProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Languages.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LanguagesProperty =
            DependencyProperty.Register("Languages", typeof(Dictionary<string, string>), typeof(MainWindow), new PropertyMetadata(null));


        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Message.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(string), typeof(MainWindow), new PropertyMetadata("Hello world"));



        public bool IsLoading
        {
            get { return (bool)GetValue(IsLoadingProperty); }
            set { SetValue(IsLoadingProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsLoading.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register("IsLoading", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

        public OpenSubtitlesClient Client { get; private set; }

        public MainWindow()
        {
            InitializeComponent();

            this.Languages = new Dictionary<string, string>
            {
                { "en", "English" },
                { "fr", "French" },
                { "de", "German" },
                { "es", "Spanish" },
                
                { "fi", "Finnish" },
                { "pl", "Polish" },
                { "sv", "Swedish" },
                { "ru", "Russian" },
                { "el", "Greek" },
                { "it", "Italian" },
                { "da", "Danish" },
                { "tr", "Turkish" },

                { "jp", "Japanese" },
                { "ch", "Chinese" }
            };
            
            this.Message = "Drag and Drop files here.";
            this.DataContext = this;
        }

        private async Task<bool> EnsureClientExists()
        {
            if (Client == null)
            {
                try
                {
                    Client = await OpenSubtitlesClient.LogIn();
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            else
                return true;
        }

        private void Error(string msg)
        {
            Message = msg;
            TaskbarInfo.ProgressState = TaskbarItemProgressState.Error;
            IsLoading = false;
        }

        private void Success(string msg)
        {
            Message = msg;
            TaskbarInfo.ProgressState = TaskbarItemProgressState.None;
            IsLoading = false;
        }

        private async void Border_Drop(object sender, DragEventArgs e)
        {
            DropZone.DraggingOver = false;
            TaskbarInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
            IsLoading = true;
            DropBorder.AllowDrop = false;

            if (!await EnsureClientExists())
            {
                Error("Server error: Couldn't connect to OpenSubtitles server.");
                return;
            }
            
            try
            {
                string lang = ((KeyValuePair<string, string>)LanguageBox.SelectedItem).Key;
                int errors = 0;

                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                foreach (string file in files)
                {
                    string friendlyName = Path.GetFileNameWithoutExtension(file);

                    Message = $"Searching subtitles for {friendlyName}...";
                    var subs = await Client.GetSubtitlesFromAll(file, lang, false);

                    if (subs.FirstOrDefault() == null)
                    {
                        errors++;
                        continue;
                    }

                    Message = $"Downloading subtitles for {friendlyName}...";
                    await Task.Run(
                        async () => File.WriteAllBytes(
                            Path.ChangeExtension(file, "srt"),
                            await Client.RetrieveSubtitle(subs.First())
                        )
                    );
                }

                if (errors > 1)
                    Error($"Couldn't find subtitles for {errors} files.");
                else if (errors == 1)
                    Error($"Couldn't find subtitles for one file.");
                else
                    Success(files.Length == 1 ? $"One subtitle loaded." : $"{files.Length} subtitles loaded.");
            }
            catch (Exception ex)
            {
                Error("Internal error: " + ex.Message);
            }

            DropBorder.AllowDrop = true;
        }

        private bool Correct(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                foreach (string file in (string[])e.Data.GetData(DataFormats.FileDrop))
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext != ".mp4" && ext != ".mkv" && ext != ".mov" && ext != ".avi")
                    {
                        return false;
                    }
                }

                e.Effects = e.AllowedEffects;
                return true;
            }
            else
                return false;
        }

        private void Border_DragEnter(object sender, DragEventArgs e)
        {
            if (Correct(e))
            {
                DropZone.DraggingOver = true;
                DropBorder.AllowDrop = true;
            }
            else
            {
                DropBorder.AllowDrop = false;
            }
        }

        private void Border_DragLeave(object sender, DragEventArgs e)
        {
            DropZone.DraggingOver = false;
            DropBorder.AllowDrop = false;

            Task.Run(async () => {
                await Task.Delay(80);

                Point cp = GetCursorPosition();

                Dispatcher.Invoke(() =>
                {
                    if (cp.Y < this.Top || cp.Y > this.Top + this.ActualHeight
                        || cp.X < this.Left || cp.X > this.Left + this.ActualWidth)
                        DropBorder.AllowDrop = true;
                    else
                        Border_DragLeave(sender, e);
                });
            });
        }

        #region GetCursorPosition
        /// <summary>
        /// Struct representing a point.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public static implicit operator Point(POINT point)
            {
                return new Point(point.X, point.Y);
            }
        }

        /// <summary>
        /// Retrieves the cursor's position, in screen coordinates.
        /// </summary>
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        public static Point GetCursorPosition()
        {
            POINT lpPoint;
            GetCursorPos(out lpPoint);
            return lpPoint;
        }
        #endregion
    }
}
