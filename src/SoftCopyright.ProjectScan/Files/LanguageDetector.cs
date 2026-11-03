namespace SoftCopyright.ProjectScan.Files;

/// <summary>语言猜测：扩展名只是提示，同时结合内容特征，避免漏掉未知扩展名的源码。</summary>
public static class LanguageDetector
{
    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "C#",
        [".csproj"] = "C#",
        [".sln"] = "C#",
        [".vb"] = "VB.NET",
        [".fs"] = "F#",
        [".fsproj"] = "F#",
        [".js"] = "JavaScript",
        [".mjs"] = "JavaScript",
        [".cjs"] = "JavaScript",
        [".jsx"] = "JavaScript",
        [".ts"] = "TypeScript",
        [".tsx"] = "TypeScript",
        [".vue"] = "Vue",
        [".svelte"] = "Svelte",
        [".py"] = "Python",
        [".pyw"] = "Python",
        [".java"] = "Java",
        [".kt"] = "Kotlin",
        [".kts"] = "Kotlin",
        [".go"] = "Go",
        [".rs"] = "Rust",
        [".rb"] = "Ruby",
        [".php"] = "PHP",
        [".c"] = "C",
        [".h"] = "C",
        [".cpp"] = "C++",
        [".cc"] = "C++",
        [".hpp"] = "C++",
        [".swift"] = "Swift",
        [".m"] = "Objective-C",
        [".dart"] = "Dart",
        [".scala"] = "Scala",
        [".sh"] = "Shell",
        [".bash"] = "Shell",
        [".ps1"] = "PowerShell",
        [".bat"] = "批处理",
        [".cmd"] = "批处理",
        [".sql"] = "SQL",
        [".html"] = "HTML",
        [".htm"] = "HTML",
        [".css"] = "CSS",
        [".scss"] = "SCSS",
        [".less"] = "Less",
        [".json"] = "JSON",
        [".yaml"] = "YAML",
        [".yml"] = "YAML",
        [".toml"] = "TOML",
        [".xml"] = "XML",
        [".md"] = "Markdown",
        [".txt"] = "文本",
        [".ini"] = "INI",
        [".cfg"] = "INI",
        [".properties"] = "Properties",
        [".gradle"] = "Gradle",
        [".wxml"] = "WXML",
        [".wxss"] = "WXSS",
        [".wxs"] = "WXS"
    };

    private static readonly Dictionary<string, string> ByFileName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dockerfile"] = "Dockerfile",
        ["Makefile"] = "Makefile",
        ["CMakeLists.txt"] = "CMake",
        ["pom.xml"] = "Maven",
        ["build.gradle"] = "Gradle",
        ["build.gradle.kts"] = "Gradle",
        ["package.json"] = "Node 配置",
        ["pyproject.toml"] = "Python 配置",
        ["requirements.txt"] = "Python 配置",
        ["cargo.toml"] = "Rust 配置"
    };

    /// <summary>根据路径与内容猜测语言；无法判断时返回 null。</summary>
    public static string? Detect(string relativePath, string? headText)
    {
        var fileName = Path.GetFileName(relativePath);
        if (ByFileName.TryGetValue(fileName, out var byName))
        {
            return byName;
        }

        var extension = Path.GetExtension(relativePath);
        if (extension.Length > 0 && ByExtension.TryGetValue(extension, out var byExtension))
        {
            return byExtension;
        }

        if (string.IsNullOrWhiteSpace(headText))
        {
            return null;
        }

        return DetectByContent(headText);
    }

    private static string? DetectByContent(string text)
    {
        var head = text.Length > 2000 ? text[..2000] : text;
        var trimmed = head.TrimStart();

        if (trimmed.StartsWith("#!", StringComparison.Ordinal))
        {
            if (trimmed.Contains("python", StringComparison.OrdinalIgnoreCase))
            {
                return "Python";
            }

            if (trimmed.Contains("node", StringComparison.OrdinalIgnoreCase))
            {
                return "JavaScript";
            }

            if (trimmed.Contains("bash", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("/bin/sh", StringComparison.Ordinal))
            {
                return "Shell";
            }
        }

        if (trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
        {
            return "XML";
        }

        if (trimmed.StartsWith("<?php", StringComparison.OrdinalIgnoreCase))
        {
            return "PHP";
        }

        if (trimmed.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
        {
            return "HTML";
        }

        if (trimmed.StartsWith("using System", StringComparison.Ordinal) ||
            trimmed.StartsWith("namespace ", StringComparison.Ordinal) && trimmed.Contains(";", StringComparison.Ordinal))
        {
            return "C#";
        }

        if (trimmed.Contains("def ", StringComparison.Ordinal) &&
            (trimmed.Contains("import ", StringComparison.Ordinal) || trimmed.Contains("print(", StringComparison.Ordinal)))
        {
            return "Python";
        }

        if (trimmed.Contains("function ", StringComparison.Ordinal) && trimmed.Contains("=>", StringComparison.Ordinal))
        {
            return "JavaScript";
        }

        if (trimmed.StartsWith("package ", StringComparison.Ordinal) && trimmed.Contains(";", StringComparison.Ordinal))
        {
            return "Java";
        }

        return null;
    }
}
