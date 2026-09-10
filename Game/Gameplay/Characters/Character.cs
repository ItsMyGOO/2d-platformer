using System;
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
    private CameraRig _cameraRig;

    /// <summary>逻辑层宿主：编排者内部驱动，表现层与探针只读。</summary>
    public CharacterMotor Motor { get; private set; }

    /// <summary>生命组件；受击入口是 <see cref="OnHurt"/>，外部不得直接改血量。</summary>
    public Health Health { get; private set; }

    /// <summary>重生完成时触发（表现层订阅，如相机瞬移到出生点）。</summary>
    public event Action Respawned;

    /// <summary>该单位死亡时触发一次（血量归零与环境即死统一走此事件；
    /// 表现层订阅做死亡反馈。血量明细仍看 Health.Died）。</summary>
    public event Action Died;

    /// <summary>是否玩家（由输入源类型推导；弹体等子系统据此选择目标层）。</summary>
    public bool IsPlayer => _isPlayer;

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
        if (_isPlayer)
        {
            AddToGroup("player"); // 全树寻址入口（BombEmitter 等子系统）
        }

        Health = new Health(_config.MaxHP, _config.InvincibilityTime);
        Health.Died += OnDied;

        Motor = new CharacterMotor(_config.ToData());
        Motor.AttackStarted += () => _hitbox?.BeginSwing();
        Motor.AttackActiveChanged += active => _hitbox?.SetActive(active);
        if (_config.Attack != null)
        {
            _hitbox?.Configure(
                _config.Attack.Damage,
                _config.Attack.KnockbackHorizontal,
                _config.Attack.KnockbackVertical
            );
            this.FindDescendant<ContactDamager>()
                ?.Configure(
                    _config.Attack.Damage,
                    _config.Attack.KnockbackHorizontal,
                    _config.Attack.KnockbackVertical
                );
        }

        if (Motor.Soul != null)
        {
            _hitbox.HitConfirmed += () => Motor.GainSoul(); // 近战命中攒魂
        }
        _presenter?.Bind(Motor, Health);
        _screenShake = this.FindDescendant<ScreenShake>();
        _screenShake?.Bind(Health, _hitbox);
        _cameraRig = this.FindDescendant<CameraRig>();
        _cameraRig?.Bind(this);
        this.FindDescendant<ProjectileEmitter>()?.Bind(this);
        this.FindDescendant<CastEmitter>()?.Bind(this);
        this.FindDescendant<BombEmitter>()?.Bind(this);

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

    /// <summary>受击入口（Hurtbox 转发或测试直调）：先过无敌帧与死亡判定，结算成功才进硬直。返回是否真实结算。</summary>
    public bool OnHurt(DamageInfo info)
    {
        if (!Health.TryApplyDamage(info))
        {
            return false;
        }
        Motor.ForceHurt(info);
        return true;
    }

    /// <summary>环境即死（落坑等）：不经过血量，直接进入死亡流程。重复调用安全。</summary>
    public void KillInstantly()
    {
        if (Motor.VisualState == CharacterVisualState.Dead)
        {
            return;
        }
        DieAndSchedule();
    }

    private void OnDied() => DieAndSchedule();

    private void DieAndSchedule()
    {
        Died?.Invoke();
        Motor.Kill();
        _hitbox?.SetActive(false);
        _deathCountdown = DeathDuration;
    }

    /// <summary>重生点被更新时触发（跨房 / 检查点；持久层订阅做自动存档）。</summary>
    public event Action<Vector2> SpawnPointChanged;

    /// <summary>更新重生点（世界流式与检查点调用；默认为初始出生点）。</summary>
    public void SetSpawnPoint(Vector2 globalPosition)
    {
        _spawnPosition = globalPosition;
        SpawnPointChanged?.Invoke(globalPosition);
    }

    /// <summary>瞬移到指定位置（继续游戏落到存档点）：清速度、立即触发相机瞬移。</summary>
    public void PlaceAt(Vector2 globalPosition)
    {
        GlobalPosition = globalPosition;
        Velocity = Vector2.Zero;
        Respawned?.Invoke();
    }

    private void Respawn()
    {
        GlobalPosition = _spawnPosition;
        Velocity = Vector2.Zero;
        Health.RestoreFull();
        Motor.Reset();
        Respawned?.Invoke();
    }
}
