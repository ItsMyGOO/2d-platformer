namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>
/// 角色运动状态。推导式：由地面事实与速度实时计算得出，
/// 不维护独立状态机，因此不可能出现状态与运动不一致的情况。
/// </summary>
public enum CharacterState
{
    Idle,
    Run,
    Jump,
    Fall,
}
