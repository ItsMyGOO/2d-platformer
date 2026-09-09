using System;
using Godot;

namespace GodotGameTemplate.UI;

/// <summary>暂停菜单：继续/回主菜单。由 Main 在进入暂停时调 Open 并交给焦点。</summary>
public partial class PauseMenu : Control
{
    public event Action ResumeRequested;

    public event Action SettingsRequested;

    public event Action MainMenuRequested;

    public override void _Ready()
    {
        GetNode<Label>("Center/VBox/Title").Text = Tr("PAUSE_TITLE");
        GetNode<Button>("Center/VBox/ResumeButton").Text = Tr("PAUSE_RESUME");
        GetNode<Button>("Center/VBox/SettingsButton").Text = Tr("PAUSE_SETTINGS");
        GetNode<Button>("Center/VBox/MainMenuButton").Text = Tr("PAUSE_MAINMENU");
        GetNode<Button>("Center/VBox/ResumeButton").Pressed += () => ResumeRequested?.Invoke();
        GetNode<Button>("Center/VBox/SettingsButton").Pressed += () => SettingsRequested?.Invoke();
        GetNode<Button>("Center/VBox/MainMenuButton").Pressed += () => MainMenuRequested?.Invoke();
        Visible = false;
    }

    /// <summary>进入暂停态：显示并聚焦首个按钮（键盘可直接确认）。</summary>
    public void Open()
    {
        Visible = true;
        GetNode<Button>("Center/VBox/ResumeButton").GrabFocus();
    }
}
