namespace Proyecto_Final.Services.ChatV2.Broker;

// Authenticates every operation in canonical SQL. The transport must first accredit the actual Windows client.
internal sealed class ChatV2BrokerDispatcher(IChatV2IntentPersistence persistence, TimeProvider clock)
{
    internal async Task<BrokerResponse> DispatchAsync(BrokerRequest request, CancellationToken ct)
    {
        if (request.Version != 1 || request.Correlation == Guid.Empty || !Enum.IsDefined(request.Operation))
            throw new ChatV2Exception(ChatV2Failure.Contract);
        try
        {
            var login = request.Login?.Restore() ?? throw new ChatV2Exception(ChatV2Failure.Session);
            if (login.ExpiresUtc <= clock.GetUtcNow().UtcDateTime) throw new ChatV2Exception(ChatV2Failure.Session);
            var identity = await persistence.ResolveAsync(login, ct);
            if (identity.OriginalLoginId != login.LoginId || identity.SessionId != login.LoginId ||
                identity.Actor != login.UserId || identity.Profile != login.ProfileId || identity.SecurityStamp != login.SecurityStamp ||
                identity.ExpiresUtc.UtcDateTime != login.ExpiresUtc || identity.ActorEpoch <= 0)
                throw new ChatV2Exception(ChatV2Failure.Session);
            if (request.Operation is not (BrokerOperation.Queue or BrokerOperation.Transfer) && request.AttentionTransfer is not null)
                throw new ChatV2Exception(ChatV2Failure.Validation);
            if (request.Operation == BrokerOperation.Resolve) return Ok(identity);
            if (request.Operation is BrokerOperation.Queue or BrokerOperation.Transfer)
            {
                if (identity.Kind != ChatV2Kind.Internal || !identity.Capabilities.HasFlag(ChatV2Capabilities.ManageAttention))
                    throw new ChatV2Exception(ChatV2Failure.Forbidden);
                // Management carries only queue metadata and an assignment CAS; never message content or a read grant.
                if (request.Resource is not null || request.OtherUser is not null || request.RequestId is not null ||
                    request.ContentHash is not null || request.ActorEpoch is not null || request.ResourceEpoch is not null ||
                    request.IntentExpiresUtc is not null || request.Content is not null || request.Cursor is not null)
                    throw new ChatV2Exception(ChatV2Failure.Validation);
                if (request.Operation == BrokerOperation.Queue)
                {
                    if (request.AttentionTransfer is not null) throw new ChatV2Exception(ChatV2Failure.Validation);
                    return Ok(await persistence.QueueAsync(login, ct));
                }
                var transfer = request.AttentionTransfer ?? throw new ChatV2Exception(ChatV2Failure.Validation);
                if (transfer.CaseId <= 0 || transfer.NewAgent <= 0 || transfer.ExpectedVersion < 0 || transfer.ExpectedVersion == long.MaxValue)
                    throw new ChatV2Exception(ChatV2Failure.Validation);
                if (transfer.NewAgent == identity.Actor) throw new ChatV2Exception(ChatV2Failure.Forbidden);
                var version = await persistence.TransferAsync(login, transfer.CaseId, transfer.NewAgent, transfer.ExpectedVersion, ct);
                if (version != transfer.ExpectedVersion + 1) throw new ChatV2Exception(ChatV2Failure.Contract);
                return Ok(version);
            }
            if (request.Operation == BrokerOperation.Directory) return Ok(await persistence.DirectoryAsync(login, ct));
            if (request.Operation == BrokerOperation.OpenPrivate)
            {
                if (request.OtherUser is not > 0 || request.OtherUser == login.UserId) throw new ChatV2Exception(ChatV2Failure.Validation);
                return Ok(await persistence.OpenPrivateAsync(login, request.OtherUser.Value, ct));
            }
            if (request.Resource is not ChatV2Resource resource || !Enum.IsDefined(resource.Domain) || resource.Id <= 0)
                throw new ChatV2Exception(ChatV2Failure.Validation);
            bool allowed = resource.Domain switch
            {
                ChatV2Domain.Private => identity.Kind == ChatV2Kind.Internal && identity.Capabilities.HasFlag(ChatV2Capabilities.InternalUse),
                ChatV2Domain.Department => persistence.SupportsDepartment && identity.Kind == ChatV2Kind.Internal && identity.Capabilities.HasFlag(ChatV2Capabilities.InternalUse),
                ChatV2Domain.Customer => persistence.SupportsCustomer && (identity.Kind == ChatV2Kind.Client && identity.Capabilities.HasFlag(ChatV2Capabilities.CustomerUse)
                    || identity.Kind == ChatV2Kind.Internal && identity.Capabilities.HasFlag(ChatV2Capabilities.AttendCustomers)),
                _ => false
            };
            if (!allowed) throw new ChatV2Exception(ChatV2Failure.Forbidden);
            switch (request.Operation)
            {
                case BrokerOperation.List: return Ok(await persistence.ListAsync(login, resource.Domain, ct));
                case BrokerOperation.Membership: return Ok(await persistence.MembershipAsync(login, resource, ct));
                case BrokerOperation.History:
                    if (request.Cursor?.Length > 2048) throw new ChatV2Exception(ChatV2Failure.Validation);
                    return Ok(await persistence.HistoryAsync(login, resource, request.Cursor, ct));
                case BrokerOperation.Leave: await persistence.LeaveAsync(login, resource, ct); return Ok(true);
            }
            if (request.RequestId is not Guid uuid || uuid == Guid.Empty || request.ContentHash is not { Length: 64 } hash ||
                !hash.All(Uri.IsHexDigit) || request.ActorEpoch != identity.ActorEpoch)
                throw new ChatV2Exception(ChatV2Failure.Conflict);
            if (request.Operation == BrokerOperation.Prepare)
                return Ok(await persistence.PrepareAsync(login, identity, resource, uuid, hash, ct));
            if (request.Operation is not (BrokerOperation.Send or BrokerOperation.Receipt) || request.ResourceEpoch is not >= 0 ||
                request.IntentExpiresUtc is not DateTimeOffset expires || expires > identity.ExpiresUtc)
                throw new ChatV2Exception(ChatV2Failure.Conflict);
            bool receipt = request.Operation == BrokerOperation.Receipt;
            if (receipt ? request.Content is not null : ChatV2Continuity.ContentHash(request.Content ?? "") != hash)
                throw new ChatV2Exception(ChatV2Failure.Conflict);
            // SQL enforces the exact immutable owner, expiry, captured epochs and content, including expired receipts.
            var bound = new ChatV2BoundIntent(login, identity, "broker-internal", resource, uuid, hash,
                request.ResourceEpoch.Value, expires);
            return Ok(await persistence.ExecuteAsync(bound, request.Content, receipt, ct));
        }
        catch (ChatV2Exception error) { return new(1, request.Correlation, error.Failure, null); }
        BrokerResponse Ok<T>(T result) => BrokerResponse.Success(request.Correlation, result);
    }
}
