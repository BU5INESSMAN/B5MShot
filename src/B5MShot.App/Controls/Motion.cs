using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;

namespace B5MShot.App.Controls;

/// <summary>Short, interruptible motion. Only chrome is animated, never the captured image.</summary>
public static class Motion
{
    public const int Enter = 460, Settle = 340, Exit = 190, Feedback = 110;
    public static bool Enabled => SystemParameters.ClientAreaAnimation &&
        !(AppContext.TryGetSwitch("B5MShot.DisableAnimations", out var disabled) && disabled);

    public static void To(DependencyObject target, DependencyProperty property, double to,
        int duration = Settle, bool spring = true, double? from = null, int delay = 0)
    {
        var start = from ?? (double)target.GetValue(property);
        // Store the final base value. Stop removes the clock's influence even on interruption.
        target.SetValue(property, to);
        DoubleAnimationUsingKeyFrames? animation = null;
        if (Enabled)
        {
            animation = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(start, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            if (delay > 0) animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(start, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay))));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay + duration)))
            { EasingFunction = spring ? new SoftSpring() : new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
        if (target is UIElement element) element.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
        else if (target is Animatable animatable) animatable.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }

    public static void Reveal(FrameworkElement element, int delay = 0, double distance = 7)
    {
        if (!element.IsLoaded) return;
        var translate = element.RenderTransform as TranslateTransform ?? new TranslateTransform();
        element.RenderTransform = translate;
        To(translate, TranslateTransform.YProperty, 0, Enter, from: -distance, delay: delay);
        To(element, UIElement.OpacityProperty, 1, 220, false, 0, delay);
    }

    public static readonly DependencyProperty InteractiveProperty = DependencyProperty.RegisterAttached(
        "Interactive", typeof(bool), typeof(Motion), new PropertyMetadata(false, InteractiveChanged));
    public static void SetInteractive(DependencyObject target, bool value) => target.SetValue(InteractiveProperty, value);
    public static bool GetInteractive(DependencyObject target) => (bool)target.GetValue(InteractiveProperty);
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached("State", typeof(ButtonMotion), typeof(Motion));
    private static void InteractiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ButtonBase button) return;
        if ((bool)e.NewValue) button.SetValue(StateProperty, new ButtonMotion(button));
        else if (button.GetValue(StateProperty) is ButtonMotion state) { state.Dispose(); button.ClearValue(StateProperty); }
    }

    // Opt-in: must not be applied globally to annotation TextBlocks (they are exported).
    public static readonly DependencyProperty TextRevealProperty = DependencyProperty.RegisterAttached(
        "TextReveal", typeof(bool), typeof(Motion), new PropertyMetadata(false, TextRevealChanged));
    public static void SetTextReveal(DependencyObject target, bool value) => target.SetValue(TextRevealProperty, value);
    public static bool GetTextReveal(DependencyObject target) => (bool)target.GetValue(TextRevealProperty);
    private static readonly DependencyProperty TextStateProperty = DependencyProperty.RegisterAttached("TextState", typeof(TextMotion), typeof(Motion));
    private static void TextRevealChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock text) return;
        if ((bool)e.NewValue) text.SetValue(TextStateProperty, new TextMotion(text));
        else if (text.GetValue(TextStateProperty) is TextMotion state) { state.Dispose(); text.ClearValue(TextStateProperty); }
    }

    private sealed class TextMotion : IDisposable
    {
        private readonly TextBlock _text;
        private readonly DependencyPropertyDescriptor _descriptor = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
        private bool _subscribed;
        public TextMotion(TextBlock text) { _text = text; text.Loaded += Loaded; text.Unloaded += Unloaded; if (text.IsLoaded) Loaded(text, new RoutedEventArgs()); }
        private void Loaded(object sender, RoutedEventArgs e) { if (_subscribed) return; _subscribed = true; _descriptor.AddValueChanged(_text, Changed); Reveal(_text); }
        private void Unloaded(object sender, RoutedEventArgs e) { if (!_subscribed) return; _descriptor.RemoveValueChanged(_text, Changed); _subscribed = false; }
        private void Changed(object? sender, EventArgs e)
        {
            var transform = _text.RenderTransform as TranslateTransform ?? new TranslateTransform();
            _text.RenderTransform = transform;
            To(transform, TranslateTransform.YProperty, 0, 260, from: 4);
            To(_text, UIElement.OpacityProperty, 1, 200, false, .35);
        }
        public void Dispose() { Unloaded(_text, new RoutedEventArgs()); _text.Loaded -= Loaded; _text.Unloaded -= Unloaded; }
    }

    private sealed class ButtonMotion : IDisposable
    {
        private readonly ButtonBase _button;
        private readonly ScaleTransform _scale = new();
        private readonly TranslateTransform _translate = new();
        private readonly RotateTransform _rotate = new();
        private readonly DependencyPropertyDescriptor _pressed = DependencyPropertyDescriptor.FromProperty(ButtonBase.IsPressedProperty, typeof(ButtonBase));
        private bool _subscribed;
        public ButtonMotion(ButtonBase button)
        {
            _button = button;
            button.Loaded += Loaded; button.Unloaded += Unloaded;
            button.MouseEnter += StateChanged; button.MouseLeave += StateChanged;
            button.GotKeyboardFocus += StateChanged; button.LostKeyboardFocus += StateChanged;
            button.Click += Click;
            if (button.IsLoaded) Loaded(button, new RoutedEventArgs());
        }
        private void Loaded(object sender, RoutedEventArgs e)
        {
            _button.ApplyTemplate();
            if (_button.Template?.FindName("MotionContent", _button) is FrameworkElement content)
            {
                var group = new TransformGroup(); group.Children.Add(_scale); group.Children.Add(_rotate); group.Children.Add(_translate);
                content.RenderTransformOrigin = new System.Windows.Point(.5, .5); content.RenderTransform = group;
            }
            if (!_subscribed) { _pressed.AddValueChanged(_button, PressedChanged); _subscribed = true; }
        }
        private void Unloaded(object sender, RoutedEventArgs e) { if (_subscribed) _pressed.RemoveValueChanged(_button, PressedChanged); _subscribed = false; }
        private void PressedChanged(object? sender, EventArgs e) => Update();
        private void StateChanged(object sender, RoutedEventArgs e) => Update();
        private void Update()
        {
            var engaged = _button.IsMouseOver || _button.IsKeyboardFocused;
            var compact = _button.ActualWidth <= 56;
            var size = _button.IsPressed ? (compact ? .84 : .97) : engaged ? (compact ? 1.12 : 1.025) : 1;
            To(_scale, ScaleTransform.ScaleXProperty, size, _button.IsPressed ? Feedback : Settle, !_button.IsPressed);
            To(_scale, ScaleTransform.ScaleYProperty, size, _button.IsPressed ? Feedback : Settle, !_button.IsPressed);
            To(_translate, TranslateTransform.YProperty, engaged && !_button.IsPressed ? -1.5 : 0, 260);
            if (_button.Template?.FindName("HoverLight", _button) is FrameworkElement light)
                To(light, UIElement.OpacityProperty, _button.IsPressed ? .2 : engaged ? .13 : 0, Feedback, false);
        }
        private void Click(object sender, RoutedEventArgs e)
        {
            Update();
            // A single recoil confirms mouse and keyboard activation, no perpetual movement.
            if (_button.ActualWidth <= 56) To(_rotate, RotateTransform.AngleProperty, 0, 360, from: -9);
        }
        public void Dispose()
        {
            Unloaded(_button, new RoutedEventArgs());
            _button.Loaded -= Loaded; _button.Unloaded -= Unloaded;
            _button.MouseEnter -= StateChanged; _button.MouseLeave -= StateChanged;
            _button.GotKeyboardFocus -= StateChanged; _button.LostKeyboardFocus -= StateChanged; _button.Click -= Click;
        }
    }
}

/// <summary>Damped spring with a small overshoot and an exact resting endpoint.</summary>
public sealed class SoftSpring : EasingFunctionBase
{
    public SoftSpring() => EasingMode = EasingMode.EaseIn;
    protected override double EaseInCore(double t)
    {
        static double Response(double x) => 1 - Math.Exp(-8 * x) * (Math.Cos(10 * x) + .8 * Math.Sin(10 * x));
        return Response(t) / Response(1);
    }
    protected override Freezable CreateInstanceCore() => new SoftSpring();
}
