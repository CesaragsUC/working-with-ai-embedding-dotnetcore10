namespace Demo.Embedding.Web.Utils;

using System;

public static class FileSizeHelper
{
    public static string GetReadableFileSize(long bytes, int decimalPlaces = 1)
    {
        if (bytes < 0)
            throw new ArgumentOutOfRangeException(nameof(bytes));

        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{Math.Round(len, decimalPlaces)} {sizes[order]}";
    }
}
