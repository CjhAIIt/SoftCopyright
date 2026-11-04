using SoftCopyright.Domain;

namespace SoftCopyright.Application.MaterialFlows;

/// <summary>专利申请文件流程描述符。</summary>
public sealed class PatentMaterialFlow : IMaterialFlowDescriptor
{
    public MaterialKind Kind => MaterialKind.Patent;

    public string DisplayName => "专利材料书写";

    public string ShortName => "专利申请";

    public string Summary => "面向发明与实用新型专利：以技术交底书为唯一真源，生成权利要求书、说明书与摘要。";

    public string Deliverables => "权利要求书 · 说明书 · 摘要 · 技术交底书";

    public IReadOnlyList<string> StageLabels =>
    [
        "创建", "上传", "扫描", "分析完成", "交底书", "权利要求", "草稿就绪", "材料审核", "校验", "材料包", "归档"
    ];

    public IReadOnlyList<MaterialTabDefinition> Tabs =>
    [
        new("import", "导入仓库", "import"),
        new("analysis", "分析与证据", "analysis"),
        new("patent", "专利交底书", "patent"),
        new("materials", "材料与导出", "materials")
    ];

    /// <summary>专利申请文件不提交代码鉴别材料，因此无需勾选代码范围。</summary>
    public bool RequiresCodeSelection => false;

    public string WorkspacePath => "/patent";

    public string PackageFileName => "专利申请材料包.zip";

    public string PackageRelativePath => "材料包/专利申请材料包.zip";
}
