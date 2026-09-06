using Godot;

namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>
/// 表现层：订阅玩家重生事件，让 Phantom Camera 瞬移到重生点——
/// 阻尼相机会平滑横穿全图，重生必须硬切。按约定名发现同级 PhantomCamera2D。
/// </summary>
public partial class CameraRig : Node
{
    private Character _player;
    private Node _pcam;

    /// <summary>由编排者在装配时调用（CameraRig 仅存在于玩家场景）。</summary>
    public void Bind(Character player)
    {
        _pcam ??= GetParent()?.GetNodeOrNull("PlayerPhantomCamera2D");
        Unbind();
        _player = player;
        _player.Respawned += Snap;
    }

    private void Snap() => _pcam?.Call("teleport_position");

    private void Unbind()
    {
        if (_player == null)
        {
            return;
        }
        _player.Respawned -= Snap;
        _player = null;
    }

    public override void _ExitTree() => Unbind();
}
