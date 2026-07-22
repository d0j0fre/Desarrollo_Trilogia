namespace Proyecto_Final.Services
{
    public interface IChatAuthorizationService
    {
        Task<bool> CanAccessConversationAsync(int userId, int conversationId);
        Task<bool> CanAccessDepartmentAsync(int userId, string? role, int departmentId);
        Task<bool> CanPostToDepartmentAsync(int userId, string? role, int departmentId);
        bool CanManageAllDepartments(string? role);
    }

    public sealed class ChatAuthorizationService : IChatAuthorizationService
    {
        private readonly IChatDbService _chatDbService;

        public ChatAuthorizationService(IChatDbService chatDbService)
        {
            _chatDbService = chatDbService;
        }

        public Task<bool> CanAccessConversationAsync(int userId, int conversationId)
        {
            if (userId <= 0 || conversationId <= 0)
            {
                return Task.FromResult(false);
            }

            return _chatDbService.IsConversationMemberAsync(conversationId, userId);
        }

        public Task<bool> CanAccessDepartmentAsync(int userId, string? role, int departmentId)
        {
            if (userId <= 0 || departmentId <= 0)
            {
                return Task.FromResult(false);
            }

            return _chatDbService.IsDepartmentMemberAsync(
                departmentId,
                userId,
                CanManageAllDepartments(role));
        }

        public Task<bool> CanPostToDepartmentAsync(int userId, string? role, int departmentId)
        {
            if (userId <= 0 || departmentId <= 0)
            {
                return Task.FromResult(false);
            }

            return _chatDbService.CanPostToDepartmentAsync(
                departmentId,
                userId,
                CanManageAllDepartments(role));
        }

        public bool CanManageAllDepartments(string? role) =>
            string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase);
    }
}
