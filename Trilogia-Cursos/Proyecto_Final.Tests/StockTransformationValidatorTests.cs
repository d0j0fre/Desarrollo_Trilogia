using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Proyecto_Final.Tests
{
    // CU-182 — Pruebas de la validación de transformación de stock.
    public class StockTransformationValidatorTests
    {
        [Fact]
        public void Validate_ProductoOrigenIgualDestino_LanzaExcepcion()
        {
            var model = new StockTransformationFormViewModel
            {
                ProductoOrigenId = 5,
                ProductoDestinoId = 5,
                CantidadOrigen = 1,
                CantidadDestino = 12
            };

            var ex = Assert.Throws<InvalidOperationException>(() => StockTransformationValidator.Validate(model));
            Assert.Contains("diferentes", ex.Message);
        }

        [Theory]
        [InlineData(0, 12)]
        [InlineData(1, 0)]
        [InlineData(-1, 12)]
        public void Validate_CantidadesNoPositivas_LanzaExcepcion(int cantidadOrigen, int cantidadDestino)
        {
            var model = new StockTransformationFormViewModel
            {
                ProductoOrigenId = 1,
                ProductoDestinoId = 2,
                CantidadOrigen = cantidadOrigen,
                CantidadDestino = cantidadDestino
            };

            Assert.Throws<InvalidOperationException>(() => StockTransformationValidator.Validate(model));
        }

        [Fact]
        public void Validate_DatosValidos_NoLanzaExcepcion()
        {
            var model = new StockTransformationFormViewModel
            {
                ProductoOrigenId = 1,
                ProductoDestinoId = 2,
                CantidadOrigen = 1,
                CantidadDestino = 12
            };

            var ex = Record.Exception(() => StockTransformationValidator.Validate(model));
            Assert.Null(ex);
        }
    }
}
