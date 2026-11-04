namespace SoftCopyright.Domain;

/// <summary>材料流程中的一个工作页标签。</summary>
/// <param name="Key">标签标识，用于标记当前激活项。</param>
/// <param name="Label">界面显示名称。</param>
/// <param name="Route">相对案例 ID 的路由片段，例如 <c>applicant</c>。</param>
public sealed record MaterialTabDefinition(string Key, string Label, string Route);

/// <summary>
/// 材料流程描述符。
///
/// 软著登记与专利申请是两条**相互独立的业务流程**：阶段含义、页面构成、
/// 前置门禁、产物清单与材料包命名都不相同。本接口把两者的差异集中到两份实现里，
/// 上层代码只读取描述符，不再用 <c>if (materialKind == ...)</c> 到处分叉。
/// </summary>
public interface IMaterialFlowDescriptor
{
    /// <summary>本流程对应的材料类型。</summary>
    MaterialKind Kind { get; }

    /// <summary>入口名称，例如「软著材料书写」。</summary>
    string DisplayName { get; }

    /// <summary>列表与徽标上的短名称，例如「软著登记」。</summary>
    string ShortName { get; }

    /// <summary>流程一句话说明，用于入口页与列表页。</summary>
    string Summary { get; }

    /// <summary>产物清单说明，用于入口页。</summary>
    string Deliverables { get; }

    /// <summary>阶段条文案，与 <see cref="SoftCopyright.Domain.Contracts.ICaseWorkflow.Pipeline"/> 一一对应。</summary>
    IReadOnlyList<string> StageLabels { get; }

    /// <summary>工作页标签。</summary>
    IReadOnlyList<MaterialTabDefinition> Tabs { get; }

    /// <summary>本流程自己的工作区路径，入口页据此跳转。</summary>
    string WorkspacePath { get; }

    /// <summary>是否需要勾选代码范围。专利申请文件不含代码鉴别材料，因此为 false。</summary>
    bool RequiresCodeSelection { get; }

    /// <summary>材料包文件名。</summary>
    string PackageFileName { get; }

    /// <summary>材料包在存储目录中的相对路径。</summary>
    string PackageRelativePath { get; }
}
