using Godot;

namespace GodotGameTemplate.Gameplay.Characters.InputSources;

/// <summary>
/// 史莱姆 AI 输入：往返巡逻，撞墙或悬崖调头；
/// 窄沟（远端探测线仍能探到地面）会起跳跳过，过宽的沟直接调头。
/// 只产出与玩家完全相同的意图，不触碰任何逻辑层细节。
/// </summary>
public partial class SlimeAIInputSource : InputSource
{
    /// <summary>墙体探测：从身体中心水平指向前方。</summary>
    [Export]
    private RayCast2D _wallRay;

    /// <summary>近端崖沿探测：从身前下方指向前下方，探不到地面说明身前是沟。</summary>
    [Export]
    private RayCast2D _ledgeNearRay;

    /// <summary>远端崖沿探测：探得更远，用来区分「跳得过的窄沟」与「必须调头的宽沟」。</summary>
    [Export]
    private RayCast2D _ledgeFarRay;

    /// <summary>初始巡逻方向：1 右，-1 左。</summary>
    [Export]
    private int _initialDirection = -1;

    [Export]
    private float _flipCooldown = 0.25f;

    [Export]
    private float _jumpCooldown = 0.8f;

    private int _direction;
    private float _flipTimer;
    private float _jumpTimer;
    private bool _jumpPulse;
    private bool _grounded;

    public override void _Ready()
    {
        _direction = _initialDirection >= 0 ? 1 : -1;
        // 探测线默认按约定名在同级节点中发现（C# Node 导出无法解析前向 NodePath）。
        Node parent = GetParent();
        _wallRay ??= parent?.GetNodeOrNull<RayCast2D>("WallRay");
        _ledgeNearRay ??= parent?.GetNodeOrNull<RayCast2D>("LedgeNearRay");
        _ledgeFarRay ??= parent?.GetNodeOrNull<RayCast2D>("LedgeFarRay");
    }

    public override void NotifyGrounded(bool grounded) => _grounded = grounded;

    public override InputIntent Poll(float delta)
    {
        _flipTimer -= delta;
        _jumpTimer -= delta;

        if (_grounded && _flipTimer <= 0f)
        {
            AimRays();
            bool wallAhead = _wallRay != null && _wallRay.IsColliding();
            bool gapAhead = _ledgeNearRay != null && !_ledgeNearRay.IsColliding();
            if (wallAhead)
            {
                Turn();
            }
            else if (gapAhead)
            {
                bool gapTooWide = IsGapTooWide();
                if (gapTooWide)
                {
                    Turn();
                }
                else if (_jumpTimer <= 0f)
                {
                    _jumpPulse = true;
                    _jumpTimer = _jumpCooldown;
                }
            }
        }

        InputIntent intent = InputIntent.Create(_direction, _jumpPulse, jumpHeld: true);
        _jumpPulse = false;
        return intent;
    }

    private void Turn()
    {
        _direction = -_direction;
        _flipTimer = _flipCooldown;
    }

    /// <summary>
    /// 判断身前的沟是否宽过跳跃能力。远端线必须把起点挪过崖沿再探——
    /// 否则线段起点还在当前一侧的地面上，会先命中近侧地面而永远误判「跳得过」。
    /// </summary>
    private bool IsGapTooWide()
    {
        if (_ledgeFarRay == null)
        {
            return true;
        }
        _ledgeFarRay.Position = new Vector2(8f * _direction, 6f);
        _ledgeFarRay.TargetPosition = new Vector2(26f * _direction, 10f);
        _ledgeFarRay.ForceRaycastUpdate();
        return !_ledgeFarRay.IsColliding();
    }

    /// <summary>按当前方向摆好三根探测线，并强制同帧刷新命中结果。</summary>
    private void AimRays()
    {
        if (_wallRay != null)
        {
            _wallRay.Position = new Vector2(0f, -1f);
            _wallRay.TargetPosition = new Vector2(14f * _direction, 0f);
            _wallRay.ForceRaycastUpdate();
        }
        if (_ledgeNearRay != null)
        {
            _ledgeNearRay.Position = new Vector2(6f * _direction, 6f);
            _ledgeNearRay.TargetPosition = new Vector2(8f * _direction, 10f);
            _ledgeNearRay.ForceRaycastUpdate();
        }
        if (_ledgeFarRay != null)
        {
            _ledgeFarRay.Position = new Vector2(2f * _direction, 6f);
            _ledgeFarRay.TargetPosition = new Vector2(30f * _direction, 10f);
            _ledgeFarRay.ForceRaycastUpdate();
        }
    }
}
