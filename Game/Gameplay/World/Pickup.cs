using Godot;
using GodotGameTemplate.Gameplay.Characters;

namespace GodotGameTemplate.Gameplay.World;

/// <summary>
/// 拾取物：玩家触碰生效。治疗类拾取在满血时不消耗（留在原地）。
/// mask 由场景配置（玩家实体层）。
/// </summary>
public partial class Pickup : Area2D
{
    /// <summary>治疗效果（回血量）；满血时拾取不消耗。</summary>
    [Export]
    public int HealAmount { get; set; } = 1;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Character character && character.Health.Heal(HealAmount))
        {
            QueueFree();
        }
    }
}
