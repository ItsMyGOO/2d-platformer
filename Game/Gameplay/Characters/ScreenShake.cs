using Godot;
using GodotGameTemplate.Gameplay.Characters.Combat;

namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>
/// 表现层：把玩家的攻击命中/受击/落地事件转发给本场景内的 Phantom Camera 噪声发射器。
/// 事件源由编排者经 Bind() 注入；Enabled 是震屏总开关，关闭后事件仍订阅但不触发。
/// 发射器为 GDScript 节点，按约定名发现（AttackNoiseEmitter2D / HurtNoiseEmitter2D / LandNoiseEmitter2D）。
/// </summary>
public partial class ScreenShake : Node
{
    [Export]
    public bool Enabled { get; set; } = true;

    private CharacterMotor _motor;
    private Health _health;
    private Hitbox _hitbox;
    private Node _attackEmitter;
    private Node _hurtEmitter;
    private Node _landEmitter;

    /// <summary>
    /// 由编排者在逻辑层创建后调用，绑定引用并订阅运动/受击/命中事件。
    /// hitbox 可为 null（不具备攻击能力时不订阅命中抖动）。
    /// </summary>
    public void Bind(CharacterMotor motor, Health health, Hitbox hitbox)
    {
        Node parent = GetParent();
        _attackEmitter ??= parent?.GetNodeOrNull("AttackNoiseEmitter2D");
        _hurtEmitter ??= parent?.GetNodeOrNull("HurtNoiseEmitter2D");
        _landEmitter ??= parent?.GetNodeOrNull("LandNoiseEmitter2D");
        Unbind();
        _motor = motor;
        _health = health;
        _hitbox = hitbox;
        _motor.Landed += OnLanded;
        _health.Damaged += OnDamaged;
        if (_hitbox != null)
        {
            _hitbox.HitConfirmed += OnAttackHit;
        }
    }

    private void Unbind()
    {
        if (_motor == null)
        {
            return;
        }
        _motor.Landed -= OnLanded;
        _health.Damaged -= OnDamaged;
        if (_hitbox != null)
        {
            _hitbox.HitConfirmed -= OnAttackHit;
        }
        _motor = null;
        _health = null;
        _hitbox = null;
    }

    public override void _ExitTree() => Unbind();

    private void OnAttackHit() => Emit(_attackEmitter);

    private void OnDamaged(DamageInfo info) => Emit(_hurtEmitter);

    private void OnLanded() => Emit(_landEmitter);

    private void Emit(Node emitter)
    {
        if (Enabled && emitter != null)
        {
            emitter.Call("emit"); // GDScript 方法：触发一次噪声
        }
    }
}
