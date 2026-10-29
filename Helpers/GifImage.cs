using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.InteropServices;

namespace ZapretDashboard.Helpers
{
    public class GifImage : System.Windows.Controls.Image
    {
        private List<BitmapSource> _frames = new();
        private DispatcherTimer? _timer;
        private int _currentFrame;

        public static readonly DependencyProperty GifSourceProperty =
            DependencyProperty.Register("GifSource", typeof(string), typeof(GifImage), new UIPropertyMetadata(null, GifSourcePropertyChanged));

        public string GifSource
        {
            get { return (string)GetValue(GifSourceProperty); }
            set { SetValue(GifSourceProperty, value); }
        }

        private static void GifSourcePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            (sender as GifImage)?.Initialize();
        }

        private void Initialize()
        {
            if (string.IsNullOrEmpty(GifSource)) return;

            try
            {
                var uri = new Uri("pack://application:,,," + GifSource);
                var stream = Application.GetResourceStream(uri)?.Stream;
                if (stream == null) return;

                using var gif = new Bitmap(stream);
                var dimension = new FrameDimension(gif.FrameDimensionsList[0]);
                int frameCount = gif.GetFrameCount(dimension);

                // Загружаем все кадры заранее
                for (int i = 0; i < frameCount; i++)
                {
                    gif.SelectActiveFrame(dimension, i);
                    var hBitmap = gif.GetHbitmap();
                    var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

                    bitmapSource.Freeze();
                    _frames.Add(bitmapSource);
                    DeleteObject(hBitmap);
                }

                // Принудительно ставим 60 FPS (16 мс)
                _timer = new DispatcherTimer(DispatcherPriority.Render);
                _timer.Interval = TimeSpan.FromMilliseconds(16);
                _timer.Tick += (s, e) => NextFrame();
                _timer.Start();

                Source = _frames[0];
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GIF: {ex.Message}");
            }
        }

        private void NextFrame()
        {
            if (_frames.Count == 0) return;
            _currentFrame = (_currentFrame + 1) % _frames.Count;
            Source = _frames[_currentFrame];
        }

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
    }
}