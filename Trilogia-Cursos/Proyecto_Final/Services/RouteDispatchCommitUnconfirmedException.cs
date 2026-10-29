using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Services;

// The writer returned a result but the COMMIT acknowledgement was lost. The
// caller may recover this exact dispatch through GetOwned; it must not send mail.
public sealed class RouteDispatchCommitUnconfirmedException : Exception
{
    public RouteDispatchResultViewModel Result { get; }

    public RouteDispatchCommitUnconfirmedException(RouteDispatchResultViewModel result, Exception inner)
        : base("No se confirmó el commit del despacho.", inner)
    {
        if (result.RutaId <= 0 || result.DispatchId == Guid.Empty)
            throw new ArgumentException("Resultado de despacho no acreditado.", nameof(result));
        Result = result;
    }
}
