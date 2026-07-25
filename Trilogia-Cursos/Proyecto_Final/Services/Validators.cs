using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Services
{
    // CU-182 — Validación de la transformación, separada del acceso a datos para poder probarla sin base de datos.
    public static class StockTransformationValidator
    {
        public static void Validate(StockTransformationFormViewModel model)
        {
            if (model.ProductoOrigenId == model.ProductoDestinoId)
                throw new InvalidOperationException("El producto de origen y destino deben ser diferentes.");

            if (model.CantidadOrigen <= 0 || model.CantidadDestino <= 0)
                throw new InvalidOperationException("Las cantidades deben ser mayores que cero.");
        }
    }

    // CU-181 — Validación de que el combo tenga al menos un producto seleccionado.
    public static class ComboValidator
    {
        public static List<ComboProductSelectionViewModel> GetSelectedProducts(ComboFormViewModel model)
        {
            var seleccionados = model.Productos.Where(p => p.Seleccionado && p.Cantidad > 0).ToList();
            if (seleccionados.Count == 0)
                throw new InvalidOperationException("Debe seleccionar al menos un producto para armar el combo.");
            return seleccionados;
        }
    }
}
