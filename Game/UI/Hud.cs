using Godot;
using GodotGameTemplate.Gameplay.Characters;
using GodotGameTemplate.Gameplay.Characters.Combat;

namespace GodotGameTemplate.UI;

/// <summary>
/// 游玩 HUD：按 Health 的生命变化渲染心形血量；玩家死亡时显示黑场+提示，
/// 重生时淡出。由 Main 在开局后 Bind。
/// </summary>
public partial class Hud : CanvasLayer
{
    private Character _player;
    private Health _health;
    private TextureRect[] _hearts;
    private Control _deathOverlay;
    private ColorRect _dim;

    public override void _Ready()
    {
        _hearts =
        [
            GetNode<TextureRect>("Box/Heart1"),
            GetNode<TextureRect>("Box/Heart2"),
            GetNode<TextureRect>("Box/Heart3"),
            GetNode<TextureRect>("Box/Heart4"),
            GetNode<TextureRect>("Box/Heart5"),
        ];
        _deathOverlay = GetNode<Control>("DeathOverlay");
        GetNode<Label>("DeathOverlay/Center/Label").Text = Tr("DEATH_TEXT");
        _dim = GetNode<ColorRect>("DeathOverlay/Dim");
    }

    /// <summary>绑定玩家并立即刷新（受伤/死亡/重生回满均经事件再刷新）。</summary>
    public void Bind(Character player)
    {
        Unbind();
        _player = player;
        _health = player.Health;
        _health.HealthChanged += OnHealthChanged;
        _health.Died += OnHealthDied;
        _player.Died += OnPlayerDied;
        _player.Respawned += OnPlayerRespawned;
        Refresh(_health.CurrentHP);
    }

    public override void _ExitTree() => Unbind();

    private void OnHealthChanged(int currentHP) => Refresh(currentHP);

    private void OnHealthDied() => Refresh(0);

    private void OnPlayerDied()
    {
        _deathOverlay.Visible = true;
        var tween = CreateTween();
        tween.TweenProperty(_dim, "color:a", 0.55f, 0.15f);
    }

    private void OnPlayerRespawned()
    {
        var tween = CreateTween();
        tween.TweenProperty(_dim, "color:a", 0f, 0.25f);
        tween.Finished += () => _deathOverlay.Visible = false;
    }

    private void Refresh(int currentHP)
    {
        if (_hearts == null)
        {
            return;
        }
        for (int i = 0; i < _hearts.Length; i++)
        {
            _hearts[i].Visible = i < currentHP;
        }
    }

    private void Unbind()
    {
        if (_health == null)
        {
            return;
        }
        _health.HealthChanged -= OnHealthChanged;
        _health.Died -= OnHealthDied;
        _player.Died -= OnPlayerDied;
        _player.Respawned -= OnPlayerRespawned;
        _player = null;
        _health = null;
    }
}
