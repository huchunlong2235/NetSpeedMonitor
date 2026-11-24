using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace NetSpeedMonitor.Views;

/// <summary>
/// 右下角轻量提示窗口：不抢焦点、不入任务栏、4 秒后自动淡出。
/// 用于内存清理完成后的结果反馈，不依赖系统通知设置（系统气泡常被吞掉）。
/// </summary>
public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _closeTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    private bool _closing;

    public ToastWindow()
    {
        InitializeComponent();
        _closeTimer.Tick += (_, _) => BeginFadeOut();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 定位到工作区右下角（WorkArea 已排除任务栏），留出 12px 边距
        var work = SystemParameters.WorkArea;
        Left = work.Right - ActualWidth - 12;
        Top = work.Bottom - ActualHeight - 12;
    }

    /// <summary>显示提示；重复调用会重置文本与计时器，避免多个窗口堆叠</summary>
    public void ShowMessage(string message)
    {
        DetailText.Text = message;
        BeginAnimation(OpacityProperty, null); // 取消可能正在进行的淡出动画
        Opacity = 1;
        _closing = false;
        _closeTimer.Stop();

        if (!IsVisible) Show();
        _closeTimer.Start();
    }

    private void BeginFadeOut()
    {
        if (_closing) return;
        _closing = true;
        _closeTimer.Stop();

        var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(350));
        anim.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, anim);
    }

    private void Border_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BeginFadeOut();
    }
}
