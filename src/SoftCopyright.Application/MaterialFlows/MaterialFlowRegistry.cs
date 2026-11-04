using SoftCopyright.Domain;

namespace SoftCopyright.Application.MaterialFlows;

/// <summary>材料流程注册表：按材料类型取对应的流程描述符。</summary>
public sealed class MaterialFlowRegistry
{
    private readonly Dictionary<MaterialKind, IMaterialFlowDescriptor> _flows;

    public MaterialFlowRegistry(SoftCopyrightMaterialFlow softCopyright, PatentMaterialFlow patent)
    {
        _flows = new Dictionary<MaterialKind, IMaterialFlowDescriptor>
        {
            [MaterialKind.SoftwareCopyright] = softCopyright,
            [MaterialKind.Patent] = patent
        };
    }

    /// <summary>全部可选流程，顺序即入口页展示顺序。</summary>
    public IReadOnlyList<IMaterialFlowDescriptor> All => _flows.Values.ToList();

    /// <summary>取指定材料类型的流程描述符。</summary>
    public IMaterialFlowDescriptor Get(MaterialKind kind) =>
        _flows.TryGetValue(kind, out var flow)
            ? flow
            : throw new InvalidOperationException($"未登记的材料类型：{kind}");
}
