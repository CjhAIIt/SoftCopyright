using SoftCopyright.Domain;

namespace SoftCopyright.Application.MaterialFlows;

/// <summary>软件著作权登记流程描述符。</summary>
public sealed class SoftCopyrightMaterialFlow : IMaterialFlowDescriptor
{
    public MaterialKind Kind => MaterialKind.SoftwareCopyright;

    public string DisplayName => "软著材料书写";

    public string ShortName => "软著登记";

    public string Summary => "面向软件著作权登记：把真实代码仓库的扫描结果整理成申请表信息、操作手册与代码鉴别材料。";

    public string Deliverables => "申请表信息 · 操作手册 · 代码鉴别材料 · 证据清单 · 校验报告";

    public IReadOnlyList<string> StageLabels =>
    [
        "创建", "上传", "扫描", "分析完成", "申请信息", "代码范围", "草稿就绪", "材料审核", "校验", "材料包", "归档"
    ];

    public IReadOnlyList<MaterialTabDefinition> Tabs =>
    [
        new("import", "导入仓库", "import"),
        new("analysis", "分析与证据", "analysis"),
        new("applicant", "申请信息", "applicant"),
        new("code", "代码材料", "code"),
        new("materials", "材料与导出", "materials")
    ];

    public bool RequiresCodeSelection => true;

    public string WorkspacePath => "/softcopyright";

    public string PackageFileName => "软著申请材料包.zip";

    public string PackageRelativePath => "材料包/软著申请材料包.zip";
}
