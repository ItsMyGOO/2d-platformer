using Godot;

namespace GodotGameTemplate.Gameplay.Characters;

/// <summary>节点树查找工具。C# 的 Node 导出无法解析前向 NodePath，
/// 节点引用统一走类型/约定名发现。</summary>
internal static class NodeExtensions
{
    /// <summary>深度优先查找第一个指定类型的后代节点。</summary>
    public static T FindDescendant<T>(this Node root)
        where T : Node
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is T typed)
            {
                return typed;
            }
            T found = child.FindDescendant<T>();
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }
}
