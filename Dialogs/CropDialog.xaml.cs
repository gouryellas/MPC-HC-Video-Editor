using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Draws the part of the frame a cut keeps, by dragging a box on a still from
/// the cut itself.
/// </summary>
/// <remarks>
/// Dragged rather than typed. A crop is a judgement about a picture — where the
/// black bars stop, how much headroom to leave — and four numbers are a poor
/// way to make it, because nothing on screen answers them until the file has
/// been written.
///
/// The still arrives already flipped and turned, so the box is drawn on the
/// picture the cut will produce. The rectangle is kept as fractions of that
/// picture, which is what the <c>crop</c> filter is given.
/// </remarks>
public partial class CropDialog : Window
{
    /// <summary>The frame's own size in pixels, for the readout.</summary>
    private readonly int _sourceWidth;
    private readonly int _sourceHeight;

    /// <summary>How big the still is drawn. The box lives in these units.</summary>
    private readonly double _stageWidth;
    private readonly double _stageHeight;

    /// <summary>The box, in stage pixels.</summary>
    private Rect _box;

    /// <summary>What the pointer is doing, and from where.</summary>
    private enum Grab { None, Draw, Move, TopLeft, TopRight, BottomLeft, BottomRight }

    private Grab _grab = Grab.None;
    private Point _grabbedAt;
    private Rect _boxAtGrab;

    /// <summary>Width ÷ height the box is held to, or zero for free.</summary>
    private double _ratio;

    /// <summary>The rectangle chosen, as fractions of the frame.</summary>
    public double CropX { get; private set; }
    public double CropY { get; private set; }
    public double CropWidth { get; private set; } = 1;
    public double CropHeight { get; private set; } = 1;

    /// <param name="frame">A still from the cut, already flipped and turned.</param>
    /// <param name="sourceWidth">The frame's real width, for the readout.</param>
    /// <param name="start">The rectangle to open with, as fractions.</param>
    public CropDialog(ImageSource frame, int sourceWidth, int sourceHeight,
                      double x, double y, double width, double height)
    {
        InitializeComponent();

        _sourceWidth = sourceWidth > 0 ? sourceWidth : (int)frame.Width;
        _sourceHeight = sourceHeight > 0 ? sourceHeight : (int)frame.Height;

        // Big enough to aim at, small enough to leave room for the controls.
        // The stage takes the frame's own shape so a square drawn on screen is
        // a square in the output.
        const double maxWidth = 720;
        const double maxHeight = 460;

        var scale = Math.Min(maxWidth / _sourceWidth, maxHeight / _sourceHeight);
        _stageWidth = Math.Round(_sourceWidth * scale);
        _stageHeight = Math.Round(_sourceHeight * scale);

        Stage.Width = _stageWidth;
        Stage.Height = _stageHeight;
        Frame.Source = frame;

        _box = new Rect(x * _stageWidth, y * _stageHeight,
                        width * _stageWidth, height * _stageHeight);

        Redraw();
    }

    // ------------------------------------------------------------------
    // Pointer
    // ------------------------------------------------------------------

    private void Stage_MouseDown(object sender, MouseButtonEventArgs e)
    {
        PressAt(e.GetPosition(Stage));
        Stage.CaptureMouse();
    }

    private void Stage_MouseMove(object sender, MouseEventArgs e)
    {
        var at = e.GetPosition(Stage);

        if (_grab == Grab.None)
        {
            Stage.Cursor = WhatIsUnder(at) switch
            {
                Grab.TopLeft or Grab.BottomRight => Cursors.SizeNWSE,
                Grab.TopRight or Grab.BottomLeft => Cursors.SizeNESW,
                Grab.Move => Cursors.SizeAll,
                _ => Cursors.Cross
            };
            return;
        }

        DragTo(at);
    }

    private void Stage_MouseUp(object sender, MouseEventArgs e)
    {
        if (_grab == Grab.None) return;

        Stage.ReleaseMouseCapture();
        Release();
    }

    // ------------------------------------------------------------------
    // The gesture itself
    //
    // Separated from the three handlers above so it can be driven without a
    // mouse. A synthetic MouseEventArgs cannot lie about where the pointer is
    // — GetPosition reads the device, not the event — so a test that went
    // through the handlers would be testing the real cursor's position, which
    // is no test at all.
    // ------------------------------------------------------------------

    /// <summary>The box as it stands, in stage pixels.</summary>
    public Rect CropBox => _box;

    /// <summary>How big the still is drawn, for anything aiming at it.</summary>
    public Size StageSize => new(_stageWidth, _stageHeight);

    /// <summary>Whether the box is the whole picture, within a pixel.</summary>
    private bool IsWholeFrame =>
        _box.Width >= _stageWidth - 1 && _box.Height >= _stageHeight - 1;

    /// <summary>Starts a gesture at a point: drawing, moving or resizing.</summary>
    public void PressAt(Point at)
    {
        _grabbedAt = Clamp(at);
        _boxAtGrab = _box;
        _grab = WhatIsUnder(_grabbedAt);

        // Starting a fresh box: it begins empty at the press and grows with the
        // pointer, so a click on the picture outside the box replaces it rather
        // than nudging the old one.
        if (_grab == Grab.Draw)
            _box = new Rect(_grabbedAt, new Size(0, 0));

        Redraw();
    }

    /// <summary>Carries the gesture to a point.</summary>
    public void DragTo(Point to)
    {
        if (_grab == Grab.None) return;

        var at = Clamp(to);

        _box = _grab switch
        {
            Grab.Draw => FromCorners(_grabbedAt, at),
            Grab.Move => Moved(at),
            Grab.TopLeft => FromCorners(new Point(_boxAtGrab.Right, _boxAtGrab.Bottom), at),
            Grab.TopRight => FromCorners(new Point(_boxAtGrab.Left, _boxAtGrab.Bottom), at),
            Grab.BottomLeft => FromCorners(new Point(_boxAtGrab.Right, _boxAtGrab.Top), at),
            Grab.BottomRight => FromCorners(new Point(_boxAtGrab.Left, _boxAtGrab.Top), at),
            _ => _box
        };

        Redraw();
    }

    /// <summary>Ends the gesture.</summary>
    public void Release()
    {
        if (_grab == Grab.None) return;
        _grab = Grab.None;

        // A press that drew nothing — a stray click, or a miss — would leave a
        // box too small to see and nothing to grab it by, so the whole frame
        // comes back instead.
        if (_box.Width < 8 || _box.Height < 8)
            _box = new Rect(0, 0, _stageWidth, _stageHeight);

        Redraw();
    }

    /// <summary>Which part of the box, if any, a point is on.</summary>
    /// <remarks>
    /// Measured against the box rather than hit-tested against the handles: the
    /// handles are inside a Canvas that does not answer the hit test, which is
    /// what keeps the Grid underneath receiving the drag in the first place.
    /// </remarks>
    private Grab WhatIsUnder(Point at)
    {
        const double reach = 12;

        // A box that covers everything is the same thing as no box at all, so
        // a press inside it starts a new one. Without this the dialog opened
        // on the whole frame and every press landed inside the box — which is
        // to say the first drag moved a full-frame box nowhere, and there was
        // no way to draw the first rectangle at all.
        if (IsWholeFrame) return Grab.Draw;

        bool near(double a, double b) => Math.Abs(a - b) <= reach;

        var onLeft = near(at.X, _box.Left);
        var onRight = near(at.X, _box.Right);
        var onTop = near(at.Y, _box.Top);
        var onBottom = near(at.Y, _box.Bottom);

        if (onLeft && onTop) return Grab.TopLeft;
        if (onRight && onTop) return Grab.TopRight;
        if (onLeft && onBottom) return Grab.BottomLeft;
        if (onRight && onBottom) return Grab.BottomRight;

        return _box.Contains(at) ? Grab.Move : Grab.Draw;
    }

    /// <summary>The box dragged whole, kept inside the picture.</summary>
    private Rect Moved(Point at)
    {
        var x = _boxAtGrab.X + (at.X - _grabbedAt.X);
        var y = _boxAtGrab.Y + (at.Y - _grabbedAt.Y);

        x = Math.Clamp(x, 0, _stageWidth - _boxAtGrab.Width);
        y = Math.Clamp(y, 0, _stageHeight - _boxAtGrab.Height);

        return new Rect(x, y, _boxAtGrab.Width, _boxAtGrab.Height);
    }

    /// <summary>
    /// A rectangle from an anchor corner to the pointer, honouring the chosen
    /// shape and staying inside the picture.
    /// </summary>
    private Rect FromCorners(Point anchor, Point at)
    {
        var width = Math.Abs(at.X - anchor.X);
        var height = Math.Abs(at.Y - anchor.Y);

        if (_ratio > 0)
        {
            // The longer side wins, so the box follows whichever way the
            // pointer is really going rather than snapping back and forth.
            if (width / _ratio > height) height = width / _ratio;
            else width = height * _ratio;

            // Room left in the direction being dragged, so a constrained box
            // stops at the edge instead of sliding along it.
            var roomX = at.X >= anchor.X ? _stageWidth - anchor.X : anchor.X;
            var roomY = at.Y >= anchor.Y ? _stageHeight - anchor.Y : anchor.Y;

            var fit = Math.Min(roomX / (_ratio > 0 ? _ratio : 1), roomY);
            if (height > fit) { height = fit; width = height * _ratio; }
        }

        var left = at.X >= anchor.X ? anchor.X : anchor.X - width;
        var top = at.Y >= anchor.Y ? anchor.Y : anchor.Y - height;

        left = Math.Clamp(left, 0, Math.Max(0, _stageWidth - width));
        top = Math.Clamp(top, 0, Math.Max(0, _stageHeight - height));

        return new Rect(left, top, width, height);
    }

    private Point Clamp(Point at) =>
        new(Math.Clamp(at.X, 0, _stageWidth), Math.Clamp(at.Y, 0, _stageHeight));

    // ------------------------------------------------------------------
    // Drawing
    // ------------------------------------------------------------------

    private void Redraw()
    {
        Canvas.SetLeft(Box, _box.X);
        Canvas.SetTop(Box, _box.Y);
        Box.Width = _box.Width;
        Box.Height = _box.Height;

        Place(HandleTL, _box.Left, _box.Top);
        Place(HandleTR, _box.Right, _box.Top);
        Place(HandleBL, _box.Left, _box.Bottom);
        Place(HandleBR, _box.Right, _box.Bottom);

        // Two rectangles, even-odd: the whole stage with the box punched out of
        // it. Four borders round the box would be four things to keep in step.
        var outside = new RectangleGeometry(new Rect(0, 0, _stageWidth, _stageHeight));
        var inside = new RectangleGeometry(_box);
        Shade.Data = new GeometryGroup
        {
            FillRule = FillRule.EvenOdd,
            Children = { outside, inside }
        };

        var width = (int)Math.Round(_sourceWidth * (_box.Width / _stageWidth));
        var height = (int)Math.Round(_sourceHeight * (_box.Height / _stageHeight));

        // Even, as the encoder requires and as the filter will force anyway —
        // better to say the number that will come out than one two pixels off.
        width -= width % 2;
        height -= height % 2;

        SizeNote.Text = $"{width} × {height}  of  {_sourceWidth} × {_sourceHeight}";
    }

    private static void Place(UIElement handle, double x, double y)
    {
        Canvas.SetLeft(handle, x - 5);
        Canvas.SetTop(handle, y - 5);
    }

    // ------------------------------------------------------------------
    // Controls
    // ------------------------------------------------------------------

    private void Shape_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;

        _ratio = sender == Shape169 ? 16.0 / 9
               : sender == Shape916 ? 9.0 / 16
               : sender == Shape11 ? 1
               : sender == Shape43 ? 4.0 / 3
               : 0;

        // The box already on screen is brought to the new shape, rather than
        // waiting for the next drag: choosing 1:1 and seeing a wide box still
        // sitting there would read as the choice not having taken.
        if (_ratio > 0)
            _box = FromCorners(new Point(_box.Left, _box.Top),
                               new Point(_box.Right, _box.Bottom));

        Redraw();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        ShapeFree.IsChecked = true;
        _ratio = 0;
        _box = new Rect(0, 0, _stageWidth, _stageHeight);
        Redraw();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        CropX = _box.X / _stageWidth;
        CropY = _box.Y / _stageHeight;
        CropWidth = _box.Width / _stageWidth;
        CropHeight = _box.Height / _stageHeight;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
