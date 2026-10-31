// Libro .xlsx mínimo (una hoja, solo texto) generado en el navegador, sin librerías.
// Lo usa el Asistente (CU-263-03) para descargar la conversación.
// Las celdas son cadenas en línea (inlineStr): Excel nunca las evalúa como fórmulas.
(function (global) {
    'use strict';

    // Estilos de celda: 0 normal, 1 negrita (encabezados), 2 texto ajustado arriba.
    const STYLE = { normal: 0, bold: 1, wrap: 2 };

    const CRC_TABLE = (function () {
        const table = new Uint32Array(256);
        for (let n = 0; n < 256; n++) {
            let c = n;
            for (let k = 0; k < 8; k++) {
                c = (c & 1) ? (0xEDB88320 ^ (c >>> 1)) : (c >>> 1);
            }
            table[n] = c >>> 0;
        }
        return table;
    })();

    function crc32(bytes) {
        let crc = 0xFFFFFFFF;
        for (let i = 0; i < bytes.length; i++) {
            crc = CRC_TABLE[(crc ^ bytes[i]) & 0xFF] ^ (crc >>> 8);
        }
        return (crc ^ 0xFFFFFFFF) >>> 0;
    }

    // Zip con método "store" (sin compresión): suficiente para un libro pequeño.
    function buildZip(files) {
        const encoder = new TextEncoder();
        const now = new Date();
        const dosTime = (now.getHours() << 11) | (now.getMinutes() << 5) | Math.floor(now.getSeconds() / 2);
        const dosDate = ((now.getFullYear() - 1980) << 9) | ((now.getMonth() + 1) << 5) | now.getDate();
        const chunks = [];
        const central = [];
        let offset = 0;

        files.forEach(function (file) {
            const name = encoder.encode(file.name);
            const data = encoder.encode(file.content);
            const crc = crc32(data);

            const local = new DataView(new ArrayBuffer(30));
            local.setUint32(0, 0x04034B50, true);
            local.setUint16(4, 20, true);
            local.setUint16(6, 0x0800, true);      // nombres en UTF-8
            local.setUint16(8, 0, true);           // store
            local.setUint16(10, dosTime, true);
            local.setUint16(12, dosDate, true);
            local.setUint32(14, crc, true);
            local.setUint32(18, data.length, true);
            local.setUint32(22, data.length, true);
            local.setUint16(26, name.length, true);
            local.setUint16(28, 0, true);
            chunks.push(new Uint8Array(local.buffer), name, data);

            const entry = new DataView(new ArrayBuffer(46));
            entry.setUint32(0, 0x02014B50, true);
            entry.setUint16(4, 20, true);
            entry.setUint16(6, 20, true);
            entry.setUint16(8, 0x0800, true);
            entry.setUint16(10, 0, true);
            entry.setUint16(12, dosTime, true);
            entry.setUint16(14, dosDate, true);
            entry.setUint32(16, crc, true);
            entry.setUint32(20, data.length, true);
            entry.setUint32(24, data.length, true);
            entry.setUint16(28, name.length, true);
            entry.setUint32(42, offset, true);
            central.push(new Uint8Array(entry.buffer), name);

            offset += 30 + name.length + data.length;
        });

        const centralSize = central.reduce(function (sum, part) { return sum + part.length; }, 0);
        const end = new DataView(new ArrayBuffer(22));
        end.setUint32(0, 0x06054B50, true);
        end.setUint16(8, files.length, true);
        end.setUint16(10, files.length, true);
        end.setUint32(12, centralSize, true);
        end.setUint32(16, offset, true);

        return new Blob(chunks.concat(central, [new Uint8Array(end.buffer)]), {
            type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
        });
    }

    // Escapa para XML y quita los caracteres de control que XML 1.0 no admite.
    function xml(value) {
        return String(value ?? '')
            .replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F￾￿]/g, '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function columnName(index) {
        let name = '';
        for (let n = index + 1; n > 0; n = Math.floor((n - 1) / 26)) {
            name = String.fromCharCode(65 + (n - 1) % 26) + name;
        }
        return name;
    }

    function sheetXml(rows, widths) {
        const MAX_CELL = 32000;   // Excel admite hasta 32 767 caracteres por celda
        let out = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
            '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">';
        if (widths && widths.length) {
            out += '<cols>' + widths.map(function (w, i) {
                return '<col min="' + (i + 1) + '" max="' + (i + 1) + '" width="' + w + '" customWidth="1"/>';
            }).join('') + '</cols>';
        }
        out += '<sheetData>';
        rows.forEach(function (row, r) {
            out += '<row r="' + (r + 1) + '">';
            row.forEach(function (cell, c) {
                const text = String(cell && cell.text != null ? cell.text : '').slice(0, MAX_CELL);
                if (!text) {
                    return;
                }
                const style = cell.style ? ' s="' + cell.style + '"' : '';
                out += '<c r="' + columnName(c) + (r + 1) + '" t="inlineStr"' + style + '>' +
                    '<is><t xml:space="preserve">' + xml(text) + '</t></is></c>';
            });
            out += '</row>';
        });
        return out + '</sheetData></worksheet>';
    }

    function buildWorkbook(sheetName, rows, widths) {
        const safeSheet = (String(sheetName || 'Hoja1').replace(/[:\\/?*\[\]]/g, '').trim() || 'Hoja1').slice(0, 31);
        return buildZip([
            {
                name: '[Content_Types].xml',
                content: '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
                    '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">' +
                    '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>' +
                    '<Default Extension="xml" ContentType="application/xml"/>' +
                    '<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>' +
                    '<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>' +
                    '<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>' +
                    '</Types>'
            },
            {
                name: '_rels/.rels',
                content: '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
                    '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' +
                    '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>' +
                    '</Relationships>'
            },
            {
                name: 'xl/workbook.xml',
                content: '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
                    '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">' +
                    '<sheets><sheet name="' + xml(safeSheet) + '" sheetId="1" r:id="rId1"/></sheets></workbook>'
            },
            {
                name: 'xl/_rels/workbook.xml.rels',
                content: '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
                    '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' +
                    '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>' +
                    '<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>' +
                    '</Relationships>'
            },
            {
                name: 'xl/styles.xml',
                content: '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
                    '<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">' +
                    '<fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts>' +
                    '<fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>' +
                    '<borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>' +
                    '<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>' +
                    '<cellXfs count="3">' +
                    '<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>' +
                    '<xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/>' +
                    '<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" applyAlignment="1"><alignment vertical="top" wrapText="1"/></xf>' +
                    '</cellXfs></styleSheet>'
            },
            { name: 'xl/worksheets/sheet1.xml', content: sheetXml(rows, widths) }
        ]);
    }

    function download(fileName, sheetName, rows, widths) {
        const blob = buildWorkbook(sheetName, rows, widths);
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
    }

    global.XlsxExport = { STYLE: STYLE, buildWorkbook: buildWorkbook, download: download };
})(window);
