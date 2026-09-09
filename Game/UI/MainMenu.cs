using System;
using Godot;
using GodotGameTemplate.Persistence;

namespace GodotGameTemplate.UI;

/// <summary>主菜单：开始/继续/退出。只上报意图，切场景由 Main 编排。</summary>
public partial class MainMenu : Control
{
    public event Action StartRequested;

    public event Action ContinueRequested;

    public event Action SettingsRequested;

    public event Action QuitRequested;

    public override void _Ready()
    {
        GetNode<Label>("Center/VBox/Title").Text = Tr("GAME_TITLE");
        GetNode<Button>("Center/VBox/StartButton").Text = Tr("MENU_START");
        GetNode<Button>("Center/VBox/ContinueButton").Text = Tr("MENU_CONTINUE");
        GetNode<Button>("Center/VBox/SettingsButton").Text = Tr("MENU_SETTINGS");
        GetNode<Button>("Center/VBox/QuitButton").Text = Tr("MENU_QUIT");
        GetNode<Button>("Center/VBox/StartButton").Pressed += () => StartRequested?.Invoke();
        var continueButton = GetNode<Button>("Center/VBox/ContinueButton");
        continueButton.Pressed += () => ContinueRequested?.Invoke();
        continueButton.Disabled = !SaveStore.Exists(); // 无存档时不可继续
        GetNode<Button>("Center/VBox/SettingsButton").Pressed += () => SettingsRequested?.Invoke();
        GetNode<Button>("Center/VBox/QuitButton").Pressed += () => QuitRequested?.Invoke();
        GetNode<Button>("Center/VBox/StartButton").GrabFocus();
    }
}
