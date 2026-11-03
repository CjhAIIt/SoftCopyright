using SoftCopyright.Application.Services;
using SoftCopyright.Domain;

namespace SoftCopyright.Tests.Unit;

/// <summary>申请案例阶段状态机测试。</summary>
public sealed class CaseStateMachineTests
{
    private readonly CaseWorkflow _workflow = new();

    [Theory]
    [InlineData(CaseStage.Created, CaseStage.Uploaded)]
    [InlineData(CaseStage.Uploaded, CaseStage.Scanning)]
    [InlineData(CaseStage.Scanning, CaseStage.AnalysisReady)]
    [InlineData(CaseStage.AnalysisReady, CaseStage.ApplicantInfoPending)]
    [InlineData(CaseStage.ApplicantInfoPending, CaseStage.CodeSelectionPending)]
    [InlineData(CaseStage.CodeSelectionPending, CaseStage.DraftReady)]
    [InlineData(CaseStage.DraftReady, CaseStage.ReviewPending)]
    [InlineData(CaseStage.ReviewPending, CaseStage.Validating)]
    [InlineData(CaseStage.Validating, CaseStage.PackageReady)]
    [InlineData(CaseStage.PackageReady, CaseStage.Archived)]
    public void 主流程迁移合法(CaseStage from, CaseStage to)
    {
        Assert.True(_workflow.CanTransition(from, to));
    }

    [Theory]
    [InlineData(CaseStage.Created, CaseStage.PackageReady)]
    [InlineData(CaseStage.Uploaded, CaseStage.Archived)]
    [InlineData(CaseStage.AnalysisReady, CaseStage.PackageReady)]
    [InlineData(CaseStage.Archived, CaseStage.Created)]
    public void 跨阶段迁移会被拒绝(CaseStage from, CaseStage to)
    {
        Assert.False(_workflow.CanTransition(from, to));
    }

    [Fact]
    public void 失败状态可以回到任意在途阶段()
    {
        Assert.True(_workflow.CanTransition(CaseStage.Failed, CaseStage.Scanning));
        Assert.True(_workflow.CanTransition(CaseStage.Failed, CaseStage.Uploaded));
    }

    [Fact]
    public void 流水线包含全部阶段且顺序正确()
    {
        Assert.Equal(CaseStage.Created, _workflow.Pipeline[0]);
        Assert.Equal(CaseStage.Archived, _workflow.Pipeline[^1]);
        Assert.Equal(11, _workflow.Pipeline.Count);
    }
}
