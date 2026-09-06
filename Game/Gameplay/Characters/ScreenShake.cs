using Godot;
using GodotGameTemplate.Gameplay.Characters.Combat;

namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>
/// 表现层：把玩家的出招/受击/落地事件转发给本场景内的 Phantom Camera 噪声发射器。
/// 事件源由编排者经 Bind() 注入；Enabled 是震屏总开关，关闭后事件仍订阅但不触发。
/// 发射器为 GDScript 节点，按约定名发现（AttackNoiseEmitter2D / HurtNoiseEmitter2D / LandNoiseEmitter2D）。
/// </summary>
public partial class ScreenShake : Node
{
    [Export]
    public bool Enabled { get; set; } = true;

    private CharacterMotor _motor;
    private Health _health;
    private Node _attackEmitter;
    private Node _hurtEmitter;
    private Node _landEmitter;

    /// <summary>由编排者在逻辑层创建后调用，绑定引用并订阅运动与受击事件。</summary>
    public void Bind(CharacterMotor motor, Health health)
    {
        Node parent = GetParent();
        _attackEmitter ??= parent?.GetNodeOrNull("AttackNoiseEmitter2D");
        _hurtEmitter ??= parent?.GetNodeOrNull("HurtNoiseEmitter2D");
        _landEmitter ??= parent?.GetNodeOrNull("LandNoiseEmitter2D");
        Unbind();
        _motor = motor;
        _health = health;
        _motor.AttackStarted += OnAttackStarted;
        _motor.Landed += OnLanded;
        _health.Damaged += OnDamaged;
    }

    private void Unbind()
    {
        if (_motor == null)
        {
            return;
        }
        _motor.AttackStarted -= OnAttackStarted;
        _motor.Landed -= OnLanded;
        _health.Damaged -= OnDamaged;
        _motor = null;
        _health = null;
    }

    public override void _ExitTree() => Unbind();

    private void OnAttackStarted() => Emit(_attackEmitter);

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
