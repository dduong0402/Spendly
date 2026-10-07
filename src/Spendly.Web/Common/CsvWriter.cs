using System.Text;

namespace Spendly.Web.Common;

/// <summary>Ghi CSV theo RFC 4180, có chặn "CSV injection" (ô bắt đầu bằng = + - @ bị Excel hiểu là công thức).</summary>
public static class CsvWriter
{
    private static readonly char[] FormulaStarts = { '=', '+', '-', '@', '\t', '\r' };

    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (Array.IndexOf(FormulaStarts, value[0]) >= 0)
        {
            value = "'" + value;
        }

        var needsQuotes = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    public static string Row(IEnumerable<string?> cells) => string.Join(",", cells.Select(Escape));

    /// <summary>
    /// Dựng nội dung file: BOM UTF-8 (để Excel đọc đúng tiếng Việt) + dòng "sep=," (Excel tiếng Việt dùng dấu ; làm ngăn cách mặc định,
    /// dòng này buộc nó dùng dấu phẩy) + tiêu đề + các dòng, xuống dòng CRLF.
    /// </summary>
    public static byte[] Build(IEnumerable<string?> header, IEnumerable<IEnumerable<string?>> rows)
    {
        var sb = new StringBuilder();
        sb.Append("sep=,\r\n");
        sb.Append(Row(header)).Append("\r\n");
        foreach (var row in rows)
        {
            sb.Append(Row(row)).Append("\r\n");
        }

        var preamble = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
        return bytes;
    }
}
