using Godot;

namespace GodotGameTemplate.Gameplay.Characters.InputSources;

/// <summary>玩家输入：读取 InputMap 中定义的动作，翻译为意图。</summary>
public partial class PlayerInputSource : InputSource
{
    [Export]
    private string _moveLeftAction = "move_left";

    [Export]
    private string _moveRightAction = "move_right";

    [Export]
    private string _jumpAction = "jump";

    [Export]
    private string _dashAction = "dash";

    [Export]
    private string _attackAction = "attack";

    [Export]
    private string _castAction = "cast";

    [Export]
    private string _moveDownAction = "move_down";

    public override InputIntent Poll(float delta) =>
        InputIntent.Create(
            Input.GetAxis(_moveLeftAction, _moveRightAction),
            Input.IsActionJustPressed(_jumpAction),
            Input.IsActionPressed(_jumpAction),
            Input.IsActionJustPressed(_dashAction),
            Input.IsActionJustPressed(_attackAction),
            Input.IsActionJustPressed(_castAction),
            Input.IsActionPressed(_moveDownAction)
        );
}
