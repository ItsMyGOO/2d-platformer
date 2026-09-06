using System;
using Godot;
using GodotGameTemplate.Gameplay.Characters;

namespace GodotGameTemplate.UI;

/// <summary>
/// 应用编排：主菜单 → 关卡 → 暂停 → 回主菜单 的生命周期切换。
/// 自身 process_mode=Always（暂停树中仍能响应 Esc）；关卡挂在 Pausable 的 LevelRoot 下，
/// 菜单挂在不暂停的 UiLayer 下。菜单节点只上报意图，切树由本类负责。
/// </summary>
public partial class Main : Node
{
    private enum AppState
    {
        InMenu,
        Playing,
        Paused,
    }

    private AppState _state = AppState.InMenu;
    private Node _level;
    private Hud _hud;

    private MainMenu _mainMenu;
    private PauseMenu _pauseMenu;
    private Node _levelRoot;

    public override void _Ready()
    {
        _mainMenu = GetNode<MainMenu>("UiLayer/MainMenu");
        _pauseMenu = GetNode<PauseMenu>("UiLayer/PauseMenu");
        _levelRoot = GetNode("LevelRoot");
        _mainMenu.StartRequested += StartGame;
        _mainMenu.QuitRequested += () => GetTree().Quit();
        _pauseMenu.ResumeRequested += ResumeGame;
        _pauseMenu.MainMenuRequested += BackToMainMenu;
    }

    /// <summary>Esc：游玩中暂停，暂停中恢复；主菜单界面无操作。</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("pause"))
        {
            return;
        }
        if (_state == AppState.Playing)
        {
            PauseGame();
        }
        else if (_state == AppState.Paused)
        {
            ResumeGame();
        }
    }

    private void StartGame()
    {
        _mainMenu.Visible = false;
        var worldScene = GD.Load<PackedScene>("res://Game/Scenes/World.tscn");
        _level = worldScene.Instantiate();
        _level.Name = "Level"; // 固定实例名，HUD 等按 LevelRoot/Level/Player 寻址
        _levelRoot.AddChild(_level);
        var player = _levelRoot.GetNode<Character>("Level/Player");
        _hud = GD.Load<PackedScene>("res://Game/UI/Hud.tscn").Instantiate<Hud>();
        GetNode("UiLayer").AddChild(_hud);
        _hud.Bind(player.Health);
        _state = AppState.Playing;
    }

    private void PauseGame()
    {
        _state = AppState.Paused;
        GetTree().Paused = true;
        _pauseMenu.Open();
    }

    private void ResumeGame()
    {
        _pauseMenu.Visible = false;
        GetTree().Paused = false;
        _state = AppState.Playing;
    }

    private void BackToMainMenu()
    {
        GetTree().Paused = false;
        _pauseMenu.Visible = false;
        _level?.QueueFree();
        _level = null;
        if (_hud != null)
        {
            _hud.QueueFree();
            _hud = null;
        }
        _mainMenu.Visible = true;
        _state = AppState.InMenu;
    }
}
