using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Proyecto_Final.Services;

/// <summary>
/// POST-qa3-R3-04: los atributos de validacion sin mensaje propio ([Required], [Range], [StringLength]...) mostraban el texto
/// en ingles de .NET ("The Description field is required."). Esta clase les pone un mensaje en espanol; los que ya traen
/// su propio ErrorMessage no se tocan.
/// </summary>
public sealed class SpanishValidationMetadataProvider : IValidationMetadataProvider
{
    public void CreateValidationMetadata(ValidationMetadataProviderContext context)
    {
        foreach (var item in context.ValidationMetadata.ValidatorMetadata)
            if (item is ValidationAttribute attribute) Apply(attribute);
    }

    public static void Apply(ValidationAttribute attribute)
    {
        if (!string.IsNullOrEmpty(attribute.ErrorMessage) || !string.IsNullOrEmpty(attribute.ErrorMessageResourceName)) return;
        var message = MessageFor(attribute);
        if (message is not null) attribute.ErrorMessage = message;
    }

    public static string? MessageFor(ValidationAttribute attribute) => attribute switch
    {
        RequiredAttribute => "El campo {0} es obligatorio.",
        RangeAttribute => "El campo {0} debe estar entre {1} y {2}.",
        StringLengthAttribute { MinimumLength: > 0 } => "El campo {0} debe tener entre {2} y {1} caracteres.",
        StringLengthAttribute => "El campo {0} no puede superar {1} caracteres.",
        MaxLengthAttribute => "El campo {0} no puede superar {1} caracteres.",
        MinLengthAttribute => "El campo {0} debe tener al menos {1} caracteres.",
        EmailAddressAttribute => "El campo {0} no es un correo electrónico válido.",
        RegularExpressionAttribute => "El campo {0} no tiene un formato válido.",
        CompareAttribute => "El campo {0} no coincide con {1}.",
        PhoneAttribute => "El campo {0} no es un teléfono válido.",
        UrlAttribute => "El campo {0} no es una dirección web válida.",
        _ => null
    };
}
