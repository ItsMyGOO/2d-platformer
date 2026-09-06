using System;
using Godot;

namespace GodotGameTemplate.UI;

/// <summary>主菜单：开始/退出。只上报意图，切场景由 Main 编排。</summary>
public partial class MainMenu : Control
{
    public event Action StartRequested;

    public event Action QuitRequested;

    public override void _Ready()
    {
        GetNode<Button>("Center/VBox/StartButton").Pressed += () => StartRequested?.Invoke();
        GetNode<Button>("Center/VBox/QuitButton").Pressed += () => QuitRequested?.Invoke();
        GetNode<Button>("Center/VBox/StartButton").GrabFocus();
    }
}
