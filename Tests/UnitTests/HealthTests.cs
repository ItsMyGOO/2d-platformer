using GodotGameTemplate.Gameplay.Characters.Combat;
using Xunit;

namespace GodotGameTemplate.UnitTests;

public class HealthTests
{
    private static DamageInfo Hit(int damage = 1) =>
        DamageInfo.Create(
            damage,
            knockbackHorizontal: 10f,
            knockbackVertical: 5f,
            sourceDirection: 1f
        );

    [Fact]
    public void TryApplyDamage_ReducesCurrentHp()
    {
        var health = new Health(maxHP: 5, invincibilityTime: 0.8f);

        bool applied = health.TryApplyDamage(Hit(damage: 2));

        Assert.True(applied);
        Assert.Equal(3, health.CurrentHP);
    }

    [Fact]
    public void TryApplyDamage_DuringInvincibility_IsRejected()
    {
        var health = new Health(maxHP: 5, invincibilityTime: 0.8f);
        health.TryApplyDamage(Hit());

        bool secondHit = health.TryApplyDamage(Hit());

        Assert.False(secondHit);
        Assert.Equal(4, health.CurrentHP);
    }

    [Fact]
    public void UpdateTimers_AfterInvincibilityExpires_AllowsDamageAgain()
    {
        var health = new Health(maxHP: 5, invincibilityTime: 0.8f);
        health.TryApplyDamage(Hit());

        health.UpdateTimers(0.9f); // 单次推进 0.9s > 0.8s 无敌帧（避免浮点逐帧累加误差）

        bool secondHit = health.TryApplyDamage(Hit());

        Assert.True(secondHit);
        Assert.Equal(3, health.CurrentHP);
    }

    [Fact]
    public void KillingBlow_FiresDiedOnce_AndSkipsDamaged()
    {
        var health = new Health(maxHP: 2, invincibilityTime: 0f); // 0 无敌帧便于连击
        int damagedCount = 0;
        int diedCount = 0;
        health.Damaged += _ => damagedCount++;
        health.Died += () => diedCount++;

        health.TryApplyDamage(Hit(damage: 1));
        bool killingBlow = health.TryApplyDamage(Hit(damage: 1));

        Assert.True(killingBlow);
        Assert.Equal(0, health.CurrentHP);
        Assert.True(health.IsDead);
        Assert.Equal(1, damagedCount); // 第一击触发 Damaged；致命一击只触发 Died
        Assert.Equal(1, diedCount);
    }

    [Fact]
    public void TryApplyDamage_AfterDeath_IsRejected()
    {
        var health = new Health(maxHP: 1, invincibilityTime: 0.8f);
        health.TryApplyDamage(Hit(damage: 1));

        bool after = health.TryApplyDamage(Hit());

        Assert.False(after);
    }

    [Fact]
    public void RestoreFull_RestoresHp()
    {
        var health = new Health(maxHP: 5, invincibilityTime: 0.8f);
        health.TryApplyDamage(Hit(damage: 3));

        health.RestoreFull();

        Assert.Equal(5, health.CurrentHP);
        Assert.False(health.IsDead);
    }

    [Fact]
    public void TryApplyDamage_RaisesHealthChanged_WithNewHp()
    {
        var health = new Health(maxHP: 5, invincibilityTime: 0.8f);
        int changedCount = 0;
        int lastHp = 0;
        health.HealthChanged += hp =>
        {
            changedCount++;
            lastHp = hp;
        };

        health.TryApplyDamage(Hit(damage: 2));

        Assert.Equal(1, changedCount);
        Assert.Equal(3, lastHp);
    }

    [Fact]
    public void KillingBlow_DoesNotRaiseHealthChanged()
    {
        var health = new Health(maxHP: 2, invincibilityTime: 0f);
        int changedCount = 0;
        int diedCount = 0;
        health.HealthChanged += _ => changedCount++;
        health.Died += () => diedCount++;

        health.TryApplyDamage(Hit(damage: 2));

        Assert.Equal(1, diedCount);
        Assert.Equal(0, changedCount); // 血量归零由 Died 表达，不重复通知
    }

    [Fact]
    public void RestoreFull_RaisesHealthChanged_WithMaxHp()
    {
        var health = new Health(maxHP: 5, invincibilityTime: 0.8f);
        health.TryApplyDamage(Hit(damage: 3));
        int lastHp = 0;
        health.HealthChanged += hp => lastHp = hp;

        health.RestoreFull();

        Assert.Equal(5, lastHp);
    }

    [Fact]
    public void Heal_RestoresUpToAmount_AndFiresHealthChanged()
    {
        var health = new Health(maxHP: 5, invincibilityTime: 0.8f);
        health.TryApplyDamage(Hit(damage: 3));
        int lastHp = 0;
        health.HealthChanged += hp => lastHp = hp;

        bool healed = health.Heal(2);

        Assert.True(healed);
        Assert.Equal(4, health.CurrentHP);
        Assert.Equal(4, lastHp);
    }

    [Fact]
    public void Heal_AtFullHp_IsRejected()
    {
        var health = new Health(maxHP: 5, invincibilityTime: 0.8f);

        bool healed = health.Heal(1);

        Assert.False(healed);
        Assert.Equal(5, health.CurrentHP);
    }

    [Fact]
    public void Heal_CapsAtMaxHp()
    {
        var health = new Health(maxHP: 5, invincibilityTime: 0.8f);
        health.TryApplyDamage(Hit(damage: 1));

        health.Heal(99);

        Assert.Equal(5, health.CurrentHP);
    }

    [Fact]
    public void Heal_WhenDead_IsRejected()
    {
        var health = new Health(maxHP: 1, invincibilityTime: 0.8f);
        health.TryApplyDamage(Hit(damage: 1));

        bool healed = health.Heal(1);

        Assert.False(healed);
        Assert.True(health.IsDead);
    }
}
