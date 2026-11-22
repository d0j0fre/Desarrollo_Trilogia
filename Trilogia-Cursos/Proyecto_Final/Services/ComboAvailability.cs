using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Services;

/// <summary>
/// M-05: cuando la tienda no puede mostrar o agregar un combo, dice cuál producto del combo es el que falta
/// (sin stock, sin stock suficiente o desactivado) en vez del mensaje genérico «no está disponible».
/// </summary>
public static class ComboAvailability
{
    public const string GenericMessage = "El combo solicitado no está disponible.";

    public static string Explain(ComboDetailViewModel? combo)
    {
        if (combo is null || !combo.Activo)
        {
            return GenericMessage;
        }

        var inactivos = combo.Componentes.Where(c => !c.ProductoActivo).Select(c => c.ProductoNombre).ToList();
        var sinStock = combo.Componentes
            .Where(c => c.ProductoActivo && c.Cantidad > 0 && c.StockDisponible < c.Cantidad)
            .Select(c => c.StockDisponible <= 0
                ? $"«{c.ProductoNombre}» (sin stock)"
                : $"«{c.ProductoNombre}» (se necesitan {c.Cantidad} y hay {c.StockDisponible})")
            .ToList();

        var partes = new List<string>();
        if (sinStock.Count > 0)
        {
            partes.Add("no alcanza el stock de " + string.Join(", ", sinStock));
        }
        if (inactivos.Count > 0)
        {
            partes.Add((inactivos.Count == 1 ? "el producto " : "los productos ") + string.Join(", ", inactivos.Select(n => $"«{n}»")) + " ya no está(n) activo(s)");
        }

        return partes.Count == 0
            ? GenericMessage
            : $"El combo «{combo.Nombre}» no está disponible: {string.Join("; ", partes)}.";
    }
}
