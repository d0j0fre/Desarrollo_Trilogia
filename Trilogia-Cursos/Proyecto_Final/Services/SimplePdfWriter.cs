using System.Globalization;
using System.Text;

namespace Proyecto_Final.Services
{
    // BUG-16-01 — PDF mínimo (A4, texto con Helvetica/Helvetica-Bold) sin
    // dependencias externas: el proyecto no usa ninguna librería de PDF y los
    // documentos que emite son solo texto con párrafos y líneas de firma.
    // Las fuentes estándar no se incrustan; el texto va en WinAnsiEncoding
    // para que tildes, ñ, ¿ y ¡ se vean bien en cualquier visor.
    public sealed class SimplePdfWriter
    {
        public const double PageWidth = 595;   // A4 en puntos
        public const double PageHeight = 842;
        private const double Margin = 56;
        private const double Bottom = 70;      // deja espacio al pie de página

        private readonly List<StringBuilder> _pages = new();
        private readonly string _footer;
        private double _y;

        public SimplePdfWriter(string footer)
        {
            _footer = footer;
            NewPage();
        }

        public double ContentWidth => PageWidth - 2 * Margin;

        public void Paragraph(string text, double size = 10.5, bool bold = false, double spaceAfter = 6, bool center = false)
        {
            foreach (var line in Wrap(text, size, bold, ContentWidth))
            {
                EnsureSpace(size * 1.35);
                var x = center ? (PageWidth - TextWidth(line, size, bold)) / 2 : Margin;
                Text(x, _y - size, line, size, bold);
                _y -= size * 1.35;
            }
            _y -= spaceAfter;
        }

        // "Etiqueta: valor", con la etiqueta en negrita y el valor a continuación.
        public void Field(string label, string value, double size = 10.5)
        {
            var labelText = label + ": ";
            var labelWidth = TextWidth(labelText, size, true);
            var lines = Wrap(value, size, false, ContentWidth - labelWidth);
            for (var i = 0; i < lines.Count; i++)
            {
                EnsureSpace(size * 1.35);
                if (i == 0) Text(Margin, _y - size, labelText, size, true);
                Text(Margin + labelWidth, _y - size, lines[i], size, false);
                _y -= size * 1.35;
            }
            _y -= 2;
        }

        public void Space(double points) => _y -= points;

        // Dos líneas de firma lado a lado con su leyenda debajo.
        public void Signatures(string left, string leftDetail, string right, string rightDetail)
        {
            var half = ContentWidth / 2;
            var column = half - 24;
            var leftLines = Wrap(leftDetail, 9, false, column);
            var rightLines = Wrap(rightDetail, 9, false, column);
            var detailLines = Math.Max(leftLines.Count, rightLines.Count);
            EnsureSpace(76 + detailLines * 12);
            _y -= 50;
            var page = _pages[^1];
            page.Append(CultureInfo.InvariantCulture, $"0.5 w {Margin:0.##} {_y:0.##} m {Margin + column:0.##} {_y:0.##} l S\n");
            page.Append(CultureInfo.InvariantCulture, $"{Margin + half + 24:0.##} {_y:0.##} m {PageWidth - Margin:0.##} {_y:0.##} l S\n");
            Text(Margin, _y - 13, left, 9.5, true);
            Text(Margin + half + 24, _y - 13, right, 9.5, true);
            for (var i = 0; i < detailLines; i++)
            {
                if (i < leftLines.Count) Text(Margin, _y - 26 - i * 12, leftLines[i], 9, false);
                if (i < rightLines.Count) Text(Margin + half + 24, _y - 26 - i * 12, rightLines[i], 9, false);
            }
            _y -= 26 + detailLines * 12;
        }

        public byte[] ToArray()
        {
            using var stream = new MemoryStream();
            var offsets = new List<long>();
            void Write(string s) { var b = Encode(s); stream.Write(b, 0, b.Length); }
            void Obj(string body) { offsets.Add(stream.Position); Write($"{offsets.Count} 0 obj\n{body}\nendobj\n"); }

            Write("%PDF-1.4\n");
            stream.Write(new byte[] { (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' }, 0, 6);

            // 1 catálogo, 2 páginas, 3 y 4 fuentes; luego página y contenido por cada hoja.
            var kids = string.Join(" ", Enumerable.Range(0, _pages.Count).Select(i => $"{5 + i * 2} 0 R"));
            Obj("<< /Type /Catalog /Pages 2 0 R >>");
            Obj($"<< /Type /Pages /Kids [{kids}] /Count {_pages.Count} >>");
            Obj("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
            Obj("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

            for (var i = 0; i < _pages.Count; i++)
            {
                var content = new StringBuilder(_pages[i].ToString());
                var footer = $"{_footer} · Página {i + 1} de {_pages.Count}";
                AppendText(content, (PageWidth - TextWidth(footer, 8, false)) / 2, 36, footer, 8, false);
                var bytes = Encode(content.ToString());

                Obj(string.Create(CultureInfo.InvariantCulture,
                    $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {6 + i * 2} 0 R >>"));
                offsets.Add(stream.Position);
                Write($"{offsets.Count} 0 obj\n<< /Length {bytes.Length} >>\nstream\n");
                stream.Write(bytes, 0, bytes.Length);
                Write("\nendstream\nendobj\n");
            }

            var xref = stream.Position;
            var table = new StringBuilder();
            table.Append($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
            foreach (var offset in offsets) table.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
            table.Append($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
            Write(table.ToString());
            return stream.ToArray();
        }

        private void NewPage()
        {
            _pages.Add(new StringBuilder());
            _y = PageHeight - Margin;
        }

        private void EnsureSpace(double height)
        {
            if (_y - height < Bottom) NewPage();
        }

        private void Text(double x, double y, string text, double size, bool bold) => AppendText(_pages[^1], x, y, text, size, bold);

        private static void AppendText(StringBuilder page, double x, double y, string text, double size, bool bold)
        {
            page.Append(CultureInfo.InvariantCulture, $"BT /{(bold ? "F2" : "F1")} {size:0.##} Tf {x:0.##} {y:0.##} Td (");
            foreach (var c in Normalize(text))
            {
                if (c is '(' or ')' or '\\') page.Append('\\');
                page.Append(c);
            }
            page.Append(") Tj ET\n");
        }

        public static List<string> Wrap(string text, double size, bool bold, double width)
        {
            var lines = new List<string>();
            foreach (var paragraph in Normalize(text).Split('\n'))
            {
                var current = string.Empty;
                foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var candidate = current.Length == 0 ? word : current + " " + word;
                    if (current.Length > 0 && TextWidth(candidate, size, bold) > width)
                    {
                        lines.Add(current);
                        current = word;
                    }
                    else
                    {
                        current = candidate;
                    }
                }
                lines.Add(current);
            }
            return lines;
        }

        // Anchos de Helvetica (AFM, milésimas de em) para ASCII 32–126; la negrita
        // se estima un 8 % más ancha para no pasarse del margen.
        private static readonly short[] Widths =
        {
            278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
            556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
            1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
            667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
            333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
            556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584
        };

        public static double TextWidth(string text, double size, bool bold)
        {
            double total = 0;
            foreach (var c in Normalize(text))
            {
                var basic = c < 128 ? c : RemoveAccent(c);
                total += basic is >= ' ' and <= '~' ? Widths[basic - ' '] : 556;
            }
            return total * size / 1000 * (bold ? 1.08 : 1);
        }

        private static char RemoveAccent(char c)
        {
            var decomposed = c.ToString().Normalize(NormalizationForm.FormD);
            return decomposed.Length > 0 && decomposed[0] < 128 ? decomposed[0] : c;
        }

        // Deja solo caracteres representables en WinAnsiEncoding.
        private static string Normalize(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text.Length);
            foreach (var c in text.Replace("\r\n", "\n").Replace("₡", "CRC "))
            {
                if (c == '\n' || (c >= ' ' && c < 127) || (c >= ' ' && c <= 'ÿ') || WinAnsiExtra.ContainsKey(c)) sb.Append(c);
                else if (c == '\t') sb.Append(' ');
                else if (!char.IsControl(c)) sb.Append('?');
            }
            return sb.ToString();
        }

        private static readonly Dictionary<char, byte> WinAnsiExtra = new()
        {
            ['€'] = 0x80, ['…'] = 0x85, ['‘'] = 0x91, ['’'] = 0x92, ['“'] = 0x93, ['”'] = 0x94,
            ['•'] = 0x95, ['–'] = 0x96, ['—'] = 0x97
        };

        private static byte[] Encode(string text)
        {
            var bytes = new byte[text.Length];
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                bytes[i] = c <= 'ÿ' ? (byte)c : WinAnsiExtra.TryGetValue(c, out var b) ? b : (byte)'?';
            }
            return bytes;
        }
    }
}
