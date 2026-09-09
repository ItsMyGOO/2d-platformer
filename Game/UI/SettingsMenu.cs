using System;
using Godot;
using GodotGameTemplate.Persistence;

namespace GodotGameTemplate.UI;

/// <summary>
/// 设置菜单：显示/玩法开关即时保存并应用；「返回」或 Esc 经 onClosed 回调交还 Main
/// 回到来源界面。process_mode=Always，暂停树中可用。
/// </summary>
public partial class SettingsMenu : Control
{
    private Action _onClosed;

    /// <summary>震屏开关变化（游玩中的玩家立即生效，由 Main 订阅）。</summary>
    public event Action<bool> ScreenshakeToggled;

    public override void _Ready()
    {
        var fullscreen = GetNode<CheckButton>("Center/VBox/FullscreenCheck");
        var vsync = GetNode<CheckButton>("Center/VBox/VsyncCheck");
        var shake = GetNode<CheckButton>("Center/VBox/ScreenshakeCheck");
        fullscreen.ButtonPressed = SettingsService.Fullscreen;
        vsync.ButtonPressed = SettingsService.Vsync;
        shake.ButtonPressed = SettingsService.ScreenshakeEnabled;
        fullscreen.Toggled += on => SettingsService.SetFullscreen(on);
        vsync.Toggled += on => SettingsService.SetVsync(on);
        shake.Toggled += on =>
        {
            SettingsService.SetScreenshake(on);
            ScreenshakeToggled?.Invoke(on);
        };
        GetNode<Button>("Center/VBox/BackButton").Pressed += Close;
    }

    /// <summary>打开设置页；onClosed 在返回/Esc 时回调（Main 据此回到来源界面）。</summary>
    public void Opened(Action onClosed)
    {
        _onClosed = onClosed;
        Visible = true;
        GetNode<CheckButton>("Center/VBox/FullscreenCheck").GrabFocus();
    }

    private void Close() => _onClosed?.Invoke();

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("pause"))
        {
            GetViewport().SetInputAsHandled(); // Esc 在设置页 = 返回，不让 Main 再当暂停处理
            Close();
        }
    }
}
