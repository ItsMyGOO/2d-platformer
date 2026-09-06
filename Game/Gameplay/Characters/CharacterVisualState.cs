namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>
/// 表现层使用的动画提示状态，由行为状态类报告。
/// 行为状态（决策）与视觉状态（动画）分离：Grounded/Airborne 内部
/// 按速度细分 idle/run/jump/fall，保证动画与真实运动一致。
/// </summary>
public enum CharacterVisualState
{
    Idle,
    Run,
    Jump,
    Fall,
    Dash,
    Attack,
    Hurt,
    Dead,
}
