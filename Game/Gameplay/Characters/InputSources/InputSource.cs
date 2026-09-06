using Godot;

namespace GodotGameTemplate.Gameplay.Characters.InputSources;

/// <summary>
/// 输入层抽象：每物理帧被轮询一次，产出一帧意图。
/// 玩家与 AI 各实现一个子类，二者产出完全相同的意图、可随时互换——
/// 这是角色与敌人共用逻辑层的关键。输入层不直接驱动角色，只表达意图。
/// </summary>
public abstract partial class InputSource : Node
{
    /// <summary>轮询一帧意图。</summary>
    /// <param name="delta">物理帧时长（秒），供 AI 内部计时。</param>
    public abstract InputIntent Poll(float delta);

    /// <summary>由角色每帧回喂的真实物理事实（如是否站在地面），供 AI 决策使用。</summary>
    public virtual void NotifyGrounded(bool grounded) { }
}
