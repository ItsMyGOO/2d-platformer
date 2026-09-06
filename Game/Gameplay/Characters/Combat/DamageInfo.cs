namespace GodotGameTemplate.Gameplay.Characters.Combat;

/// <summary>一次伤害结算的全部数据。SourceDirection 为攻击者→受击者的水平方向：+1 受击者向右飞。</summary>
public readonly struct DamageInfo
{
    public int Damage { get; init; }

    public float KnockbackHorizontal { get; init; }

    /// <summary>击退垂直速度（正值向上弹起）。</summary>
    public float KnockbackVertical { get; init; }

    public float SourceDirection { get; init; }

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
