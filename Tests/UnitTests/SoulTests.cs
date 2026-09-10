using System;
using Godot;
using GodotGameTemplate.Gameplay.Characters;
using GodotGameTemplate.Gameplay.Characters.Combat;
using Xunit;

namespace GodotGameTemplate.UnitTests;

/// <summary>HK 式魂量：封顶/拒绝消费/清零事件，以及 Motor 集成（命中积攒、重生清零）。</summary>
public class SoulTests
{
    [Fact]
    public void Gain_ClampsAtMax_AndFiresSoulChanged()
    {
        var soul = new Soul(33);
        int? lastCurrent = null;
        soul.SoulChanged += (current, max) =>
        {
            Assert.Equal(33, max);
            lastCurrent = current;
        };

        soul.Gain(11);
        Assert.Equal(11, soul.Current);
        Assert.Equal(11, lastCurrent);

        soul.Gain(100); // 超上限封顶
        Assert.Equal(33, soul.Current);
        Assert.Equal(33, lastCurrent);
    }

    [Fact]
    public void Gain_NonPositive_IsIgnored()
    {
        var soul = new Soul(33);
        bool changed = false;
        soul.SoulChanged += (_, _) => changed = true;

        soul.Gain(0);
        soul.Gain(-5);
        Assert.Equal(0, soul.Current);
        Assert.False(changed);
    }

    [Fact]
    public void TrySpend_SucceedsWhenEnough_AndRejectsWhenNot()
    {
        var soul = new Soul(33);
        soul.Gain(22);

        Assert.True(soul.TrySpend(11));
        Assert.Equal(11, soul.Current);

        bool changed = false;
        soul.SoulChanged += (_, _) => changed = true;
        Assert.False(soul.TrySpend(22)); // 不足拒绝且不动值不发事件
        Assert.Equal(11, soul.Current);
        Assert.False(changed);
    }

    [Fact]
    public void Clear_ZeroesAndFires()
    {
        var soul = new Soul(33);
        soul.Gain(33);
        int? last = null;
        soul.SoulChanged += (current, _) => last = current;

        soul.Clear();
        Assert.Equal(0, soul.Current);
        Assert.Equal(0, last);
    }

    private static CharacterConfigData MotorConfig(int soulMax = 33, int soulGain = 11) =>
        new()
        {
            MaxSpeed = 130f,
            Acceleration = 1000f,
            Friction = 1400f,
            JumpVelocity = -330f,
            MaxHP = 5,
            SoulMax = soulMax,
            SoulGainPerHit = soulGain,
        };

    [Fact]
    public void Motor_GainSoul_AccumulatesFromConfig()
    {
        var motor = new CharacterMotor(MotorConfig());
        Assert.NotNull(motor.Soul);

        motor.GainSoul();
        motor.GainSoul();
        Assert.Equal(22, motor.Soul.Current);
    }

    [Fact]
    public void Motor_Reset_ClearsSoul()
    {
        var motor = new CharacterMotor(MotorConfig());
        motor.GainSoul();
        motor.GainSoul();

        motor.Reset(); // 重生清零
        Assert.Equal(0, motor.Soul.Current);
    }

    [Fact]
    public void Motor_ZeroSoulMax_HasNullSoul()
    {
        var motor = new CharacterMotor(MotorConfig(soulMax: 0));
        Assert.Null(motor.Soul);
        motor.GainSoul(); // 无魂系统时忽略
    }
}
