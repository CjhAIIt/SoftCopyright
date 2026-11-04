using SoftCopyright.Domain;

namespace SoftCopyright.Application.Confirmation;

/// <summary>确认门禁注册表：按材料类型挑选门禁实现。</summary>
public sealed class ConfirmationGateRegistry
{
    private readonly Dictionary<MaterialKind, IConfirmationGate> _gates;

    public ConfirmationGateRegistry(IEnumerable<IConfirmationGate> gates)
    {
        _gates = gates.ToDictionary(g => g.Kind);
    }

    /// <summary>取指定材料类型的门禁。</summary>
    public IConfirmationGate Get(MaterialKind kind) =>
        _gates.TryGetValue(kind, out var gate)
            ? gate
            : throw new InvalidOperationException($"未登记的材料确认门禁：{kind}");
}
