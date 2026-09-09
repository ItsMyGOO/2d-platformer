using System;
using Godot;
using GodotGameTemplate.Gameplay.Characters;
using GodotGameTemplate.Persistence;

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
    private SettingsMenu _settingsMenu;
    private Node _levelRoot;
    private bool _settingsFromPause; // 设置页来源：true=暂停态，false=主菜单态

    public override void _Ready()
    {
        _mainMenu = GetNode<MainMenu>("UiLayer/MainMenu");
        _pauseMenu = GetNode<PauseMenu>("UiLayer/PauseMenu");
        _settingsMenu = GetNode<SettingsMenu>("UiLayer/SettingsMenu");
        _levelRoot = GetNode("LevelRoot");
        _mainMenu.StartRequested += StartGame;
        _mainMenu.ContinueRequested += ContinueGame;
        _mainMenu.SettingsRequested += () => OpenSettings(fromPause: false);
        _mainMenu.QuitRequested += () => GetTree().Quit();
        _pauseMenu.ResumeRequested += ResumeGame;
        _pauseMenu.SettingsRequested += () => OpenSettings(fromPause: true);
        _pauseMenu.MainMenuRequested += BackToMainMenu;
        _settingsMenu.ScreenshakeToggled += ApplyScreenshakeSetting;

        // 启动即应用持久化设置（显示项 + 存量开关）
        SettingsService.Load();
        SettingsService.ApplyDisplay();
    }

    /// <summary>打开设置页：记住来源（主菜单/暂停），返回时回来源界面。</summary>
    private void OpenSettings(bool fromPause)
    {
        _settingsFromPause = fromPause;
        if (fromPause)
        {
            _pauseMenu.Visible = false;
        }
        else
        {
            _mainMenu.Visible = false;
        }
        _settingsMenu.Opened(CloseSettings);
    }

    private void CloseSettings()
    {
        _settingsMenu.Visible = false;
        if (_settingsFromPause)
        {
            _pauseMenu.Open();
        }
        else
        {
            _mainMenu.Visible = true;
        }
    }

    /// <summary>游玩中切换震屏开关立即对当前玩家生效（暂停/设置树内也可执行）。</summary>
    private void ApplyScreenshakeSetting(bool enabled)
    {
        if (_level != null && FindDescendant<ScreenShake>(_level) is { } shake)
        {
            shake.Enabled = enabled;
        }
    }

    private static T FindDescendant<T>(Node root)
        where T : Node
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is T hit)
            {
                return hit;
            }
            var deeper = FindDescendant<T>(child);
            if (deeper != null)
            {
                return deeper;
            }
        }
        return null;
    }

    /// <summary>Esc：游玩中暂停，暂停中恢复；主菜单界面无操作。</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("pause") || _settingsMenu.Visible)
        {
            return; // 设置页打开时 Esc 交给设置页（返回），不做暂停切换
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
        // 最近重生点自动存档：跨房/检查点更新重生点即写盘（M4 语义，Phase5 Task 1）
        player.SpawnPointChanged += pos =>
            SaveStore.Save(new SaveData { SpawnX = pos.X, SpawnY = pos.Y });
        _hud = GD.Load<PackedScene>("res://Game/UI/Hud.tscn").Instantiate<Hud>();
        GetNode("UiLayer").AddChild(_hud);
        _hud.Bind(player);
        if (FindDescendant<ScreenShake>(_level) is { } shake)
        {
            shake.Enabled = SettingsService.ScreenshakeEnabled;
        }
        _state = AppState.Playing;
    }

    /// <summary>继续游戏：先正常开局，再把玩家落到存档的重生点（满血由 Respawn 语义保证）。</summary>
    private void ContinueGame()
    {
        StartGame();
        if (SaveStore.TryLoad(out var save))
        {
            _levelRoot
                .GetNode<Character>("Level/Player")
                .PlaceAt(new Vector2(save.SpawnX, save.SpawnY));
        }
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
