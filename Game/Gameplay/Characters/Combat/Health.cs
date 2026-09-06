using System;

namespace GodotGameTemplate.Gameplay.Characters.Combat;

/// <summary>
/// 生命值：伤害结算、受击无敌帧与死亡事件。纯 C# 类，由编排者驱动计时。
/// </summary>
public class Health
{
    private readonly float _invincibilityTime;
    private float _invincibilityTimer;

    /// <summary>最大生命值（构造后不变）。</summary>
    public int MaxHP { get; }

    /// <summary>当前生命值，下限 0。</summary>
    public int CurrentHP { get; private set; }

    /// <summary>处于受击无敌帧期间（期间 TryApplyDamage 被拒）。</summary>
    public bool IsInvincible => _invincibilityTimer > 0f;

    /// <summary>是否已死亡（CurrentHP ≤ 0）；死亡为终态。</summary>
    public bool IsDead => CurrentHP <= 0;

    /// <summary>成功受伤时触发（死亡时只触发 Died）。</summary>
    public event Action<DamageInfo> Damaged;

    /// <summary>致命一击时触发一次（该次不触发 Damaged）。</summary>
    public event Action Died;

    /// <summary>生命值发生任何变化（受伤结算 / 回满）时触发，携带当前血量。</summary>
    public event Action<int> HealthChanged;

    public Health(int maxHP, float invincibilityTime)
    {
        if (maxHP <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHP));
        }
        MaxHP = maxHP;
        CurrentHP = maxHP;
        _invincibilityTime = invincibilityTime;
    }

    /// <summary>由编排者每物理帧调用，推进无敌帧计时。</summary>
    public void UpdateTimers(float delta)
    {
        if (_invincibilityTimer > 0f)
        {
            _invincibilityTimer -= delta;
        }
    }

    /// <summary>尝试结算伤害。无敌帧期间或已死亡时拒绝并返回 false。</summary>
    public bool TryApplyDamage(DamageInfo info)
    {
        if (IsDead || IsInvincible)
        {
            return false;
        }
        CurrentHP = Math.Max(0, CurrentHP - info.Damage);
        _invincibilityTimer = _invincibilityTime;
        if (CurrentHP <= 0)
        {
            Died?.Invoke(); // 血量归零由 Died 表达，不重复发 HealthChanged
        }
        else
        {
            HealthChanged?.Invoke(CurrentHP);
            Damaged?.Invoke(info);
        }
        return true;
    }

    /// <summary>回满生命（重生时同时附带无敌帧）。</summary>
    public void RestoreFull()
    {
        CurrentHP = MaxHP;
        _invincibilityTimer = _invincibilityTime;
        HealthChanged?.Invoke(CurrentHP);
    }

    /// <summary>治疗：已死亡、已满血或治疗量非正时拒绝并返回 false。治疗量超过上限时截断。</summary>
    public bool Heal(int amount)
    {
        if (IsDead || CurrentHP >= MaxHP || amount <= 0)
        {
            return false;
        }
        CurrentHP = Math.Min(MaxHP, CurrentHP + amount);
        HealthChanged?.Invoke(CurrentHP);
        return true;
    }
}
