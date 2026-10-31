using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Services
{
    // BUG-16-01 · CU-162-01 — Contrato de comodato que se emite al asignar un equipo.
    public static class ComodatoContractPdf
    {
        public const string Comodante = "Distribuidora JJ / Licorera La Bodega";

        public static byte[] Build(ComodatoContractViewModel c)
        {
            var pdf = new SimplePdfDocument($"{Comodante} · Contrato de comodato {c.Numero}");
            static string OrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

            pdf.Title("Contrato de comodato de equipo");
            pdf.Text($"{Comodante} · N.º {c.Numero} · Emitido el {c.FechaRegistro:dd/MM/yyyy}");
            pdf.Rule();

            pdf.Heading("Partes");
            pdf.Field("Comodante", $"{Comodante}, quien presta el equipo.");
            pdf.Field("Comodatario", $"{c.ClienteNombre.Trim()}, identificación {OrDash(c.ClienteIdentificacion)}, quien lo recibe.");

            pdf.Heading("Equipo entregado");
            pdf.Field("Activo", $"{c.CodigoActivo} — {c.ActivoNombre} ({c.ActivoTipo})");
            pdf.Field("Ubicación del equipo", OrDash(c.Ubicacion));
            pdf.Field("Fecha de entrega", c.FechaAsignacion.ToString("dd/MM/yyyy"));
            pdf.Field("Condición de entrega", OrDash(c.CondicionEntrega));
            pdf.Field("Observaciones", OrDash(c.Observaciones));

            pdf.Heading("Cláusulas");
            string[] clausulas =
            {
                "1. Objeto. El comodante entrega al comodatario, en préstamo de uso gratuito, el equipo descrito, que sigue siendo propiedad del comodante. El equipo se destina a la conservación y exhibición de productos adquiridos al comodante.",
                "2. Ubicación. El equipo permanecerá en la ubicación indicada. No podrá trasladarse, prestarse, alquilarse ni cederse a terceros sin autorización escrita del comodante.",
                "3. Cuidado. El comodatario usará el equipo con diligencia, lo mantendrá limpio y conectado según las indicaciones del comodante e informará de inmediato cualquier avería. No podrá repararlo ni modificarlo por su cuenta.",
                "4. Daños. Las averías causadas por mal uso, golpes o negligencia se cobrarán al comodatario según la valoración del taller del comodante. El desgaste normal por el uso no se cobra.",
                "5. Devolución. El comodante podrá solicitar la devolución del equipo en cualquier momento, en especial cuando el comodatario no registre compras durante 30 días o mantenga saldos vencidos. El equipo se devolverá en la condición en que se entregó, salvo el desgaste normal.",
                "6. Revisión. El comodatario permitirá que el personal del comodante revise el equipo en horario comercial.",
                "7. Aceptación. Las partes firman en señal de conformidad con la entrega del equipo y con las condiciones de este contrato."
            };
            foreach (var clausula in clausulas)
            {
                pdf.Paragraph(clausula);
                pdf.Space(4);
            }

            pdf.Signatures(
                "Por el comodante", $"{Comodante}{(string.IsNullOrWhiteSpace(c.RegistradoPorNombre) ? "" : $" · Registró: {c.RegistradoPorNombre.Trim()}")}",
                "Comodatario", $"{c.ClienteNombre.Trim()} · Identificación {OrDash(c.ClienteIdentificacion)}");

            return pdf.ToArray();
        }

        public static string FileName(ComodatoContractViewModel c) => $"contrato-comodato-{c.Numero}.pdf";
    }
}
