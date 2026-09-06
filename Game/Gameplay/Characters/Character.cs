using Godot;
using GodotGameTemplate.Gameplay.Characters.Combat;
using GodotGameTemplate.Gameplay.Characters.InputSources;

namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>
/// 编排者：每物理帧按固定顺序串联 输入→意图→逻辑→物理→表现，
/// 并接线战斗闭环（Hitbox/Hurtbox/Health/死亡重生）。
/// 自身不含任何玩法规则；玩家与敌人场景复用同一个类
/// （玩家身份由输入源类型推导：PlayerInputSource 即玩家）。
/// </summary>
public partial class Character : CharacterBody2D
{
    [Export]
    private CharacterConfig _config;

    [Export]
    private InputSource _inputSource;

    [Export]
    private CharacterPresenter _presenter;

    private ScreenShake _screenShake;

    public CharacterMotor Motor { get; private set; }

    public Health Health { get; private set; }

    private Hitbox _hitbox;
    private Hurtbox _hurtbox;
    private Vector2 _spawnPosition;
    private float _deathCountdown = -1f;
    private bool _isPlayer;

    private const float DeathDuration = 1f;

    public override void _Ready()
    {
        _inputSource ??= this.FindDescendant<InputSource>();
        _presenter ??= this.FindDescendant<CharacterPresenter>();
        _hitbox = this.FindDescendant<Hitbox>();
        _hurtbox = this.FindDescendant<Hurtbox>();
        _hurtbox?.Bind(this);
        _isPlayer = _inputSource is PlayerInputSource;

        Health = new Health(_config.MaxHP, _config.InvincibilityTime);
        Health.Died += OnDied;

        Motor = new CharacterMotor(_config.ToData());
        Motor.AttackStarted += () => _hitbox?.BeginSwing();
        Motor.AttackActiveChanged += active => _hitbox?.SetActive(active);
        _presenter?.Bind(Motor, Health);
        _screenShake = this.FindDescendant<ScreenShake>();
        _screenShake?.Bind(Motor, Health);

        _spawnPosition = GlobalPosition;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        Health.UpdateTimers(dt);

        InputIntent intent =
            _inputSource != null && Health != null && !Health.IsDead
                ? _inputSource.Poll(dt)
                : InputIntent.Create(0f, jumpPressed: false, jumpHeld: false);
        Motor.Process(intent, dt, GetGravity().Y);

        Velocity = Motor.Velocity;
        MoveAndSlide();
        Motor.PostPhysics(IsOnFloor(), Velocity);
        _inputSource?.NotifyGrounded(IsOnFloor());

        _presenter?.Sync();
    }

    public override void _Process(double delta)
    {
        if (_deathCountdown < 0f)
        {
            return;
        }
        _deathCountdown -= (float)delta;
        if (_deathCountdown <= 0f)
        {
            _deathCountdown = -1f;
            if (_isPlayer)
            {
                Respawn();
            }
            else
            {
                QueueFree(); // 敌人尸体消失
            }
        }
    }

    /// <summary>受击入口（Hurtbox 转发或测试直调）：先过无敌帧与死亡判定，再进硬直。</summary>
    public void OnHurt(DamageInfo info)
    {
        if (Health.TryApplyDamage(info))
        {
            Motor.ForceHurt(info);
        }
    }

    private void OnDied()
    {
        Motor.Kill();
        _hitbox?.SetActive(false);
        _deathCountdown = DeathDuration;
    }

    private void Respawn()
    {
        GlobalPosition = _spawnPosition;
        Velocity = Vector2.Zero;
        Health.RestoreFull();
        Motor.Reset();
    }
}
