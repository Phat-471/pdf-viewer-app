#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PdfViewerApp;

// Read the exact bytes embedded in the PDF. Windows can return cached names
// from another face with the same family; those names must not select a font.
internal sealed class PdfTrueTypeFont
{
    private readonly byte[] _data;
    private readonly Dictionary<string, (int Offset, int Length)> _tables = new();
    internal string Name { get; }
    internal string PostScriptName { get; }

    internal PdfTrueTypeFont(byte[] data)
    {
        _data = data;
        if (U32(0) != 0x00010000)
            throw new InvalidOperationException("Cần font TrueType (.ttf) dạng tĩnh. Chưa hỗ trợ OTF hoặc TTC.");
        int count = U16(4);
        Check(12, checked(count * 16));
        for (int i = 0; i < count; i++)
        {
            int record = 12 + i * 16;
            string tag = Encoding.ASCII.GetString(data, record, 4);
            int offset = checked((int)U32(record + 8)), length = checked((int)U32(record + 12));
            Check(offset, length);
            if (!_tables.TryAdd(tag, (offset, length))) throw Invalid();
        }
        if (_tables.ContainsKey("fvar")) throw new InvalidOperationException("Hãy chọn bản TTF dạng tĩnh của font này.");
        var names = ReadNames();
        if (!names.TryGetValue(1, out string? family) || !names.TryGetValue(2, out string? face)) throw Invalid();
        Name = family + (face is "Regular" or "Normal" or "Roman" ? "" : "-" + face);
        PostScriptName = names.GetValueOrDefault(6, Name);
    }

    internal void CheckEmbeddingRights()
    {
        var table = Table("OS/2", 10);
        int rights = U16(table.Offset + 8);
        // fsType=0 is installable. Editable embedding is bit 3; preview-only
        // and restricted embedding do not authorize editing.
        if ((rights & 2) != 0 || ((rights & 0xF) != 0 && (rights & 8) == 0))
            throw new InvalidOperationException("Giấy phép font không cho phép nhúng để chỉnh sửa. Hãy chọn font có quyền nhúng phù hợp.");
        if ((rights & 0x200) != 0)
            throw new InvalidOperationException("Font chỉ cho phép nhúng ảnh, không dùng được để sửa chữ PDF.");
    }

    internal string ReadCharacters() => new(ReadGlyphMap().Keys.ToArray());

    internal SortedDictionary<char, ushort> ReadGlyphMap()
    {
        var cmap = Table("cmap", 4);
        int count = U16(cmap.Offset + 2);
        InTable(cmap, cmap.Offset + 4, checked(count * 8));
        var candidates = new List<(int Offset, int Format, int Rank)>();
        for (int i = 0; i < count; i++)
        {
            int record = cmap.Offset + 4 + i * 8;
            int platform = U16(record), encoding = U16(record + 2);
            if (platform != 0 && !(platform == 3 && encoding is 1 or 10)) continue;
            int offset = checked(cmap.Offset + (int)U32(record + 4));
            InTable(cmap, offset, 2);
            int format = U16(offset);
            if (format is 4 or 12 or 13)
                candidates.Add((offset, format, (format == 4 ? 0 : 10) + (platform == 3 ? 1 : 0)));
        }
        if (candidates.Count == 0) throw new InvalidOperationException("Font không có bảng mã Unicode được hỗ trợ. Hãy chọn TTF khác.");
        var chosen = candidates.OrderByDescending(c => c.Rank).First();
        int start = chosen.Offset;
        int length = chosen.Format == 4 ? U16(start + 2) : checked((int)U32(start + 4));
        InTable(cmap, start, length);
        var sub = (Offset: start, Length: length);
        var supported = new SortedDictionary<char, ushort>();
        int glyphCount = U16(Table("maxp", 6).Offset + 4);
        void Add(int code, uint glyph)
        {
            if (glyph >= glyphCount) throw Invalid();
            if (glyph != 0 && code < 0xFFFF && code is not (>= 0xD800 and <= 0xDFFF)) supported.Add((char)code, (ushort)glyph);
        }
        if (chosen.Format == 4)
        {
            InTable(sub, start, 16);
            int segments = U16(start + 6) / 2;
            if (segments == 0 || U16(start + 6) % 2 != 0) throw Invalid();
            InTable(sub, start + 14, checked(segments * 8 + 2));
            int ends = start + 14, starts = ends + segments * 2 + 2;
            int deltas = starts + segments * 2, ranges = deltas + segments * 2;
            int previousEnd = -1;
            for (int i = 0; i < segments; i++)
            {
                int first = U16(starts + i * 2), last = U16(ends + i * 2), delta = U16(deltas + i * 2);
                int range = U16(ranges + i * 2);
                if (first > last || first <= previousEnd) throw Invalid();
                previousEnd = last;
                for (int code = first; code <= last && code < 0xFFFF; code++)
                {
                    int glyph;
                    if (range == 0) glyph = (code + delta) & 0xFFFF;
                    else
                    {
                        int address = checked(ranges + i * 2 + range + (code - first) * 2);
                        InTable(sub, address, 2);
                        glyph = U16(address);
                        if (glyph != 0) glyph = (glyph + delta) & 0xFFFF;
                    }
                    Add(code, (uint)glyph);
                }
            }
        }
        else
        {
            InTable(sub, start, 16);
            int groups = checked((int)U32(start + 12));
            InTable(sub, start + 16, checked(groups * 12));
            long previousEnd = -1;
            for (int i = 0; i < groups; i++)
            {
                int record = start + 16 + i * 12;
                uint first = U32(record), last = U32(record + 4), glyph = U32(record + 8);
                if (first > last || first <= previousEnd || last > 0x10FFFF) throw Invalid();
                previousEnd = last;
                for (uint code = first; code <= last && code < 0xFFFF; code++)
                    Add((int)code, chosen.Format == 13 ? glyph : checked(glyph + code - first));
            }
        }
        if (supported.Count == 0) throw new InvalidOperationException("Không đọc được ký tự Unicode trong font. Hãy chọn TTF khác.");
        return supported;
    }

    private Dictionary<int, string> ReadNames()
    {
        var table = Table("name", 6);
        int count = U16(table.Offset + 2), storage = U16(table.Offset + 4);
        InTable(table, table.Offset + 6, checked(count * 12));
        var names = new Dictionary<int, (string Value, int Rank)>();
        for (int i = 0; i < count; i++)
        {
            int record = table.Offset + 6 + i * 12;
            int platform = U16(record), encoding = U16(record + 2), language = U16(record + 4), id = U16(record + 6);
            if (id is not (1 or 2 or 6) || (platform != 0 && !(platform == 3 && encoding is 0 or 1 or 10))) continue;
            int length = U16(record + 8), offset = checked(table.Offset + storage + U16(record + 10));
            InTable(table, offset, length);
            if (length % 2 != 0) throw Invalid();
            string value = Encoding.BigEndianUnicode.GetString(_data, offset, length).Trim('\0', ' ').Normalize(NormalizationForm.FormC);
            int rank = (platform == 3 ? 1 : 0) + (language == 0x0409 ? 10 : 0);
            if (value.Length > 0 && (!names.TryGetValue(id, out var current) || rank > current.Rank)) names[id] = (value, rank);
        }
        return names.ToDictionary(p => p.Key, p => p.Value.Value);
    }

    private (int Offset, int Length) Table(string tag, int minimum)
    {
        if (!_tables.TryGetValue(tag, out var table) || table.Length < minimum) throw Invalid();
        return table;
    }
    private void InTable((int Offset, int Length) table, int offset, int length)
    {
        if (offset < table.Offset || length < 0 || (long)offset + length > (long)table.Offset + table.Length) throw Invalid();
    }
    private void Check(int offset, int length) { if (offset < 0 || length < 0 || (long)offset + length > _data.Length) throw Invalid(); }
    private int U16(int offset) { Check(offset, 2); return (_data[offset] << 8) | _data[offset + 1]; }
    private uint U32(int offset) { Check(offset, 4); return ((uint)U16(offset) << 16) | (uint)U16(offset + 2); }
    private static InvalidOperationException Invalid() => new("File font thiếu bảng dữ liệu hoặc bị hỏng. Hãy chọn TTF khác.");
}
