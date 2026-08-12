namespace Proyecto_Final.Services;

public interface IRolePermissionService
{
    Task<bool> HasModulePermissionAsync(string? roleName, string module);

    Task<bool> HasCodePermissionAsync(string roleName, string permissionCode);
}
