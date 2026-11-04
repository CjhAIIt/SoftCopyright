using System.Diagnostics;

namespace SoftCopyright.Tests.Support;

/// <summary>测试用本地 Git 仓库构造器（内容全部合成）。</summary>
public static class GitFixture
{
    /// <summary>创建一个包含 README 与示例源码的本地 git 仓库。</summary>
    public static string CreateRepository(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));

        File.WriteAllText(
            Path.Combine(root, "README.md"),
            "# 演示仓库\n\n用于验证 Git 导入通道的合成样例仓库。\n\n## 功能说明\n\n### 入口程序\n\n通过 Program.cs 启动。\n");

        File.WriteAllText(
            Path.Combine(root, "src", "Program.cs"),
            "using System;\n\nnamespace Demo;\n\npublic static class Program\n{\n    public static void Main() => Console.WriteLine(\"demo\");\n}\n");

        File.WriteAllText(
            Path.Combine(root, "package.json"),
            "{\n  \"name\": \"git-import-demo\",\n  \"version\": \"3.1.0\",\n  \"description\": \"用于验证 Git 导入的合成样例\"\n}\n");

        Run(root, "init");
        Run(root, "add", "-A");
        Run(root, "-c", "user.email=test@example.com", "-c", "user.name=test", "commit", "-m", "init");
        return root;
    }

    private static void Run(string workingDirectory, params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in args)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', args)} 失败：{stderr}{stdout}");
        }
    }
}
