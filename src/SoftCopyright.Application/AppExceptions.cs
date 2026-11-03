namespace SoftCopyright.Application;

/// <summary>用例层异常，携带可映射到 HTTP 状态码的错误码。</summary>
public class AppException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>资源不存在。</summary>
public sealed class NotFoundException(string message) : AppException("not_found", message);

/// <summary>状态冲突（阶段不允许、乐观并发失败、门禁未通过）。</summary>
public sealed class ConflictException(string message) : AppException("conflict", message);

/// <summary>输入校验失败。</summary>
public sealed class InputValidationException(string message) : AppException("validation", message);
