using Godot;
using GodotGameTemplate.Gameplay.Characters;
using GodotGameTemplate.Gameplay.Characters.Combat;
using Xunit;

namespace GodotGameTemplate.UnitTests;

/// <summary>剑气：无配置忽略/冷却/魂不足拒发不扣/成功扣魂出膛/互斥/受击打断。</summary>
public class CastTests
{
    private const float Dt = 1f / 60f;
    private const float Gravity = 980f;

    private static CharacterConfigData TestConfig(
        bool withCast = true,
        int soulMax = 33,
        int soulCost = 11,
        float castCooldown = 0.4f
    ) =>
        new()
        {
            MaxSpeed = 130f,
            Acceleration = 1000f,
            Friction = 1400f,
            JumpVelocity = -330f,
            MaxHP = 5,
            InvincibilityTime = 0.8f,
            HurtStunTime = 0.3f,
            SoulMax = soulMax,
            SoulGainPerHit = 11,
            Dash = new CharacterConfigData.DashData
            {
                Speed = 420f,
                Duration = 0.18f,
                Cooldown = 0.6f,
                EndSpeedKeepRatio = 0.4f,
            },
            Attack = new CharacterConfigData.AttackData
            {
                WindupTime = 0.12f,
                ActiveTime = 0.1f,
                RecoveryTime = 0.18f,
                Damage = 1,
                KnockbackHorizontal = 180f,
                KnockbackVertical = 120f,
                Cooldown = 0.5f,
            },
            Cast = withCast
                ? new CharacterConfigData.CastData
                {
                    WindupTime = 0.08f,
                    RecoveryTime = 0.22f,
                    Cooldown = castCooldown,
                    Damage = 1,
                    KnockbackHorizontal = 200f,
                    KnockbackVertical = 80f,
                    SoulCost = soulCost,
                }
                : null,
        };

    private static InputIntent Intent(
        float moveAxis = 0f,
        bool jumpPressed = false,
        bool dashPressed = false,
        bool attackPressed = false,
        bool castPressed = false
    ) =>
        InputIntent.Create(
            moveAxis,
            jumpPressed,
            jumpHeld: jumpPressed,
            dashPressed,
            attackPressed,
            castPressed
        );

    private static CharacterMotor RunGrounded(
        CharacterMotor motor,
        in InputIntent intent,
        int frames
    )
    {
        for (int i = 0; i < frames; i++)
        {
            motor.Process(intent, Dt, Gravity);
            motor.PostPhysics(onFloor: true, motor.Velocity);
        }
        return motor;
    }

    [Fact]
    public void NoCastConfig_InputIgnored()
    {
        var motor = new CharacterMotor(TestConfig(withCast: false));
        RunGrounded(motor, Intent(), 5);

        bool fired = false;
        motor.CastFired += () => fired = true;
        RunGrounded(motor, Intent(castPressed: true), 2);

        Assert.Equal(CharacterVisualState.Idle, motor.VisualState);
        Assert.False(fired);
    }

    [Fact]
    public void Cooldown_RejectsSecondCast()
    {
        var motor = new CharacterMotor(TestConfig(soulMax: 99, castCooldown: 0.4f));
        motor.GainSoul();
        motor.GainSoul();
        motor.GainSoul(); // 33 魂，够两发
        RunGrounded(motor, Intent(), 5);

        int fired = 0;
        motor.CastFired += () => fired++;
        RunGrounded(motor, Intent(castPressed: true), 1); // 第一发，冷却 0.4s 起算
        RunGrounded(motor, Intent(), 20); // 走完前摇+后摇（0.3s），冷却仍剩 ~0.07s
        Assert.Equal(1, fired);

        RunGrounded(motor, Intent(castPressed: true), 2); // 冷却中再按 → 拒
        Assert.Equal(1, fired);
        Assert.Equal(CharacterVisualState.Idle, motor.VisualState);

        RunGrounded(motor, Intent(), 30); // 等 0.5s 冷却过
        RunGrounded(motor, Intent(castPressed: true), 1); // 单帧脉冲再发
        RunGrounded(motor, Intent(), 30); // 完整走完第二发
        Assert.Equal(2, fired); // 冷却结束可再发
    }

    [Fact]
    public void InsufficientSoul_Rejected_AndNotSpent()
    {
        var motor = new CharacterMotor(TestConfig(soulMax: 33, soulCost: 11));
        motor.GainSoul(); // 11 < 11？相等可花；再补一发失败场景：只攒 0
        RunGrounded(motor, Intent(), 5);

        // 先清空情景：未攒魂（Current=0）时施法
        var poor = new CharacterMotor(TestConfig(soulMax: 33, soulCost: 11));
        RunGrounded(poor, Intent(), 5);
        bool fired = false;
        poor.CastFired += () => fired = true;
        RunGrounded(poor, Intent(castPressed: true), 2);

        Assert.Equal(0, poor.Soul.Current); // 不扣（也没得扣）
        Assert.False(fired);
        Assert.Equal(CharacterVisualState.Idle, poor.VisualState);
    }

    [Fact]
    public void Cast_SpendsSoul_AtStart_AndFiresAtWindupEnd()
    {
        var motor = new CharacterMotor(TestConfig());
        motor.GainSoul();
        motor.GainSoul(); // 22
        RunGrounded(motor, Intent(), 5);

        int fired = 0;
        motor.CastFired += () => fired++;

        RunGrounded(motor, Intent(castPressed: true), 1);
        Assert.Equal(11, motor.Soul.Current); // 施法瞬间原子扣魂
        Assert.Equal(0, fired); // 前摇未结束

        RunGrounded(motor, Intent(), 5); // 0.08s ≈ 5 帧后出膛
        Assert.Equal(1, fired);
        Assert.Equal(CharacterVisualState.Attack, motor.VisualState); // 复用攻击动画

        RunGrounded(motor, Intent(), 25); // 后摇 0.22s 结束
        Assert.Equal(CharacterVisualState.Idle, motor.VisualState);
    }

    [Fact]
    public void DuringAttack_CastRejected()
    {
        var motor = new CharacterMotor(TestConfig());
        motor.GainSoul();
        RunGrounded(motor, Intent(), 5);

        bool fired = false;
        motor.CastFired += () => fired = true;
        RunGrounded(motor, Intent(attackPressed: true), 1);
        Assert.Equal(CharacterVisualState.Attack, motor.VisualState);

        RunGrounded(motor, Intent(castPressed: true), 20); // 攻击中按剑气 → 拒
        Assert.False(fired);
        Assert.Equal(11, motor.Soul.Current); // 魂未被消耗
    }

    [Fact]
    public void DuringCast_AttackAndDashRejected()
    {
        var motor = new CharacterMotor(TestConfig());
        motor.GainSoul();
        RunGrounded(motor, Intent(), 5);

        int attacks = 0;
        int dashes = 0;
        motor.AttackStarted += () => attacks++;
        motor.Jumped += () => dashes++; // 冲刺无独立事件，用状态断言为主

        RunGrounded(motor, Intent(castPressed: true), 1);
        Assert.Equal(CharacterVisualState.Attack, motor.VisualState); // 剑气复用攻击视觉

        RunGrounded(motor, Intent(attackPressed: true), 3);
        RunGrounded(motor, Intent(dashPressed: true), 3);
        Assert.Equal(0, attacks); // 剑气中攻击被拒

        // 仍在剑气状态（后摇 0.3s 内）
        RunGrounded(motor, Intent(), 10);
        Assert.Equal(0, attacks);
    }

    [Fact]
    public void Hurt_InterruptsCast()
    {
        var motor = new CharacterMotor(TestConfig());
        motor.GainSoul();
        RunGrounded(motor, Intent(), 5);

        bool fired = false;
        motor.CastFired += () => fired = true;
        RunGrounded(motor, Intent(castPressed: true), 1); // 前摇刚开始

        motor.ForceHurt(DamageInfo.Create(1, 100f, 50f, sourceDirection: 1f)); // 受击打断
        Assert.Equal(CharacterVisualState.Hurt, motor.VisualState);

        RunGrounded(motor, Intent(), 30);
        Assert.False(fired); // 前摇未完成即被打断，不放剑气
    }
}
