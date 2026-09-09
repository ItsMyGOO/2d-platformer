using System;
using System.Collections.Generic;
using Godot;
using GodotGameTemplate.Persistence;

namespace GodotGameTemplate.UI;

/// <summary>
/// 设置菜单：显示/玩法开关即时保存并应用；「操作」节为键位重映射（点按钮进入捕获态，
/// 按任意键改绑、Esc 取消）；「返回」或 Esc 经 onClosed 回调交还 Main。
/// process_mode=Always，暂停树中可用。
/// </summary>
public partial class SettingsMenu : Control
{
    private Action _onClosed;
    private string _capturingAction;
    private readonly Dictionary<string, Button> _bindButtons = new();

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

        BuildActionRows();
        GetNode<Button>("Center/VBox/ResetDefaultsButton").Pressed += () =>
        {
            InputRemapStore.Reset();
            RefreshBindButtons();
        };
    }

    /// <summary>打开设置页；onClosed 在返回/Esc 时回调（Main 据此回到来源界面）。</summary>
    public void Opened(Action onClosed)
    {
        _onClosed = onClosed;
        Visible = true;
        RefreshBindButtons(); // Main._Ready 的 Apply 可能晚于本节点 _Ready，打开时刷新一次
        GetNode<CheckButton>("Center/VBox/FullscreenCheck").GrabFocus();
    }

    private void Close() => _onClosed?.Invoke();

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible)
        {
            return;
        }

        // 捕获态：任意非 echo 按键改绑（Esc 取消），并吞掉该输入
        if (_capturingAction != null)
        {
            if (@event is InputEventKey key && !key.Echo)
            {
                GetViewport().SetInputAsHandled();
                var action = _capturingAction;
                _capturingAction = null;
                if (key.PhysicalKeycode != Key.Escape)
                {
                    InputRemapStore.Set(action, key.PhysicalKeycode);
                }
                RefreshBindButtons();
            }
            return;
        }

        if (@event.IsActionPressed("pause"))
        {
            GetViewport().SetInputAsHandled(); // Esc 在设置页 = 返回，不让 Main 再当暂停处理
            Close();
        }
    }

    private void BuildActionRows()
    {
        var rows = GetNode<VBoxContainer>("Center/VBox/ActionRows");
        foreach (var action in InputRemapStore.RemappableActions)
        {
            var row = new HBoxContainer();
            var label = new Label
            {
                Text = ActionDisplayName(action),
                CustomMinimumSize = new Vector2(140, 0),
            };
            var button = new Button { CustomMinimumSize = new Vector2(120, 32) };
            button.Pressed += () => BeginCapture(action);
            row.AddChild(label);
            row.AddChild(button);
            rows.AddChild(row);
            _bindButtons[action] = button;
        }
    }

    private void BeginCapture(string action)
    {
        _capturingAction = action;
        _bindButtons[action].Text = "按下新按键…";
    }

    private void RefreshBindButtons()
    {
        foreach (var (action, button) in _bindButtons)
        {
            button.Text = OS.GetKeycodeString(InputRemapStore.CurrentKey(action));
        }
    }

    private static string ActionDisplayName(string action) =>
        action switch
        {
            "move_left" => "向左",
            "move_right" => "向右",
            "jump" => "跳跃",
            "attack" => "攻击",
            "dash" => "冲刺",
            "pause" => "暂停",
            _ => action,
        };
}
