using System.Text;
using CashFlowPlanner.BlazorWasm;
using CashFlowPlanner.BlazorWasm.Resources;
using Microsoft.Extensions.Localization;

namespace CashFlowPlanner.BlazorWasm.Services;

public sealed class EnumLocalizer
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    public EnumLocalizer(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }

    public string Translate<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var enumType = typeof(TEnum);
        var enumName = value.ToString();

        var key = CreateKey(enumType, enumName);
        var localized = _localizer[key];

        if (!localized.ResourceNotFound)
        {
            return localized.Value;
        }

        return SplitPascalCase(enumName);
    }

    public string TranslateNullable<TEnum>(
        TEnum? value,
        string emptyText = "-")
        where TEnum : struct, Enum
    {
        return value is null
            ? emptyText
            : Translate(value.Value);
    }

    public string Translate(Enum value)
    {
        var enumType = value.GetType();
        var enumName = value.ToString();

        var key = CreateKey(enumType, enumName);
        var localized = _localizer[key];

        if (!localized.ResourceNotFound)
        {
            return localized.Value;
        }

        return SplitPascalCase(enumName);
    }

    private static string CreateKey(Type enumType, string enumName)
    {
        return $"Enum.{enumType.Name}.{enumName}";
    }

    private static string SplitPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];

            if (i > 0 &&
                char.IsUpper(current) &&
                !char.IsWhiteSpace(value[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(current);
        }

        return builder.ToString();
    }
}