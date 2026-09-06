using Godot;
using GodotGameTemplate.Gameplay.Characters;

namespace GodotGameTemplate.Gameplay.World;

/// <summary>
/// 即死区：进入的任何角色立即死亡（不经过血量——坠落死与战斗死分列）。
/// 敌人走既有尸体流程后消失，玩家回出生点重生。关卡自持，通常置于关卡底部之外。
/// </summary>
public partial class KillZone : Area2D
{
    public override void _Ready()
    {
        // collision_mask 由场景配置：实体层 1|2
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Character character)
        {
            character.KillInstantly();
        }
    }
}
