using System.Text.Json;

namespace Proyecto_Final.Services;

/// <summary>Unsubmitted UUID only. Durable attempts and their outcomes are recovered from SQL.</summary>
public static class InvoiceNotificationAttempt
{
    private const string Prefix = "InvoiceNotification.Unsubmitted.";
    private sealed record Attempt(Guid Login, Guid Operation);

    public static Guid ForInvoice(ISession session, OriginalLoginContext login, int invoice)
    {
        if (invoice <= 0 || !login.HasCanonicalLifetime) throw new ArgumentException("Contexto no válido.");
        string key = Prefix + invoice;
        if (session.GetString(key) is { } json)
        {
            try
            {
                var current = JsonSerializer.Deserialize<Attempt>(json);
                if (current?.Login == login.LoginId && current.Operation != Guid.Empty) return current.Operation;
            }
            catch (JsonException) { }
            session.Remove(key);
        }
        if (session.Keys.Count(x => x.StartsWith(Prefix, StringComparison.Ordinal)) >= 20)
            throw new InvalidOperationException("Hay demasiadas facturas pendientes en esta sesión.");
        var operation = Guid.NewGuid();
        session.SetString(key, JsonSerializer.Serialize(new Attempt(login.LoginId, operation)));
        return operation;
    }

    public static void Confirmed(ISession session, int invoice) => session.Remove(Prefix + invoice);

    public static Guid? Peek(ISession session, OriginalLoginContext login, int invoice)
    {
        if (session.GetString(Prefix + invoice) is not { } json) return null;
        try
        {
            var attempt = JsonSerializer.Deserialize<Attempt>(json);
            return attempt?.Login == login.LoginId && attempt.Operation != Guid.Empty ? attempt.Operation : null;
        }
        catch (JsonException) { return null; }
    }

    public static bool Matches(ISession session, OriginalLoginContext login, int invoice, Guid operation)
    {
        if (operation == Guid.Empty || session.GetString(Prefix + invoice) is not { } json) return false;
        try
        {
            var current = JsonSerializer.Deserialize<Attempt>(json);
            return current?.Login == login.LoginId && current.Operation == operation;
        }
        catch (JsonException) { return false; }
    }
}
