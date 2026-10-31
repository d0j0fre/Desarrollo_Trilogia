using System.Globalization;
using System.Text;

namespace Proyecto_Final.Services
{
    /// <summary>
    /// BUG-12-02: generador mínimo de PDF de texto (A4, fuentes estándar Helvetica y Courier
    /// con WinAnsiEncoding). El proyecto no tenía librería de PDF y los documentos del portal
    /// (estado de cuenta y factura) son texto y tablas, así que no se agrega una dependencia.
    /// Las tablas usan Courier (ancho fijo) para alinear columnas; las páginas se agregan solas.
    /// </summary>
    public sealed class SimplePdfDocument
    {
        private const double PageWidth = 595.28;
        private const double PageHeight = 841.89;
        private const double Margin = 42;
        private const double FooterSpace = 30;

        /// <summary>Caracteres que caben en una línea de Courier 8.5 dentro de los márgenes.</summary>
        public const int MonoColumns = 98;
        private const double MonoSize = 8.5;

        private readonly List<StringBuilder> _pages = new();
        private readonly string _footer;
        private double _y;

        public SimplePdfDocument(string footer)
        {
            _footer = footer;
            NewPage();
        }

        public void Title(string text) => Write(text, "F2", 15, 22);

        public void Heading(string text)
        {
            Space(6);
            Write(text, "F2", 11, 16);
        }

        public void Text(string text, bool bold = false) => Write(text, bold ? "F2" : "F1", 9.5, 13);

        public void Mono(string text, bool bold = false) => Write(text, bold ? "F4" : "F3", MonoSize, 11);

        public void Space(double points) => _y -= points;

        public void Rule()
        {
            EnsureRoom(8);
            _y -= 4;
            Current.Append(CultureInfo.InvariantCulture, $"0.6 w {Margin:0.##} {_y:0.##} m {PageWidth - Margin:0.##} {_y:0.##} l S\n");
            _y -= 6;
        }

        /// <summary>Arma una línea de tabla en ancho fijo: texto alineado a la izquierda o, con ancho negativo, a la derecha.</summary>
        public static string Columns(params (string Text, int Width)[] columns)
        {
            var line = new StringBuilder();
            foreach (var (text, width) in columns)
            {
                var size = Math.Abs(width);
                var value = text.Length > size ? text[..Math.Max(size - 1, 0)] + "…" : text;
                line.Append(width < 0 ? value.PadLeft(size) : value.PadRight(size)).Append(' ');
            }
            return line.ToString().TrimEnd();
        }

        public static string Money(decimal amount) =>
            "CRC " + amount.ToString("#,##0.00", CultureInfo.GetCultureInfo("en-US"));

        public byte[] ToArray()
        {
            var objects = new List<byte[]>();
            var total = _pages.Count;
            const int fixedObjects = 6; // catálogo, páginas y cuatro fuentes
            var pageIds = Enumerable.Range(0, total).Select(i => fixedObjects + 1 + i * 2).ToList();

            objects.Add(Ascii("<< /Type /Catalog /Pages 2 0 R >>"));
            objects.Add(Ascii($"<< /Type /Pages /Kids [{string.Join(' ', pageIds.Select(id => $"{id} 0 R"))}] /Count {total} >>"));
            objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));
            objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"));
            objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>"));
            objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Courier-Bold /Encoding /WinAnsiEncoding >>"));

            for (var i = 0; i < total; i++)
            {
                var content = new StringBuilder(_pages[i].ToString());
                AppendText(content, $"{_footer}  ·  Página {i + 1} de {total}", "F1", 7.5, Margin, 20);
                var stream = Encode(content.ToString());

                objects.Add(Ascii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth.ToString("0.##", CultureInfo.InvariantCulture)} {PageHeight.ToString("0.##", CultureInfo.InvariantCulture)}] " +
                    $"/Resources << /Font << /F1 3 0 R /F2 4 0 R /F3 5 0 R /F4 6 0 R >> >> /Contents {pageIds[i] + 1} 0 R >>"));
                objects.Add(Concat(Ascii($"<< /Length {stream.Length} >>\nstream\n"), stream, Ascii("\nendstream")));
            }

            using var output = new MemoryStream();
            WriteAscii(output, "%PDF-1.4\n%âãÏÓ\n");
            var offsets = new List<long>();
            for (var i = 0; i < objects.Count; i++)
            {
                offsets.Add(output.Position);
                WriteAscii(output, $"{i + 1} 0 obj\n");
                output.Write(objects[i]);
                WriteAscii(output, "\nendobj\n");
            }

            var xref = output.Position;
            WriteAscii(output, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
            foreach (var offset in offsets)
                WriteAscii(output, $"{offset:D10} 00000 n \n");
            WriteAscii(output, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
            return output.ToArray();
        }

        private StringBuilder Current => _pages[^1];

        private void NewPage()
        {
            _pages.Add(new StringBuilder());
            _y = PageHeight - Margin;
        }

        private void EnsureRoom(double height)
        {
            if (_y - height < Margin + FooterSpace)
                NewPage();
        }

        private void Write(string text, string font, double size, double leading)
        {
            EnsureRoom(leading);
            _y -= leading;
            AppendText(Current, text, font, size, Margin, _y);
        }

        private static void AppendText(StringBuilder content, string text, string font, double size, double x, double y)
        {
            content.Append(CultureInfo.InvariantCulture, $"BT /{font} {size:0.##} Tf {x:0.##} {y:0.##} Td (")
                .Append(Escape(text))
                .Append(") Tj ET\n");
        }

        private static string Escape(string text)
        {
            var normalized = text
                .Replace("₡", "CRC ", StringComparison.Ordinal)
                .Replace('\t', ' ')
                .Replace('\r', ' ')
                .Replace('\n', ' ');
            var builder = new StringBuilder(normalized.Length);
            foreach (var character in normalized)
            {
                if (character is '(' or ')' or '\\') builder.Append('\\');
                builder.Append(character);
            }
            return builder.ToString();
        }

        /// <summary>WinAnsiEncoding: Latin-1 más los signos tipográficos de 0x80–0x9F; lo demás se vuelve '?'.</summary>
        private static byte[] Encode(string text)
        {
            var bytes = new byte[text.Length];
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                bytes[i] = c switch
                {
                    < (char)0x80 => (byte)c,
                    >= (char)0xA0 and <= (char)0xFF => (byte)c,
                    '€' => 0x80, '…' => 0x85, '–' => 0x96, '—' => 0x97,
                    '‘' => 0x91, '’' => 0x92, '“' => 0x93, '”' => 0x94, '•' => 0x95,
                    _ => (byte)'?'
                };
            }
            return bytes;
        }

        private static byte[] Ascii(string text) => Encode(text);

        private static void WriteAscii(Stream stream, string text) => stream.Write(Encode(text));

        private static byte[] Concat(params byte[][] parts)
        {
            var result = new byte[parts.Sum(part => part.Length)];
            var position = 0;
            foreach (var part in parts)
            {
                Buffer.BlockCopy(part, 0, result, position, part.Length);
                position += part.Length;
            }
            return result;
        }
    }
}
