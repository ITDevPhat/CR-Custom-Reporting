using System.Globalization;

namespace Report.Infrastructure.Persistence.Sql;

internal static class Row
{
    public static string GetString(dynamic row, string name, string defaultValue = "")
        => Convert.ToString(Get(row, name), CultureInfo.InvariantCulture) ?? defaultValue;


    public static string GetFirstString(dynamic row, string defaultValue, params string[] names)
    {
        foreach (var name in names)
        {
            var value = Get(row, name);
            if (value is not null) return Convert.ToString(value, CultureInfo.InvariantCulture) ?? defaultValue;
        }

        return defaultValue;
    }

    public static bool GetBool(dynamic row, string name, bool defaultValue = false)
    {
        var value = Get(row, name);
        if (value is null) return defaultValue;
        if (value is bool b) return b;
        return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    public static int GetInt(dynamic row, string name, int defaultValue = 0)
    {
        var value = Get(row, name);
        return value is null ? defaultValue : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    public static byte? GetByte(dynamic row, string name)
    {
        var value = Get(row, name);
        return value is null ? null : Convert.ToByte(value, CultureInfo.InvariantCulture);
    }

    public static short? GetShort(dynamic row, string name)
    {
        var value = Get(row, name);
        return value is null ? null : Convert.ToInt16(value, CultureInfo.InvariantCulture);
    }

    public static int? GetNullableInt(dynamic row, string name)
    {
        var value = Get(row, name);
        return value is null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    public static decimal GetDecimal(dynamic row, string name, decimal defaultValue = 0m)
    {
        var value = Get(row, name);
        return value is null ? defaultValue : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    public static object? Get(dynamic row, string name)
    {
        if (row is IDictionary<string, object> dict && dict.TryGetValue(name, out var value)) return value;
        var property = row.GetType().GetProperty(name);
        return property?.GetValue(row);
    }
}
