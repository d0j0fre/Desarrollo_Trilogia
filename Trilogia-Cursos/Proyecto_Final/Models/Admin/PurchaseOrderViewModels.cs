using System.ComponentModel.DataAnnotations;

namespace Proyecto_Final.Models.Admin
{
    // CU-102 — Órdenes de compra a proveedores.
    public class PurchaseOrderListItemViewModel
    {
        public int OrdenCompraId { get; set; }
        public int ProveedorId { get; set; }
        public string ProveedorNombre { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string? Notas { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaRecepcion { get; set; }
        public decimal MontoTotal { get; set; }
        public int TotalOrdenado { get; set; }
        public int TotalRecibido { get; set; }
    }

    // Fila de producto seleccionable al armar una orden nueva (mismo patrón que ComboProductSelectionViewModel).
    public class PurchaseOrderLineSelectionViewModel
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int StockActual { get; set; }
        public bool Seleccionado { get; set; }
        public int Cantidad { get; set; } = 1;
        public decimal PrecioUnitario { get; set; } = 1.00m;

        // Solo de referencia: precio al que se vende hoy en tienda (no se guarda en la orden).
        public decimal PrecioTienda { get; set; }

        // Solo de referencia: promedio de ventas mensual de los últimos meses(para CU-103).
        public decimal PromedioVentaMensual { get; set; }

        // Solo de referencia: último precio pagado a cualquier proveedor por este producto (para CU-104).
        public decimal? UltimoPrecioPagado { get; set; }
    }

    public class PurchaseOrderFormViewModel
    {
        [Required(ErrorMessage = "Debe seleccionar un proveedor.")]
        public int ProveedorId { get; set; }

        [StringLength(300)]
        public string? Notas { get; set; }

        public List<SupplierListItemViewModel> Proveedores { get; set; } = new();
        public List<PurchaseOrderLineSelectionViewModel> Productos { get; set; } = new();
    }

    public class PurchaseOrderDetailLineViewModel
    {
        public int DetalleOrdenCompraId { get; set; }
        public int ProductoId { get; set; }
        public string ProductoNombre { get; set; } = string.Empty;
        public int CantidadOrdenada { get; set; }
        public int CantidadRecibida { get; set; }
        public decimal PrecioUnitario { get; set; }
        public int Pendiente => CantidadOrdenada - CantidadRecibida;
    }

    public class PurchaseOrderDetailViewModel
    {
        public int OrdenCompraId { get; set; }
        public int ProveedorId { get; set; }
        public string ProveedorNombre { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string? Notas { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaRecepcion { get; set; }
        public List<PurchaseOrderDetailLineViewModel> Lineas { get; set; } = new();
    }
}
