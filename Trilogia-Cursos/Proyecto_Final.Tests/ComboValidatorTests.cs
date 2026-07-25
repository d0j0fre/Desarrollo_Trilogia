using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Proyecto_Final.Tests
{
    // CU-181 — Pruebas de la validación de selección de productos del combo.
    public class ComboValidatorTests
    {
        [Fact]
        public void GetSelectedProducts_SinProductosSeleccionados_LanzaExcepcion()
        {
            var model = new ComboFormViewModel
            {
                Nombre = "Combo prueba",
                Precio = 5000,
                Productos = new List<ComboProductSelectionViewModel>
                {
                    new() { ProductoId = 1, Nombre = "Producto A", Seleccionado = false, Cantidad = 1 },
                    new() { ProductoId = 2, Nombre = "Producto B", Seleccionado = false, Cantidad = 1 }
                }
            };

            Assert.Throws<InvalidOperationException>(() => ComboValidator.GetSelectedProducts(model));
        }

        [Fact]
        public void GetSelectedProducts_ProductoSeleccionadoConCantidadCero_SeExcluye()
        {
            var model = new ComboFormViewModel
            {
                Nombre = "Combo prueba",
                Precio = 5000,
                Productos = new List<ComboProductSelectionViewModel>
                {
                    new() { ProductoId = 1, Nombre = "Producto A", Seleccionado = true, Cantidad = 0 }
                }
            };

            Assert.Throws<InvalidOperationException>(() => ComboValidator.GetSelectedProducts(model));
        }

        [Fact]
        public void GetSelectedProducts_ConProductosValidos_DevuelveSoloLosSeleccionados()
        {
            var model = new ComboFormViewModel
            {
                Nombre = "Combo prueba",
                Precio = 5000,
                Productos = new List<ComboProductSelectionViewModel>
                {
                    new() { ProductoId = 1, Nombre = "Producto A", Seleccionado = true, Cantidad = 2 },
                    new() { ProductoId = 2, Nombre = "Producto B", Seleccionado = false, Cantidad = 1 },
                    new() { ProductoId = 3, Nombre = "Producto C", Seleccionado = true, Cantidad = 1 }
                }
            };

            var resultado = ComboValidator.GetSelectedProducts(model);

            Assert.Equal(2, resultado.Count);
            Assert.Contains(resultado, p => p.ProductoId == 1);
            Assert.Contains(resultado, p => p.ProductoId == 3);
        }
    }
}
