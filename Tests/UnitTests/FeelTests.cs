using Godot;
using GodotGameTemplate.Gameplay.Characters;
using GodotGameTemplate.Gameplay.Characters.Combat;
using Xunit;

namespace GodotGameTemplate.UnitTests;

/// <summary>HK 手感件：下劈朝向标记 / 反冲冲量 / Pogo 弹跳（逻辑层）。</summary>
public class FeelTests
{
    private const float Dt = 1f / 60f;
    private const float Gravity = 980f;

    private static CharacterConfigData TestConfig() =>
        new()
        {
            MaxSpeed = 130f,
            Acceleration = 1000f,
            Friction = 1400f,
            JumpVelocity = -330f,
            MaxHP = 5,
            Attack = new CharacterConfigData.AttackData
            {
                WindupTime = 0.12f,
                ActiveTime = 0.1f,
                RecoveryTime = 0.18f,
                Damage = 1,
                KnockbackHorizontal = 180f,
                KnockbackVertical = 120f,
                Cooldown = 0.5f,
                RecoilVelocity = 120f,
            },
        };

    private static InputIntent Intent(
        float moveAxis = 0f,
        bool jumpPressed = false,
        bool attackPressed = false,
        bool downHeld = false
    ) =>
        InputIntent.Create(
            moveAxis,
            jumpPressed,
            jumpHeld: jumpPressed,
            attackPressed: attackPressed,
            downHeld: downHeld
        );

    private static void Run(CharacterMotor motor, in InputIntent intent, int frames, bool onFloor)
    {
        for (int i = 0; i < frames; i++)
        {
            motor.Process(intent, Dt, Gravity);
            motor.PostPhysics(onFloor, motor.Velocity);
        }
    }

    [Fact]
    public void DownHeld_FlowsIntoIntent()
    {
        var motor = new CharacterMotor(TestConfig());
        var intent = Intent(downHeld: true);

        motor.Process(intent, Dt, Gravity);

        Assert.True(motor.LastIntent.DownHeld);
    }

    [Fact]
    public void AirborneAttack_WithDownHeld_IsDownOriented()
    {
        var motor = new CharacterMotor(TestConfig());
        Run(motor, Intent(), 5, onFloor: true);
        motor.Process(Intent(jumpPressed: true), Dt, Gravity); // 起跳入空中
        motor.PostPhysics(false, motor.Velocity);
        Run(motor, Intent(), 3, onFloor: false);

        Run(motor, Intent(attackPressed: true, downHeld: true), 1, onFloor: false);

        Assert.True(motor.AttackDownOriented);
    }

    [Fact]
    public void GroundedAttack_IsNotDownOriented()
    {
        var motor = new CharacterMotor(TestConfig());
        Run(motor, Intent(), 5, onFloor: true);

        Run(motor, Intent(attackPressed: true, downHeld: true), 1, onFloor: true);

        Assert.False(motor.AttackDownOriented); // 地面攻击即便按住下也不是下劈
    }

    [Fact]
    public void Bounce_SetsUpwardVelocity()
    {
        var motor = new CharacterMotor(TestConfig());
        // 模拟下坠中（Velocity 由 PostPhysics 回喂）
        motor.Process(Intent(), Dt, Gravity);
        motor.PostPhysics(onFloor: false, new Vector2(60f, 300f));

        motor.Bounce();

        Assert.Equal(60f, motor.Velocity.X);
        Assert.True(motor.Velocity.Y < 0f); // 上升
        Assert.Equal(-330f * 0.75f, motor.Velocity.Y, 3); // JumpVelocity × 0.75
    }

    [Fact]
    public void ApplyImpulse_AddsVelocity()
    {
        var motor = new CharacterMotor(TestConfig());
        motor.Process(Intent(), Dt, Gravity);
        motor.PostPhysics(onFloor: true, new Vector2(80f, 0f));

        motor.ApplyImpulse(new Vector2(-120f, 0f)); // 反冲朝面朝反方向

        Assert.Equal(new Vector2(-40f, 0f), motor.Velocity);
    }
}
