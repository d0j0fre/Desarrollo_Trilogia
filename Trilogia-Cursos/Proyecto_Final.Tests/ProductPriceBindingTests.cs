using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Proyecto_Final.ModelBinders;
using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Tests;

public sealed class ProductPriceBindingTests
{
    [Theory]
    [InlineData("1769911.50", "1769911.50")]
    [InlineData("1769911,50", "1769911.50")]
    [InlineData("0.05", "0.05")]
    [InlineData("0,05", "0.05")]
    public async Task Product_price_preserves_cents_for_both_decimal_separators(string value, string expected)
    {
        var context = await Bind(value);
        Assert.Empty(context.ModelState.Values.SelectMany(x => x.Errors));
        Assert.True(context.Result.IsModelSet);
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), Assert.IsType<decimal>(context.Result.Model));
    }
    [Theory]
    [InlineData("1,769,911.50")]
    [InlineData("1.769.911,50")]
    public async Task Ambiguous_group_separators_are_rejected(string value)
    {
        var context = await Bind(value);
        Assert.NotEmpty(context.ModelState.Values.SelectMany(x => x.Errors));
        Assert.False(context.Result.IsModelSet);
    }
    private static async Task<ModelBindingContext> Bind(string value)
    {
        var metadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(decimal));
        var action = new ActionContext(new DefaultHttpContext(), new RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var context = DefaultModelBindingContext.CreateBindingContext(action,
            new FormValueProvider(BindingSource.Form, new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { ["Precio"] = value }), CultureInfo.GetCultureInfo("es-CR")),
            metadata, null, "Precio");
        var attribute = Assert.Single(typeof(ProductFormViewModel).GetProperty(nameof(ProductFormViewModel.Precio))!.GetCustomAttributes(typeof(ModelBinderAttribute), true).Cast<ModelBinderAttribute>());
        Assert.Equal(typeof(FlexibleDecimalModelBinder), attribute.BinderType);
        await ((IModelBinder)Activator.CreateInstance(attribute.BinderType!)!).BindModelAsync(context);
        return context;
    }
}
