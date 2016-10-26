using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Subspace
{
    public enum DropState
    {
        Valid,
        Invalid,
        None
    }

    /// <summary>
    /// Interaction logic for DropArea.xaml
    /// </summary>
    public partial class DropArea : UserControl
    {
        #region Properties and their event handlers
        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Message.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(string), typeof(DropArea), new PropertyMetadata(""));
        
        public bool Loading
        {
            get { return (bool)GetValue(LoadingProperty); }
            set { SetValue(LoadingProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Loading.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LoadingProperty =
            DependencyProperty.Register("Loading", typeof(bool), typeof(DropArea), new PropertyMetadata(false, LoadingChanged));

        public DropState State
        {
            get { return (DropState)GetValue(StateProperty); }
            set { SetValue(StateProperty, value); }
        }

        // Using a DependencyProperty as the backing store for State.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register("State", typeof(DropState), typeof(DropArea), new PropertyMetadata(DropState.None, StateChanged));
        
        /// <summary>
        /// Static function called when the state changes.
        /// </summary>
        private static void StateChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue != e.OldValue)
                ((DropArea)obj).OnStateChanged((DropState)e.NewValue);
        }

        /// <summary>
        /// Static function called when <see cref="Loading"/> changes.
        /// </summary>
        private static void LoadingChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue != e.OldValue)
                ((DropArea)obj).OnLoadingChanged((bool)e.NewValue);
        }
        #endregion

        /// <summary>
        /// All paths used for state animation
        /// </summary>
        public IEnumerable<Path> Paths => PathCanvas.Children.OfType<Path>();

        /// <summary>
        /// Computed length of all paths.
        /// </summary>
        public double[] PathLengths { get; private set; }

        /// <summary>
        /// <see cref="SolidColorBrush"/> corresponding to a valid drag & drop action.
        /// </summary>
        public static SolidColorBrush ValidBrush
        {
            get { return new SolidColorBrush(Colors.Cyan); }
        }

        /// <summary>
        /// <see cref="SolidColorBrush"/> corresponding to an invalid drag & drop action.
        /// </summary>
        public static SolidColorBrush InvalidBrush
        {
            get { return new SolidColorBrush(Colors.Crimson); }
        }

        /// <summary>
        /// <see cref="ScaleTransform"/> property of the main button
        /// (which is a <see cref="Border"/>).
        /// </summary>
        private ScaleTransform ButtonScaleTransform;

        public DropArea()
        {
            InitializeComponent();
            ComputePathLengths();

            ButtonScaleTransform = new ScaleTransform(1.0, 1.0);
            MainBtn.RenderTransform = ButtonScaleTransform;

            InitializeLoader();
        }

        /// <summary>
        /// Function called when the state changes.
        /// </summary>
        private void OnStateChanged(DropState state)
        {
            if (state == DropState.None)
            {
                ChangeScale(1.0);

                int i = 0;
                foreach (Path path in Paths)
                {
                    ChangePathDashOffset(path, -PathLengths[i++]);
                }
            }
            else
            {
                if (state == DropState.Valid)
                    ChangeScale(1.2);

                foreach (Path path in Paths)
                {
                    path.Stroke = state == DropState.Valid ? ValidBrush : InvalidBrush;
                    ChangePathDashOffset(path, 0);
                }
            }
        }

        /// <summary>
        /// Function called when <see cref="Loading"/> changes.
        /// </summary>
        private void OnLoadingChanged(bool loading)
        {
            LoaderCanvas.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        }

        #region Utils

        /// <summary>
        /// Compute the length of all paths, and set <see cref="PathLengths"/>.
        /// </summary>
        private void ComputePathLengths()
        {
            PathLengths = new double[PathCanvas.Children.Count];

            int i = 0;
            foreach (Path path in Paths)
            {
                PathLengths[i] = GetLength(path.Data) * 2;
                path.StrokeDashOffset = -PathLengths[i++];
            }
        }

        /// <summary>
        /// Compute the length of a geometry with many segments.
        /// </summary>
        /// <remarks>
        /// Shameless copy-paste from http://stackoverflow.com/questions/21728753
        /// </remarks>
        private static double GetLength(Geometry geo)
        {
            PathGeometry path = geo.GetFlattenedPathGeometry();
            double length = 0.0;

            foreach (PathFigure pf in path.Figures)
            {
                Point start = pf.StartPoint;

                foreach (PolyLineSegment seg in pf.Segments)
                {
                    foreach (Point point in seg.Points)
                    {
                        length += Math.Sqrt(Math.Pow(start.X - point.X, 2) + Math.Pow(start.Y - point.Y, 2));
                        start = point;
                    }
                }
            }

            return length;
        }

        /// <summary>
        /// Animate a path's <see cref="Shape.StrokeDashOffset"/> property to a given value.
        /// </summary>
        private void ChangePathDashOffset(Path path, double to)
        {
            DoubleAnimation anim = new DoubleAnimation(path.StrokeDashOffset, to, new Duration(TimeSpan.FromMilliseconds(500)))
            {
                EasingFunction = new CubicEase()
            };

            path.ApplyAnimationClock(Shape.StrokeDashOffsetProperty, anim.CreateClock());
        }

        /// <summary>
        /// Animate the <see cref="ScaleTransform.ScaleX"/> and <see cref="ScaleTransform.ScaleY"/>
        /// properties of the main button to a given value.
        /// </summary>
        private void ChangeScale(double to)
        {
            DoubleAnimation anim = new DoubleAnimation(ButtonScaleTransform.ScaleX, to, new Duration(TimeSpan.FromMilliseconds(500 * to)))
            {
                EasingFunction = new ExponentialEase()
            };

            AnimationClock clock = anim.CreateClock();
            ButtonScaleTransform.ApplyAnimationClock(ScaleTransform.ScaleXProperty, clock);
            ButtonScaleTransform.ApplyAnimationClock(ScaleTransform.ScaleYProperty, clock);
        }

        /// <summary>
        /// Animate the loader canvas from left to right indefinitely,
        /// and create its children.
        /// </summary>
        private void InitializeLoader()
        {
            for (int i = 0; i < 30; i++)
            {
                LoaderCanvas.Children.Add(new Line
                {
                    X1 = -(Width + 120) + (35.0 * i),
                    X2 = -Width + (35.0 * i)
                });
            }

            ThicknessAnimation anim = new ThicknessAnimation(
                new Thickness(0, 0, 0, 0),
                new Thickness(Width + 20.0, 0, 0, 0),
                new Duration(TimeSpan.FromMilliseconds(4000)))
            {
                AutoReverse = false,
                RepeatBehavior = RepeatBehavior.Forever
            };

            LoaderCanvas.ApplyAnimationClock(MarginProperty, anim.CreateClock());
        }
        #endregion
    }
}
