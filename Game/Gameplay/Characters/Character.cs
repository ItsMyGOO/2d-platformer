using Godot;
using GodotGameTemplate.Gameplay.Characters.InputSources;

namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>
/// 编排者：每物理帧按固定顺序串联 输入→意图→逻辑→物理→表现。
/// 自身不含任何玩法规则；玩家与敌人场景复用同一个类。
/// 输入源与表现层默认按类型自动发现（C# 的 Node 导出在场景实例化时
/// 无法解析前向 NodePath），也可通过导出属性显式指定。
/// </summary>
public partial class Character : CharacterBody2D
{
    [Export]
    private CharacterConfig _config;

    [Export]
    private InputSource _inputSource;

    [Export]
    private CharacterPresenter _presenter;

    public CharacterMotor Motor { get; private set; }

    public override void _Ready()
    {
        _inputSource ??= FindDescendant<InputSource>(this);
        _presenter ??= FindDescendant<CharacterPresenter>(this);
        Motor = new CharacterMotor(_config);
        _presenter?.Bind(Motor);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        InputIntent intent =
            _inputSource != null
                ? _inputSource.Poll(dt)
                : InputIntent.Create(0f, jumpPressed: false, jumpHeld: false);
        Motor.Process(intent, dt, GetGravity().Y);

        Velocity = Motor.Velocity;
        MoveAndSlide();
        Motor.PostPhysics(IsOnFloor(), Velocity);
        _inputSource?.NotifyGrounded(IsOnFloor());

        _presenter?.Sync();
    }

    /// <summary>深度优先查找第一个指定类型的后代节点。</summary>
    private static T FindDescendant<T>(Node root)
        where T : Node
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is T typed)
            {
                return typed;
            }
            Node found = FindDescendant<T>(child);
            if (found != null)
            {
                return (T)found;
            }
        }
        return null;
    }
}
