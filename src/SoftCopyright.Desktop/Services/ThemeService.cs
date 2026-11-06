using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Styling;

namespace SoftCopyright.Desktop.Services;

/// <summary>主题服务：深浅双主题切换，并记住用户的选择。</summary>
public sealed class ThemeService
{
    private const string SettingsFileName = "ui-preferences.json";

    private readonly string _settingsPath;

    public ThemeService(string dataRoot)
    {
        _settingsPath = Path.Combine(dataRoot, SettingsFileName);
        IsDark = Load();
        Apply();
    }

    /// <summary>当前是否深色主题。</summary>
    public bool IsDark { get; private set; }

    /// <summary>切换主题。</summary>
    public void Toggle()
    {
        IsDark = !IsDark;
        Apply();
        Save();
    }

    /// <summary>直接指定主题（不写偏好文件），供无头渲染等场景使用。</summary>
    public void SetDark(bool dark)
    {
        IsDark = dark;
        Apply();
    }

    /// <summary>把当前主题应用到界面。</summary>
    public void Apply()
    {
        var app = Avalonia.Application.Current;
        if (app is not null)
        {
            app.RequestedThemeVariant = IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }

    private bool Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return false;
            }

            var json = File.ReadAllText(_settingsPath);
            var preferences = JsonSerializer.Deserialize<UiPreferences>(json, JsonOptions);
            return preferences?.Theme?.Equals("dark", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (Exception)
        {
            // 偏好文件损坏时按浅色主题启动，不影响使用。
            return false;
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var preferences = new UiPreferences { Theme = IsDark ? "dark" : "light" };
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(preferences, JsonOptions));
        }
        catch (Exception)
        {
            // 写不进去也只是下次不记住偏好，不影响使用。
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class UiPreferences
    {
        public string? Theme { get; set; }
    }
}
