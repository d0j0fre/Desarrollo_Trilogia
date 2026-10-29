namespace Proyecto_Final.Services;

public sealed class DocumentSourceUnavailableException : Exception
{
    public DocumentSourceUnavailableException() : base("El contrato documental no está habilitado.") { }
}
