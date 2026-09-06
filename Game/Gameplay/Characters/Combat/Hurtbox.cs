using Godot;

namespace GodotGameTemplate.Gameplay.Characters.Combat;

/// <summary>
/// 受击框：接收 Hitbox 的命中并转发给所属角色。
/// 所属角色在 _Ready 沿祖先链发现（编排者在自己的 _Ready 里调用 Bind）。
/// </summary>
public partial class Hurtbox : Area2D
{
    private Character _character;

    public override void _Ready()
    {
        Monitorable = true;
        Monitoring = false; // 受击框只被打，不打人
        _character = FindOwnerCharacter();
    }

    /// <summary>由编排者显式绑定所属角色（比祖先链查找更明确）。</summary>
    public void Bind(Character character) => _character = character;

    /// <summary>被 Hitbox 命中的入口：转发给所属角色走伤害结算。</summary>
    public void ReceiveHit(DamageInfo info) => _character?.OnHurt(info);

    private Character FindOwnerCharacter()
    {
        Node current = GetParent();
        while (current != null)
        {
            if (current is Character character)
            {
                return character;
            }
            current = current.GetParent();
        }
        return null;
    }
}
