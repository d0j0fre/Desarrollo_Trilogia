namespace Proyecto_Final.Services;

/// <summary>
/// Imagen de un producto en la Tienda, el carrito e Inventario (G3-11). Si el producto
/// no tiene imagen, o la guardada no existe (los datos demo apuntan a ~/img/productos/,
/// que no está en el sitio), se usa la imagen de su categoría o un ícono neutro, nunca
/// la foto de un whisky.
/// </summary>
public static class ProductImageFallback
{
    public const string Neutral = "~/img/producto-sin-imagen.svg";

    /// <summary>
    /// sp_Store_GetProducts, sp_Store_GetProductById y sp_Ventas_CrossSellSuggestions devuelven
    /// esta foto cuando el producto no tiene imagen: se trata como «sin imagen».
    /// </summary>
    public const string StoreProcedurePlaceholder = "~/img/whisky-premium.webp";

    public static string ForCategory(string? categoria) => (categoria ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "cerveza" or "cervezas" => "~/img/CAT-Cerveza.png",
        "vino" or "vinos" => "~/img/CAT-Vino.png",
        "ron" => "~/img/CAT- Ron.png",
        "vodka" => "~/img/CAT-Vodka.webp",
        "tequila" => "~/img/CAT-Tequila.webp",
        "whisky" => "~/img/CAT-Whisky.png",
        "ginebra" or "gin" => "~/img/CAT-Gin.png",
        "refrescos" or "energéticas" or "mixers" => "~/img/CAT-Mixers.png",
        _ => Neutral,
    };

    public static string Resolve(string? imagenUrl, string? categoria) =>
        string.IsNullOrWhiteSpace(imagenUrl)
        || string.Equals(imagenUrl.Trim(), StoreProcedurePlaceholder, StringComparison.OrdinalIgnoreCase)
            ? ForCategory(categoria)
            : imagenUrl;
}
