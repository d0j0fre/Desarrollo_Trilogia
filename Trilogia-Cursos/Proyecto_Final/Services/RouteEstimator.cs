using System.Globalization;
using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Services;

/// <summary>Llegada estimada a una parada, contada desde la salida de la bodega.</summary>
public sealed record RouteStopEstimate(int RutaPedidoId, double DistanciaKm, int MinutosAcumulados);

/// <summary>Tiempo y distancia estimados de lo que falta recorrer de una ruta (BUG-25-02).</summary>
public sealed record RouteEstimate(
    int Paradas,
    int ParadasSinUbicacion,
    double DistanciaKm,
    int MinutosTotales,
    IReadOnlyDictionary<int, RouteStopEstimate> PorParada)
{
    public bool TieneEstimacion => Paradas > 0;

    /// <summary>«1 h 25 min», «45 min» o «—» si no hay paradas con ubicación.</summary>
    public string TextoTotal => RouteEstimator.FormatMinutes(MinutosTotales, TieneEstimacion);

    public string TextoDistancia => DistanciaKm.ToString("0.0", CultureInfo.GetCultureInfo("es-CR")) + " km";
}

/// <summary>
/// Estimación del tiempo de una ruta a partir de las coordenadas de sus paradas, en el orden
/// en que están secuenciadas: se sale de la bodega, se recorre cada tramo y en cada parada se
/// suma un tiempo de atención. No consulta ningún servicio de mapas: es una referencia para
/// planificar, no una promesa al cliente.
/// </summary>
public static class RouteEstimator
{
    /// <summary>Punto de partida: el mismo que usa la secuenciación (San José).</summary>
    public const double OrigenLatitud = 9.9333;
    public const double OrigenLongitud = -84.0833;

    /// <summary>Velocidad media en zona urbana y carretera mixta.</summary>
    public const double VelocidadPromedioKmH = 30.0;

    /// <summary>La carretera es más larga que la línea recta entre dos puntos.</summary>
    public const double FactorRecorrido = 1.3;

    /// <summary>Minutos en cada parada para descargar y entregar.</summary>
    public const int MinutosPorParada = 10;

    private const double RadioTierraKm = 6371.0;

    /// <summary>
    /// Estima lo que falta por recorrer: las paradas Pendiente o En ruta, en orden de secuencia.
    /// Las entregadas o fallidas ya no cuentan. Las que no tienen coordenada se omiten y se cuentan aparte.
    /// </summary>
    public static RouteEstimate Estimate(IEnumerable<RouteOrderItemViewModel> paradas)
    {
        var porVisitar = paradas
            .Where(p => p.EstadoEntrega is "Pendiente" or "EnRuta")
            .OrderBy(p => p.Secuencia)
            .ThenBy(p => p.RutaPedidoId)
            .ToList();

        var detalle = new Dictionary<int, RouteStopEstimate>();
        var latitud = OrigenLatitud;
        var longitud = OrigenLongitud;
        double kmTotal = 0;
        double minutos = 0;
        var sinUbicacion = 0;

        foreach (var parada in porVisitar)
        {
            if (parada.Latitud is not { } lat || parada.Longitud is not { } lng)
            {
                sinUbicacion++;
                continue;
            }

            var km = DistanceKm(latitud, longitud, (double)lat, (double)lng) * FactorRecorrido;
            kmTotal += km;
            minutos += km / VelocidadPromedioKmH * 60.0 + MinutosPorParada;
            detalle[parada.RutaPedidoId] = new RouteStopEstimate(parada.RutaPedidoId, km, (int)Math.Round(minutos));
            latitud = (double)lat;
            longitud = (double)lng;
        }

        return new RouteEstimate(detalle.Count, sinUbicacion, kmTotal, (int)Math.Round(minutos), detalle);
    }

    /// <summary>Distancia en línea recta (haversine) entre dos puntos, en kilómetros.</summary>
    public static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        static double Rad(double grados) => grados * Math.PI / 180.0;
        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return RadioTierraKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    public static string FormatMinutes(int minutos, bool hayEstimacion = true)
    {
        if (!hayEstimacion) return "—";
        if (minutos < 60) return $"{minutos} min";
        var horas = minutos / 60;
        var resto = minutos % 60;
        return resto == 0 ? $"{horas} h" : $"{horas} h {resto} min";
    }
}
