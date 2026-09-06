using Godot;
using GodotGameTemplate.Gameplay.Characters;
using GodotGameTemplate.Gameplay.Characters.Combat;
using Xunit;

namespace GodotGameTemplate.UnitTests;

public class CharacterMotorTests
{
    private const float Dt = 1f / 60f;
    private const float Gravity = 980f;

    private static CharacterConfigData TestConfig(bool withDash = true, bool withAttack = true) =>
        new()
        {
            MaxSpeed = 130f,
            Acceleration = 1000f,
            Friction = 1400f,
            JumpVelocity = -330f,
            GravityScale = 1f,
            JumpCutMultiplier = 0.45f,
            CoyoteTime = 0.1f,
            JumpBufferTime = 0.12f,
            MaxFallSpeed = 520f,
            MaxHP = 5,
            InvincibilityTime = 0.8f,
            HurtStunTime = 0.3f,
            Dash = withDash
                ? new CharacterConfigData.DashData
                {
                    Speed = 420f,
                    Duration = 0.18f,
                    Cooldown = 0.6f,
                    EndSpeedKeepRatio = 0.4f,
                }
                : null,
            Attack = withAttack
                ? new CharacterConfigData.AttackData
                {
                    WindupTime = 0.12f,
                    ActiveTime = 0.1f,
                    RecoveryTime = 0.18f,
                    Damage = 1,
                    KnockbackHorizontal = 180f,
                    KnockbackVertical = 120f,
                    Cooldown = 0.5f,
                }
                : null,
        };

    private static InputIntent Intent(
        float moveAxis = 0f,
        bool jumpPressed = false,
        bool jumpHeld = false,
        bool dashPressed = false,
        bool attackPressed = false
    ) => InputIntent.Create(moveAxis, jumpPressed, jumpHeld, dashPressed, attackPressed);

    /// <summary>模拟「站在开阔地面」：每帧 Process + PostPhysics(onFloor: true)。</summary>
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

    /// <summary>模拟「空中」：重力由状态内部施加，PostPhysics(onFloor: false) 原样回喂。</summary>
    private static CharacterMotor RunAirborne(
        CharacterMotor motor,
        in InputIntent intent,
        int frames
    )
    {
        for (int i = 0; i < frames; i++)
        {
            motor.Process(intent, Dt, Gravity);
            motor.PostPhysics(onFloor: false, motor.Velocity);
        }
        return motor;
    }

    [Fact]
    public void Grounded_JumpInput_LiftsOffWithJumpVelocity()
    {
        var motor = new CharacterMotor(TestConfig());
        RunGrounded(motor, Intent(), 10);

        motor.Process(Intent(jumpPressed: true, jumpHeld: true), Dt, Gravity);

        Assert.Equal(-330f, motor.Velocity.Y);
        Assert.False(motor.IsOnFloor);
        Assert.Equal(CharacterVisualState.Jump, motor.VisualState);
    }

    [Fact]
    public void Jump_AirborneJumpInput_CannotRejump_CoyoteBurned()
    {
        var motor = new CharacterMotor(TestConfig());
        int jumps = 0;
        motor.Jumped += () => jumps++;
        RunGrounded(motor, Intent(), 5);
        motor.Process(Intent(jumpPressed: true, jumpHeld: true), Dt, Gravity);
        motor.PostPhysics(false, motor.Velocity);
        Assert.Equal(1, jumps);

        RunAirborne(motor, Intent(jumpPressed: true, jumpHeld: true), 30);

        Assert.Equal(1, jumps); // 起跳瞬间烧掉土狼时间，空中新脉冲无效
        Assert.True(motor.Velocity.Y > -330f); // 速度已被重力衰减而非重置为起跳速度
    }

    [Fact]
    public void CoyoteTime_JumpWithinWindow_Works()
    {
        var motor = new CharacterMotor(TestConfig());
        int jumps = 0;
        motor.Jumped += () => jumps++;
        RunGrounded(motor, Intent(), 5);

        // 走出平台边缘：地面事实消失
        motor.Process(Intent(), Dt, Gravity);
        motor.PostPhysics(false, motor.Velocity);

        RunAirborne(motor, Intent(), 3); // 0.05s ≤ 土狼 0.1s
        motor.Process(Intent(jumpPressed: true, jumpHeld: true), Dt, Gravity);

        Assert.Equal(1, jumps);
        Assert.Equal(CharacterVisualState.Jump, motor.VisualState);
    }

    [Fact]
    public void CoyoteTime_AfterWindow_JumpRejected()
    {
        var motor = new CharacterMotor(TestConfig());
        int jumps = 0;
        motor.Jumped += () => jumps++;
        RunGrounded(motor, Intent(), 5);
        motor.Process(Intent(), Dt, Gravity);
        motor.PostPhysics(false, motor.Velocity);

        RunAirborne(motor, Intent(), 12); // 0.2s > 土狼 0.1s
        motor.Process(Intent(jumpPressed: true, jumpHeld: true), Dt, Gravity);

        Assert.Equal(0, jumps);
        Assert.Equal(CharacterVisualState.Fall, motor.VisualState);
    }

    [Fact]
    public void JumpBuffer_PressedBeforeLanding_FiresOnLanding()
    {
        var motor = new CharacterMotor(TestConfig());
        int jumps = 0;
        motor.Jumped += () => jumps++;
        RunGrounded(motor, Intent(), 5);
        motor.Process(Intent(jumpPressed: true, jumpHeld: true), Dt, Gravity); // 起跳
        motor.PostPhysics(false, motor.Velocity);

        RunAirborne(motor, Intent(), 40); // 下落 ~0.67s，土狼已过期
        RunAirborne(motor, Intent(jumpPressed: true, jumpHeld: true), 3); // 落地前 0.05s 按跳
        motor.Process(Intent(), Dt, Gravity);
        motor.PostPhysics(true, motor.Velocity); // 落地帧 → 回地面状态

        motor.Process(Intent(), Dt, Gravity); // 落地后一帧：缓冲跳触发

        Assert.Equal(2, jumps);
        Assert.False(motor.IsOnFloor);
    }

    [Fact]
    public void VariableJumpHeight_ReleasingEarly_CutsRise()
    {
        var held = new CharacterMotor(TestConfig());
        RunGrounded(held, Intent(), 5);
        held.Process(Intent(jumpPressed: true, jumpHeld: true), Dt, Gravity);
        held.PostPhysics(false, held.Velocity);
        RunAirborne(held, Intent(jumpHeld: true), 2); // 持续按住

        var released = new CharacterMotor(TestConfig());
        RunGrounded(released, Intent(), 5);
        released.Process(Intent(jumpPressed: true, jumpHeld: true), Dt, Gravity);
        released.PostPhysics(false, released.Velocity);
        RunAirborne(released, Intent(jumpHeld: false), 2); // 立即松开

        Assert.True(held.Velocity.Y < -250f); // 未截断
        Assert.True(released.Velocity.Y > -200f); // 截断到 ~45%
    }

    [Fact]
    public void Dash_Grounded_EntersAndEndsWithKeepRatio()
    {
        var motor = new CharacterMotor(TestConfig());
        RunGrounded(motor, Intent(), 5);

        motor.Process(Intent(dashPressed: true), Dt, Gravity);
        Assert.Equal(CharacterVisualState.Dash, motor.VisualState);
        Assert.Equal(420f, motor.Velocity.X);
        motor.PostPhysics(true, motor.Velocity);

        for (int i = 0; i < 10; i++) // 冲刺持续 ~0.18s
        {
            motor.Process(Intent(), Dt, Gravity);
            Assert.Equal(CharacterVisualState.Dash, motor.VisualState);
            motor.PostPhysics(true, motor.Velocity);
        }

        motor.Process(Intent(), Dt, Gravity); // 耗尽帧
        Assert.NotEqual(CharacterVisualState.Dash, motor.VisualState);
        Assert.Equal(168f, motor.Velocity.X, precision: 1); // 420 × 0.4
    }

    [Fact]
    public void Dash_WhileActive_IgnoresJumpAndAttack()
    {
        var motor = new CharacterMotor(TestConfig());
        RunGrounded(motor, Intent(), 5);
        motor.Process(Intent(dashPressed: true), Dt, Gravity);
        motor.PostPhysics(true, motor.Velocity);

        motor.Process(
            Intent(jumpPressed: true, jumpHeld: true, attackPressed: true, dashPressed: true),
            Dt,
            Gravity
        );

        Assert.Equal(CharacterVisualState.Dash, motor.VisualState);
    }

    [Fact]
    public void Dash_Cooldown_PreventsImmediateRedash()
    {
        var motor = new CharacterMotor(TestConfig());
        RunGrounded(motor, Intent(), 5);
        RunGrounded(motor, Intent(dashPressed: true), 12); // 冲刺并耗尽

        RunGrounded(motor, Intent(dashPressed: true), 5); // 冷却中（0.6s）

        Assert.NotEqual(CharacterVisualState.Dash, motor.VisualState);
        RunGrounded(motor, Intent(dashPressed: true), 30); // 冷却结束帧自动再冲
        Assert.Equal(CharacterVisualState.Dash, motor.VisualState);
    }

    [Fact]
    public void Attack_Grounded_RunsWindows_ThenReturns()
    {
        var motor = new CharacterMotor(TestConfig());
        int started = 0;
        int activeOn = 0;
        int activeOff = 0;
        motor.AttackStarted += () => started++;
        motor.AttackActiveChanged += active =>
        {
            if (active)
            {
                activeOn++;
            }
            else
            {
                activeOff++;
            }
        };
        RunGrounded(motor, Intent(), 5);

        motor.Process(Intent(attackPressed: true), Dt, Gravity);
        Assert.Equal(CharacterVisualState.Attack, motor.VisualState);
        Assert.Equal(0f, motor.Velocity.X); // 地面攻击定身
        motor.PostPhysics(true, motor.Velocity);

        int attackFrames = 0;
        for (int i = 0; i < 30; i++)
        {
            motor.Process(Intent(), Dt, Gravity);
            motor.PostPhysics(true, motor.Velocity);
            if (motor.VisualState == CharacterVisualState.Attack)
            {
                attackFrames++;
            }
        }

        Assert.Equal(1, started);
        Assert.Equal(1, activeOn);
        Assert.Equal(1, activeOff);
        Assert.InRange(attackFrames, 20, 26); // 三段窗口总长 0.40s ≈ 24 帧
        Assert.NotEqual(CharacterVisualState.Attack, motor.VisualState);
    }

    [Fact]
    public void Attack_WhileActive_IgnoresDashAndJump()
    {
        var motor = new CharacterMotor(TestConfig());
        RunGrounded(motor, Intent(), 5);
        motor.Process(Intent(attackPressed: true), Dt, Gravity);
        motor.PostPhysics(true, motor.Velocity);

        motor.Process(Intent(dashPressed: true, jumpPressed: true, jumpHeld: true), Dt, Gravity);

        Assert.Equal(CharacterVisualState.Attack, motor.VisualState);
    }

    [Fact]
    public void ForceHurt_InterruptsDash_EntersHurt_ThenRecovers()
    {
        var motor = new CharacterMotor(TestConfig());
        RunGrounded(motor, Intent(), 5);
        motor.Process(Intent(dashPressed: true), Dt, Gravity);

        motor.ForceHurt(
            DamageInfo.Create(
                damage: 1,
                knockbackHorizontal: 180f,
                knockbackVertical: 120f,
                sourceDirection: -1f
            )
        );

        Assert.Equal(CharacterVisualState.Hurt, motor.VisualState);
        Assert.Equal(-180f, motor.Velocity.X);
        Assert.Equal(-120f, motor.Velocity.Y);

        RunGrounded(motor, Intent(), 20); // 0.33s > 硬直 0.3s

        Assert.NotEqual(CharacterVisualState.Hurt, motor.VisualState);
    }

    [Fact]
    public void ForceHurt_InterruptsAttack()
    {
        var motor = new CharacterMotor(TestConfig());
        RunGrounded(motor, Intent(), 5);
        motor.Process(Intent(attackPressed: true), Dt, Gravity);
        Assert.Equal(CharacterVisualState.Attack, motor.VisualState);

        motor.ForceHurt(
            DamageInfo.Create(
                damage: 1,
                knockbackHorizontal: 100f,
                knockbackVertical: 50f,
                sourceDirection: 1f
            )
        );

        Assert.Equal(CharacterVisualState.Hurt, motor.VisualState);
    }

    [Fact]
    public void Hurt_DuringStun_IgnoresAllInput()
    {
        var motor = new CharacterMotor(TestConfig());
        RunGrounded(motor, Intent(), 5);
        motor.ForceHurt(
            DamageInfo.Create(
                damage: 1,
                knockbackHorizontal: 180f,
                knockbackVertical: 120f,
                sourceDirection: 1f
            )
        );

        motor.Process(
            Intent(
                moveAxis: 1f,
                jumpPressed: true,
                jumpHeld: true,
                dashPressed: true,
                attackPressed: true
            ),
            Dt,
            Gravity
        );

        Assert.Equal(CharacterVisualState.Hurt, motor.VisualState);
    }

    [Fact]
    public void Kill_EntersDeadState_WhichIgnoresHurtAndInput()
    {
        var motor = new CharacterMotor(TestConfig());
        RunGrounded(motor, Intent(), 5);

        motor.Kill();

        Assert.Equal(CharacterVisualState.Dead, motor.VisualState);
        Assert.Equal(-280f, motor.Velocity.Y); // 死亡小跳（随世界 ×2）

        motor.Kill();
        motor.ForceHurt(
            DamageInfo.Create(
                damage: 1,
                knockbackHorizontal: 100f,
                knockbackVertical: 100f,
                sourceDirection: 1f
            )
        );
        motor.Process(Intent(moveAxis: 1f, jumpPressed: true, jumpHeld: true), Dt, Gravity);

        Assert.Equal(CharacterVisualState.Dead, motor.VisualState);
    }

    [Fact]
    public void Reset_RestoresGroundedLocomotion()
    {
        var motor = new CharacterMotor(TestConfig());
        motor.Kill();

        motor.Reset();

        Assert.Equal(CharacterVisualState.Idle, motor.VisualState);
        Assert.True(motor.IsOnFloor);
        Assert.Equal(Vector2.Zero, motor.Velocity);
    }

    [Fact]
    public void MoveAxis_UpdatesFacing()
    {
        var motor = new CharacterMotor(TestConfig());

        RunGrounded(motor, Intent(moveAxis: -1f), 3);

        Assert.Equal(-1, motor.Facing);
        Assert.True(motor.Velocity.X < 0f);
    }

    [Fact]
    public void MoveAxis_Release_DeceleratesToStopByFriction()
    {
        var motor = new CharacterMotor(TestConfig());
        RunGrounded(motor, Intent(moveAxis: 1f), 30); // 1000/60 ≈ 16.7/帧，30 帧内到达 MaxSpeed
        Assert.Equal(130f, motor.Velocity.X, precision: 1);

        RunGrounded(motor, Intent(), 30); // 摩擦 1400/60 ≈ 23.3/帧，6 帧内归零

        Assert.Equal(0f, motor.Velocity.X);
    }

    [Fact]
    public void MissingDashConfig_DashInputIsIgnored()
    {
        var motor = new CharacterMotor(TestConfig(withDash: false));

        RunGrounded(motor, Intent(dashPressed: true), 5);

        Assert.Equal(CharacterVisualState.Idle, motor.VisualState);
    }
}
