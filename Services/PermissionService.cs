using System.Text.RegularExpressions;
using LMS.Data;
using LMS.Models;
using MongoDB.Driver;

namespace LMS.Services;

public class PermissionService(MongoContext db)
{
    private static readonly Regex PermissionCodePattern = new(
        "^[a-z][a-z0-9]*(\\.[a-z][a-z0-9]*){1,4}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task InitializeAsync()
    {
        foreach (var definition in PermissionCodes.SystemPermissions)
        {
            await db.Permissions.UpdateOneAsync(
                x => x.Code == definition.Code,
                Builders<Permission>.Update
                    .SetOnInsert(x => x.Code, definition.Code)
                    .Set(x => x.Name, definition.Name)
                    .Set(x => x.Category, definition.Category)
                    .Set(x => x.Description, definition.Description)
                    .Set(x => x.IsSystem, true),
                new UpdateOptions { IsUpsert = true });
        }

        var users = await db.Users.Find(x => !x.PermissionsInitialized).ToListAsync();
        foreach (var user in users)
        {
            user.PermissionCodes = PermissionCodes.DefaultsForRole(user.Role).ToList();
            user.PermissionsInitialized = true;
            user.StudentPortalPermissionsInitialized = user.Role == Roles.Student;
            await db.Users.UpdateOneAsync(
                x => x.Id == user.Id,
                Builders<User>.Update
                    .Set(x => x.PermissionCodes, user.PermissionCodes)
                    .Set(x => x.PermissionsInitialized, true)
                    .Set(x => x.StudentPortalPermissionsInitialized, user.StudentPortalPermissionsInitialized));
        }

        var studentsNeedingPortalPermissions = await db.Users.Find(x =>
            x.Role == Roles.Student && !x.StudentPortalPermissionsInitialized).ToListAsync();
        foreach (var student in studentsNeedingPortalPermissions)
        {
            var grants = student.PermissionCodes.ToHashSet(StringComparer.Ordinal);
            grants.Add(PermissionCodes.StudentProgressView);
            grants.Add(PermissionCodes.TuitionView);
            grants.Add(PermissionCodes.TuitionPay);
            await db.Users.UpdateOneAsync(
                x => x.Id == student.Id,
                Builders<User>.Update
                    .Set(x => x.PermissionCodes, grants.ToList())
                    .Set(x => x.StudentPortalPermissionsInitialized, true));
        }
    }

    public Task<List<Permission>> GetAllAsync() =>
        db.Permissions.Find(_ => true).SortBy(x => x.Category).ThenBy(x => x.Name).ToListAsync();

    public async Task<bool> CreateAsync(Permission permission)
    {
        permission.Code = permission.Code.Trim().ToLowerInvariant();
        permission.Name = permission.Name.Trim();
        permission.Category = permission.Category.Trim();
        permission.Description = permission.Description.Trim();
        permission.ControllerName = permission.ControllerName.Trim();
        permission.ActionName = permission.ActionName.Trim();
        if (!PermissionCodePattern.IsMatch(permission.Code) ||
            string.IsNullOrWhiteSpace(permission.Name) ||
            string.IsNullOrWhiteSpace(permission.Category) ||
            permission.Name.Length > 100 ||
            permission.Category.Length > 60 ||
            permission.Description.Length > 500 ||
            (string.IsNullOrWhiteSpace(permission.ControllerName) != string.IsNullOrWhiteSpace(permission.ActionName)) ||
            await db.Permissions.Find(x => x.Code == permission.Code).AnyAsync())
        {
            return false;
        }

        if (!string.IsNullOrEmpty(permission.ControllerName))
        {
            var mapped = await db.Permissions.Find(x =>
                x.ControllerName != "" && x.ActionName != "").ToListAsync();
            if (mapped.Any(x =>
                    x.ControllerName.Equals(permission.ControllerName, StringComparison.OrdinalIgnoreCase) &&
                    x.ActionName.Equals(permission.ActionName, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        await db.Permissions.InsertOneAsync(permission);
        return true;
    }

    public async Task<string?> GetCodeForActionAsync(string? controllerName, string? actionName)
    {
        if (string.IsNullOrWhiteSpace(controllerName) || string.IsNullOrWhiteSpace(actionName))
            return null;

        var mapped = await db.Permissions.Find(x => x.ControllerName != "" && x.ActionName != "")
            .ToListAsync();
        return mapped.FirstOrDefault(x =>
            x.ControllerName.Equals(controllerName, StringComparison.OrdinalIgnoreCase) &&
            x.ActionName.Equals(actionName, StringComparison.OrdinalIgnoreCase))?.Code;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out var permissionId))
            return false;
        var permission = await db.Permissions.Find(x => x.Id == permissionId).FirstOrDefaultAsync();
        if (permission is null || permission.IsSystem)
            return false;

        if (await db.Users.Find(x => x.PermissionCodes.Contains(permission.Code)).AnyAsync())
            return false;

        return (await db.Permissions.DeleteOneAsync(x => x.Id == permissionId)).DeletedCount == 1;
    }

    public async Task<bool> SetForUserAsync(string userId, IReadOnlyCollection<string> codes)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(userId, out var objectId))
            return false;
        var user = await db.Users.Find(x => x.Id == objectId && x.IsActive).FirstOrDefaultAsync();
        if (user is null || user.Role == Roles.Admin)
            return false;

        var uniqueCodes = codes.Distinct(StringComparer.Ordinal).ToArray();
        if (!await AreValidCodesAsync(uniqueCodes))
            return false;

        await db.Users.UpdateOneAsync(
            x => x.Id == objectId,
            Builders<User>.Update
                .Set(x => x.PermissionCodes, uniqueCodes.ToList())
                .Set(x => x.PermissionsInitialized, true));
        return true;
    }

    public async Task<bool> AreValidCodesAsync(IReadOnlyCollection<string> codes)
    {
        var uniqueCodes = codes.Distinct(StringComparer.Ordinal).ToArray();
        var validCodes = await db.Permissions.Find(x => uniqueCodes.Contains(x.Code))
            .Project(x => x.Code).ToListAsync();
        return validCodes.Count == uniqueCodes.Length;
    }
}
