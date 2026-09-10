using System;

namespace GodotGameTemplate.Gameplay.Characters.Combat;

/// <summary>
/// HK 式魂量（纯 C#）：近战命中积攒、剑气消耗。上限封顶、不足拒绝消费；
/// SoulChanged 供 HUD 魂条渲染。重生经 Clear() 归零，不持久化。
/// </summary>
public sealed class Soul
{
    private int _current;

    public Soul(int max)
    {
        if (max < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(max));
        }

        Max = max;
    }

    /// <summary>魂量上限。</summary>
    public int Max { get; }

    /// <summary>当前魂量。</summary>
    public int Current => _current;

    /// <summary>魂量变化（current, max），HUD 魂条订阅。</summary>
    public event Action<int, int> SoulChanged;

    /// <summary>积攒魂量（封顶；非正数与零上限忽略）。</summary>
    public void Gain(int amount)
    {
        if (amount <= 0 || Max == 0)
        {
            return;
        }

        int before = _current;
        _current = Math.Min(Max, _current + amount);
        if (_current != before)
        {
            SoulChanged?.Invoke(_current, Max);
        }
    }

    /// <summary>尝试消费：不足拒绝且不动值。</summary>
    public bool TrySpend(int amount)
    {
        if (amount <= 0 || _current < amount)
        {
            return false;
        }

        _current -= amount;
        SoulChanged?.Invoke(_current, Max);
        return true;
    }

    /// <summary>清空（重生归零）。</summary>
    public void Clear()
    {
        if (_current == 0)
        {
            return;
        }

        _current = 0;
        SoulChanged?.Invoke(_current, Max);
    }
}
