using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Point = System.Windows.Point;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventHandler = System.Windows.Input.MouseEventHandler;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace DesktopVideoWallpaper
{
    public partial class CalibrationWindow : Window
    {
        private readonly string _backgroundImagePath;
        private WallpaperPreset _preset;

        private Point _p0;
        private Point _p1;
        private Point _p2;
        private Point _p3;

        private int _selectedThumbIndex = -1;
        private bool _is3D = false;

        public double ResultX { get; private set; }
        public double ResultY { get; private set; }
        public double ResultWidth { get; private set; }
        public double ResultHeight { get; private set; }

        public double ResultX0 { get; private set; }
        public double ResultY0 { get; private set; }
        public double ResultX1 { get; private set; }
        public double ResultY1 { get; private set; }
        public double ResultX2 { get; private set; }
        public double ResultY2 { get; private set; }
        public double ResultX3 { get; private set; }
        public double ResultY3 { get; private set; }
        public bool ResultIs3D { get; private set; }

        public event EventHandler<PositionChangedEventArgs>? PositionChanged;

        private void OnPositionChanged()
        {
            double canvasWidth = SystemParameters.PrimaryScreenWidth;
            double canvasHeight = SystemParameters.PrimaryScreenHeight;

            // Bounding box
            double minX = Math.Min(Math.Min(_p0.X, _p1.X), Math.Min(_p2.X, _p3.X));
            double maxX = Math.Max(Math.Max(_p0.X, _p1.X), Math.Max(_p2.X, _p3.X));
            double minY = Math.Min(Math.Min(_p0.Y, _p1.Y), Math.Min(_p2.Y, _p3.Y));
            double maxY = Math.Max(Math.Max(_p0.Y, _p1.Y), Math.Max(_p2.Y, _p3.Y));

            ResultX = minX / canvasWidth;
            ResultY = minY / canvasHeight;
            ResultWidth = (maxX - minX) / canvasWidth;
            ResultHeight = (maxY - minY) / canvasHeight;

            ResultX0 = _p0.X / canvasWidth;
            ResultY0 = _p0.Y / canvasHeight;
            ResultX1 = _p1.X / canvasWidth;
            ResultY1 = _p1.Y / canvasHeight;
            ResultX2 = _p2.X / canvasWidth;
            ResultY2 = _p2.Y / canvasHeight;
            ResultX3 = _p3.X / canvasWidth;
            ResultY3 = _p3.Y / canvasHeight;
            ResultIs3D = _is3D;

            PositionChanged?.Invoke(this, new PositionChangedEventArgs(
                ResultX, ResultY, ResultWidth, ResultHeight,
                ResultX0, ResultY0, ResultX1, ResultY1,
                ResultX2, ResultY2, ResultX3, ResultY3,
                ResultIs3D
            ));
        }

        public CalibrationWindow(string backgroundImagePath, WallpaperPreset preset)
        {
            InitializeComponent();
            _backgroundImagePath = backgroundImagePath;
            _preset = preset;
            _is3D = preset.Is3D;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            double canvasWidth = SystemParameters.PrimaryScreenWidth;
            double canvasHeight = SystemParameters.PrimaryScreenHeight;

            if (_is3D)
            {
                Radio3D.IsChecked = true;
                _p0 = new Point(_preset.X0 * canvasWidth, _preset.Y0 * canvasHeight);
                _p1 = new Point(_preset.X1 * canvasWidth, _preset.Y1 * canvasHeight);
                _p2 = new Point(_preset.X2 * canvasWidth, _preset.Y2 * canvasHeight);
                _p3 = new Point(_preset.X3 * canvasWidth, _preset.Y3 * canvasHeight);
            }
            else
            {
                Radio2D.IsChecked = true;
                double w = _preset.Width * canvasWidth;
                double h = _preset.Height * canvasHeight;
                double left = _preset.X * canvasWidth;
                double top = _preset.Y * canvasHeight;

                if (w < 50) w = 320;
                if (h < 30) h = 180;
                if (left < 0 || left > canvasWidth) left = (canvasWidth - w) / 2;
                if (top < 0 || top > canvasHeight) top = (canvasHeight - h) / 2;

                _p0 = new Point(left, top);
                _p1 = new Point(left + w, top);
                _p2 = new Point(left + w, top + h);
                _p3 = new Point(left, top + h);
            }

            UpdatePolygon();
        }

        private void UpdatePolygon()
        {
            if (SelectionPolygon == null) return;

            SelectionPolygon.Points.Clear();
            SelectionPolygon.Points.Add(_p0);
            SelectionPolygon.Points.Add(_p1);
            SelectionPolygon.Points.Add(_p2);
            SelectionPolygon.Points.Add(_p3);

            Canvas.SetLeft(ThumbP0, _p0.X - 6); Canvas.SetTop(ThumbP0, _p0.Y - 6);
            Canvas.SetLeft(ThumbP1, _p1.X - 6); Canvas.SetTop(ThumbP1, _p1.Y - 6);
            Canvas.SetLeft(ThumbP2, _p2.X - 6); Canvas.SetTop(ThumbP2, _p2.Y - 6);
            Canvas.SetLeft(ThumbP3, _p3.X - 6); Canvas.SetTop(ThumbP3, _p3.Y - 6);

            OnPositionChanged();
        }

        private void SelectionPolygon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source is Polygon poly)
            {
                poly.CaptureMouse();
                Point anchor = e.GetPosition(CalibrationCanvas);

                Point origP0 = _p0;
                Point origP1 = _p1;
                Point origP2 = _p2;
                Point origP3 = _p3;

                MouseEventHandler? moveHandler = null;
                MouseButtonEventHandler? upHandler = null;

                moveHandler = (s, args) =>
                {
                    Point current = args.GetPosition(CalibrationCanvas);
                    double dx = current.X - anchor.X;
                    double dy = current.Y - anchor.Y;

                    _p0 = new Point(origP0.X + dx, origP0.Y + dy);
                    _p1 = new Point(origP1.X + dx, origP1.Y + dy);
                    _p2 = new Point(origP2.X + dx, origP2.Y + dy);
                    _p3 = new Point(origP3.X + dx, origP3.Y + dy);

                    UpdatePolygon();
                };

                upHandler = (s, args) =>
                {
                    poly.ReleaseMouseCapture();
                    poly.MouseMove -= moveHandler;
                    poly.MouseLeftButtonUp -= upHandler;
                };

                poly.MouseMove += moveHandler;
                poly.MouseLeftButtonUp += upHandler;
            }
        }

        private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            var thumb = sender as Thumb;
            if (thumb == null) return;

            int index = int.Parse(thumb.Tag.ToString() ?? "0");
            double dx = e.HorizontalChange;
            double dy = e.VerticalChange;

            double canvasWidth = SystemParameters.PrimaryScreenWidth;
            double canvasHeight = SystemParameters.PrimaryScreenHeight;

            if (_is3D)
            {
                switch (index)
                {
                    case 0: _p0 = new Point(Math.Max(0, Math.Min(canvasWidth, _p0.X + dx)), Math.Max(0, Math.Min(canvasHeight, _p0.Y + dy))); break;
                    case 1: _p1 = new Point(Math.Max(0, Math.Min(canvasWidth, _p1.X + dx)), Math.Max(0, Math.Min(canvasHeight, _p1.Y + dy))); break;
                    case 2: _p2 = new Point(Math.Max(0, Math.Min(canvasWidth, _p2.X + dx)), Math.Max(0, Math.Min(canvasHeight, _p2.Y + dy))); break;
                    case 3: _p3 = new Point(Math.Max(0, Math.Min(canvasWidth, _p3.X + dx)), Math.Max(0, Math.Min(canvasHeight, _p3.Y + dy))); break;
                }
            }
            else
            {
                switch (index)
                {
                    case 0: // Top-Left
                        _p0 = new Point(Math.Max(0, Math.Min(_p1.X - 50, _p0.X + dx)), Math.Max(0, Math.Min(_p3.Y - 30, _p0.Y + dy)));
                        _p1.Y = _p0.Y;
                        _p3.X = _p0.X;
                        break;
                    case 1: // Top-Right
                        _p1 = new Point(Math.Max(_p0.X + 50, Math.Min(canvasWidth, _p1.X + dx)), Math.Max(0, Math.Min(_p2.Y - 30, _p1.Y + dy)));
                        _p0.Y = _p1.Y;
                        _p2.X = _p1.X;
                        break;
                    case 2: // Bottom-Right
                        _p2 = new Point(Math.Max(_p3.X + 50, Math.Min(canvasWidth, _p2.X + dx)), Math.Max(_p1.Y + 30, Math.Min(canvasHeight, _p2.Y + dy)));
                        _p3.Y = _p2.Y;
                        _p1.X = _p2.X;
                        break;
                    case 3: // Bottom-Left
                        _p3 = new Point(Math.Max(0, Math.Min(_p2.X - 50, _p3.X + dx)), Math.Max(_p0.Y + 30, Math.Min(canvasHeight, _p3.Y + dy)));
                        _p2.Y = _p3.Y;
                        _p0.X = _p3.X;
                        break;
                }
            }

            UpdatePolygon();
        }

        private void Thumb_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Thumb thumb)
            {
                _selectedThumbIndex = int.Parse(thumb.Tag.ToString() ?? "0");
                thumb.BorderBrush = System.Windows.Media.Brushes.Red;
                thumb.BorderThickness = new Thickness(2);
                thumb.Background = System.Windows.Media.Brushes.Yellow;
            }
        }

        private void Thumb_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Thumb thumb)
            {
                thumb.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4F46E5"));
                thumb.BorderThickness = new Thickness(1.5);
                thumb.Background = System.Windows.Media.Brushes.White;
            }
        }

        private void RadioMode_Checked(object sender, RoutedEventArgs e)
        {
            if (Radio2D == null || Radio3D == null) return;

            bool nextIs3D = Radio3D.IsChecked == true;
            if (nextIs3D == _is3D) return;

            _is3D = nextIs3D;

            if (!_is3D)
            {
                double minX = Math.Min(Math.Min(_p0.X, _p1.X), Math.Min(_p2.X, _p3.X));
                double maxX = Math.Max(Math.Max(_p0.X, _p1.X), Math.Max(_p2.X, _p3.X));
                double minY = Math.Min(Math.Min(_p0.Y, _p1.Y), Math.Min(_p2.Y, _p3.Y));
                double maxY = Math.Max(Math.Max(_p0.Y, _p1.Y), Math.Max(_p2.Y, _p3.Y));

                _p0 = new Point(minX, minY);
                _p1 = new Point(maxX, minY);
                _p2 = new Point(maxX, maxY);
                _p3 = new Point(minX, maxY);
            }

            UpdatePolygon();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            double step = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) ? 10 : 1;
            double dx = 0, dy = 0;

            switch (e.Key)
            {
                case Key.Left: dx = -step; break;
                case Key.Right: dx = step; break;
                case Key.Up: dy = -step; break;
                case Key.Down: dy = step; break;
                case Key.Enter: SaveAndClose(); return;
                case Key.Escape: DialogResult = false; Close(); return;
            }

            if (dx != 0 || dy != 0)
            {
                double canvasWidth = SystemParameters.PrimaryScreenWidth;
                double canvasHeight = SystemParameters.PrimaryScreenHeight;

                if (_selectedThumbIndex >= 0 && _selectedThumbIndex <= 3)
                {
                    if (_is3D)
                    {
                        switch (_selectedThumbIndex)
                        {
                            case 0: _p0 = new Point(Math.Max(0, Math.Min(canvasWidth, _p0.X + dx)), Math.Max(0, Math.Min(canvasHeight, _p0.Y + dy))); break;
                            case 1: _p1 = new Point(Math.Max(0, Math.Min(canvasWidth, _p1.X + dx)), Math.Max(0, Math.Min(canvasHeight, _p1.Y + dy))); break;
                            case 2: _p2 = new Point(Math.Max(0, Math.Min(canvasWidth, _p2.X + dx)), Math.Max(0, Math.Min(canvasHeight, _p2.Y + dy))); break;
                            case 3: _p3 = new Point(Math.Max(0, Math.Min(canvasWidth, _p3.X + dx)), Math.Max(0, Math.Min(canvasHeight, _p3.Y + dy))); break;
                        }
                    }
                    else
                    {
                        switch (_selectedThumbIndex)
                        {
                            case 0:
                                _p0 = new Point(Math.Max(0, Math.Min(_p1.X - 50, _p0.X + dx)), Math.Max(0, Math.Min(_p3.Y - 30, _p0.Y + dy)));
                                _p1.Y = _p0.Y;
                                _p3.X = _p0.X;
                                break;
                            case 1:
                                _p1 = new Point(Math.Max(_p0.X + 50, Math.Min(canvasWidth, _p1.X + dx)), Math.Max(0, Math.Min(_p2.Y - 30, _p1.Y + dy)));
                                _p0.Y = _p1.Y;
                                _p2.X = _p1.X;
                                break;
                            case 2:
                                _p2 = new Point(Math.Max(_p3.X + 50, Math.Min(canvasWidth, _p2.X + dx)), Math.Max(_p1.Y + 30, Math.Min(canvasHeight, _p2.Y + dy)));
                                _p3.Y = _p2.Y;
                                _p1.X = _p2.X;
                                break;
                            case 3:
                                _p3 = new Point(Math.Max(0, Math.Min(_p2.X - 50, _p3.X + dx)), Math.Max(_p0.Y + 30, Math.Min(canvasHeight, _p3.Y + dy)));
                                _p2.Y = _p3.Y;
                                _p0.X = _p3.X;
                                break;
                        }
                    }
                }
                else
                {
                    _p0 = new Point(_p0.X + dx, _p0.Y + dy);
                    _p1 = new Point(_p1.X + dx, _p1.Y + dy);
                    _p2 = new Point(_p2.X + dx, _p2.Y + dy);
                    _p3 = new Point(_p3.X + dx, _p3.Y + dy);
                }

                UpdatePolygon();
                e.Handled = true;
            }
        }

        private void SaveAndClose()
        {
            double canvasWidth = SystemParameters.PrimaryScreenWidth;
            double canvasHeight = SystemParameters.PrimaryScreenHeight;

            double minX = Math.Min(Math.Min(_p0.X, _p1.X), Math.Min(_p2.X, _p3.X));
            double maxX = Math.Max(Math.Max(_p0.X, _p1.X), Math.Max(_p2.X, _p3.X));
            double minY = Math.Min(Math.Min(_p0.Y, _p1.Y), Math.Min(_p2.Y, _p3.Y));
            double maxY = Math.Max(Math.Max(_p0.Y, _p1.Y), Math.Max(_p2.Y, _p3.Y));

            ResultX = minX / canvasWidth;
            ResultY = minY / canvasHeight;
            ResultWidth = (maxX - minX) / canvasWidth;
            ResultHeight = (maxY - minY) / canvasHeight;

            ResultX0 = _p0.X / canvasWidth;
            ResultY0 = _p0.Y / canvasHeight;
            ResultX1 = _p1.X / canvasWidth;
            ResultY1 = _p1.Y / canvasHeight;
            ResultX2 = _p2.X / canvasWidth;
            ResultY2 = _p2.Y / canvasHeight;
            ResultX3 = _p3.X / canvasWidth;
            ResultY3 = _p3.Y / canvasHeight;
            ResultIs3D = _is3D;

            DialogResult = true;
            Close();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveAndClose();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class PositionChangedEventArgs : EventArgs
    {
        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }

        public double X0 { get; }
        public double Y0 { get; }
        public double X1 { get; }
        public double Y1 { get; }
        public double X2 { get; }
        public double Y2 { get; }
        public double X3 { get; }
        public double Y3 { get; }
        public bool Is3D { get; }

        public PositionChangedEventArgs(double x, double y, double width, double height,
                                         double x0, double y0, double x1, double y1,
                                         double x2, double y2, double x3, double y3, bool is3D)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
            X3 = x3;
            Y3 = y3;
            Is3D = is3D;
        }
    }
}
