namespace GodotGameTemplate.Gameplay.Characters.Combat;

/// <summary>一次伤害结算的全部数据。SourceDirection 为攻击者→受击者的水平方向：+1 受击者向右飞。</summary>
public readonly struct DamageInfo
{
    /// <summary>本次伤害数值。</summary>
    public int Damage { get; init; }

    /// <summary>击退水平速度（像素/秒），方向由 SourceDirection 决定。</summary>
    public float KnockbackHorizontal { get; init; }

    /// <summary>击退垂直速度（正值向上弹起）。</summary>
    public float KnockbackVertical { get; init; }

    /// <summary>攻击者→受击者的水平方向：+1 受击者向右飞，-1 向左。</summary>
    public float SourceDirection { get; init; }

    /// <summary>构造一次伤害结算数据（只读结构体的唯一入口）。</summary>
    public static DamageInfo Create(
        int damage,
        float knockbackHorizontal,
        float knockbackVertical,
        float sourceDirection
    ) =>
        new()
        {
            Damage = damage,
            KnockbackHorizontal = knockbackHorizontal,
            KnockbackVertical = knockbackVertical,
            SourceDirection = sourceDirection,
        };
}
