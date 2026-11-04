using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace SoftCopyright.Infrastructure;

/// <summary>
/// 选项绑定辅助。
///
/// IConfiguration 对**数组属性**是"追加"而不是"替换"：
/// 选项类里给了默认值、配置文件里又写了同名项时，两边会叠加成重复列表
/// （例如 Git 域名白名单变成 github.com、gitee.com、gitlab.com、github.com…）。
///
/// 这里在绑定前把数组清空、绑定后对"配置未提供该项"的属性还原默认值，
/// 做到"配置覆盖默认、缺省沿用默认"。
/// </summary>
public static class OptionsBinder
{
    /// <summary>把配置节绑定到选项实例（数组不会被重复叠加）。</summary>
    public static void Bind<T>(T options, IConfiguration section) where T : class
    {
        var defaults = new List<(object Owner, PropertyInfo Property, Array Value)>();

        CollectArrayDefaults(options, defaults);
        ClearArrays(options);
        section.Bind(options);
        RestoreMissingDefaults(defaults);
    }

    private static void CollectArrayDefaults(object instance, List<(object, PropertyInfo, Array)> defaults)
    {
        foreach (var property in instance.GetType().GetProperties())
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            if (property.PropertyType.IsArray)
            {
                if (property.GetValue(instance) is Array array && array.Length > 0)
                {
                    defaults.Add((instance, property, (Array)array.Clone()));
                }

                continue;
            }

            // 继续下钻嵌套的选项对象（例如 MaterialRules.Fields）。
            if (property.PropertyType.IsClass &&
                property.PropertyType != typeof(string) &&
                property.GetValue(instance) is { } child)
            {
                CollectArrayDefaults(child, defaults);
            }
        }
    }

    private static void ClearArrays(object instance)
    {
        foreach (var property in instance.GetType().GetProperties())
        {
            if (!property.CanWrite || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            if (property.PropertyType.IsArray)
            {
                property.SetValue(instance, Array.CreateInstance(property.PropertyType.GetElementType()!, 0));
                continue;
            }

            if (property.PropertyType.IsClass &&
                property.PropertyType != typeof(string) &&
                property.GetValue(instance) is { } child)
            {
                ClearArrays(child);
            }
        }
    }

    private static void RestoreMissingDefaults(List<(object Owner, PropertyInfo Property, Array Value)> defaults)
    {
        foreach (var (owner, property, value) in defaults)
        {
            if (property.GetValue(owner) is Array current && current.Length == 0)
            {
                property.SetValue(owner, value);
            }
        }
    }
}
