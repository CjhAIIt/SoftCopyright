namespace SoftCopyright.Client.ViewModels;

/// <summary>下拉候选项：值是规范选项码，标签是官方中文表述。</summary>
public sealed class OptionItem
{
    public OptionItem(string value, string label, string? note = null)
    {
        Value = value;
        Label = label;
        Note = note;
    }

    /// <summary>规范选项码。</summary>
    public string Value { get; }

    /// <summary>官方中文标签。</summary>
    public string Label { get; }

    /// <summary>官方填写说明摘要。</summary>
    public string? Note { get; }

    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    public override string ToString() => Label;
}
