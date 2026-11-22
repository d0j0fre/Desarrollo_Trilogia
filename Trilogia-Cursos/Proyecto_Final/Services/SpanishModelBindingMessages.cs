using Microsoft.AspNetCore.Mvc;

namespace Proyecto_Final.Services
{
    /// <summary>
    /// POST-qa2-09: los errores de enlace de MVC (por ejemplo un número o una lista sin elegir) salían en inglés
    /// («The value '' is invalid.»). Se reemplazan por textos en español para todos los formularios.
    /// </summary>
    public static class SpanishModelBindingMessages
    {
        public static string AttemptedValueIsInvalid(string? value, string field) =>
            string.IsNullOrWhiteSpace(value)
                ? $"Debe completar o seleccionar «{field}»."
                : $"El valor «{value}» no es válido para «{field}».";

        public static string ValueIsInvalid(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? "Debe completar o seleccionar este campo."
                : $"El valor «{value}» no es válido.";

        public static void Apply(MvcOptions options)
        {
            var provider = options.ModelBindingMessageProvider;
            provider.SetAttemptedValueIsInvalidAccessor(AttemptedValueIsInvalid);
            provider.SetValueIsInvalidAccessor(ValueIsInvalid);
            provider.SetMissingBindRequiredValueAccessor(field => $"Falta el valor de «{field}».");
            provider.SetMissingKeyOrValueAccessor(() => "Debe indicar un valor.");
            provider.SetMissingRequestBodyRequiredValueAccessor(() => "Debe enviar los datos del formulario.");
            provider.SetUnknownValueIsInvalidAccessor(field => $"El valor indicado para «{field}» no es válido.");
            provider.SetValueMustBeANumberAccessor(field => $"«{field}» debe ser un número.");
            provider.SetNonPropertyValueMustBeANumberAccessor(() => "Debe ser un número.");
            provider.SetValueMustNotBeNullAccessor(value => "Debe completar este campo.");
            provider.SetNonPropertyAttemptedValueIsInvalidAccessor(value => ValueIsInvalid(value));
            provider.SetNonPropertyUnknownValueIsInvalidAccessor(() => "El valor indicado no es válido.");
        }
    }
}
