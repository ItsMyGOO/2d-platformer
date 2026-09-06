using Godot;
using GodotGameTemplate.Gameplay.Characters.Combat;

namespace GodotGameTemplate.UI;

/// <summary>游玩 HUD：按 Health 的生命变化渲染心形血量。由 Main 在开局后 Bind。</summary>
public partial class Hud : CanvasLayer
{
    private Health _health;
    private TextureRect[] _hearts;

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
    }

    /// <summary>绑定玩家生命组件并立即刷新（受伤/死亡/重生回满均经事件再刷新）。</summary>
    public void Bind(Health health)
    {
        Unbind();
        _health = health;
        _health.HealthChanged += OnHealthChanged;
        _health.Died += OnDied;
        Refresh(_health.CurrentHP);
    }

    public override void _ExitTree() => Unbind();

    private void OnHealthChanged(int currentHP) => Refresh(currentHP);

    private void OnDied() => Refresh(0);

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
        _health.Died -= OnDied;
        _health = null;
    }
}
