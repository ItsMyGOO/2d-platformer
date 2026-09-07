using Godot;
using GodotGameTemplate.Gameplay.Characters;

namespace GodotGameTemplate.Gameplay.World;

/// <summary>
/// 检查点：玩家首次触碰即把重生点更新到此处并回满血，视觉切换为点亮。
/// 一次性激活（缓存房间里保持已激活状态）。mask 由场景配置（玩家实体层）。
/// </summary>
public partial class Checkpoint : Area2D
{
    private bool _activated;

    /// <summary>未激活时立柱颜色。</summary>
    [Export]
    public Color InactiveColor { get; set; } = new(0.45f, 0.45f, 0.5f, 1f);

    /// <summary>激活后立柱颜色。</summary>
    [Export]
    public Color ActiveColor { get; set; } = new(0.4f, 0.85f, 0.45f, 1f);

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        GetNode<ColorRect>("Visual").Color = InactiveColor;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_activated || body is not Character character)
        {
            return;
        }
        _activated = true;
        character.SetSpawnPoint(GlobalPosition);
        character.Health.RestoreFull();
        GetNode<ColorRect>("Visual").Color = ActiveColor;
    }
}
