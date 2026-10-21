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
                    .Set(x => x.IsSystem, true)
                    .Set(x => x.ControllerName, "")
                    .Set(x => x.ActionName, "")
                    .Set(x => x.AllowedRoles, new[] { Roles.Teacher, Roles.Student, Roles.Staff }.Where(role => PermissionCodes.DefaultsForRole(role).Contains(definition.Code)).ToList())
                    .Set(x => x.RequiredCodes, PermissionCodes.RequiredFor(definition.Code).ToList()),
                new UpdateOptions { IsUpsert = true });
        }

        var definitions = await GetAllAsync();
        foreach (var user in await db.Users.Find(_ => true).ToListAsync())
        {
            var proposed = !user.PermissionsInitialized && user.PermissionCodes.Count == 0
                ? PermissionCodes.DefaultsForRole(user.Role).ToList()
                : user.PermissionCodes.ToList();
            if (user.Role == Roles.Student && !user.StudentPortalPermissionsInitialized)
                proposed.AddRange(new[] { PermissionCodes.StudentProgressView, PermissionCodes.TuitionView, PermissionCodes.TuitionPay });
            var clean = PermissionCodes.EffectiveFor(user.Role, proposed, definitions);
            var portalInitialized = user.StudentPortalPermissionsInitialized || user.Role == Roles.Student;
            if (user.PermissionsInitialized && user.StudentPortalPermissionsInitialized == portalInitialized &&
                user.PermissionCodes.SequenceEqual(clean)) continue;

            var snapshot = new PermissionMigration { UserId = user.Id, Role = user.Role,
                Before = user.PermissionCodes.ToList(), After = clean };
            await db.PermissionMigrations.InsertOneAsync(snapshot);
            // Match the snapshot so startup cannot overwrite a concurrent revocation or role change.
            var filter = Builders<User>.Filter.And(
                Builders<User>.Filter.Eq(x => x.Id, user.Id),
                Builders<User>.Filter.Eq(x => x.Role, user.Role),
                user.PermissionCodes.Count == 0
                    ? Builders<User>.Filter.Or(Builders<User>.Filter.Eq(x => x.PermissionCodes, user.PermissionCodes), Builders<User>.Filter.Exists(x => x.PermissionCodes, false))
                    : Builders<User>.Filter.Eq(x => x.PermissionCodes, user.PermissionCodes),
                user.PermissionsInitialized
                    ? Builders<User>.Filter.Eq(x => x.PermissionsInitialized, true)
                    : Builders<User>.Filter.Ne(x => x.PermissionsInitialized, true),
                user.StudentPortalPermissionsInitialized
                    ? Builders<User>.Filter.Eq(x => x.StudentPortalPermissionsInitialized, true)
                    : Builders<User>.Filter.Ne(x => x.StudentPortalPermissionsInitialized, true));
            var result = await db.Users.UpdateOneAsync(filter, Builders<User>.Update
                .Set(x => x.PermissionCodes, clean).Set(x => x.PermissionsInitialized, true)
                .Set(x => x.StudentPortalPermissionsInitialized, portalInitialized));
            await db.PermissionMigrations.UpdateOneAsync(x => x.Id == snapshot.Id,
                Builders<PermissionMigration>.Update.Set(x => x.Applied, result.MatchedCount == 1));
        }
    }

    public async Task<List<string>> GetEffectiveCodesAsync(User user) =>
        user.IsActive ? PermissionCodes.EffectiveFor(user.Role, user.PermissionCodes, await GetAllAsync()) : new();

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
        if (!await AreValidForRoleAsync(user.Role, uniqueCodes))
            return false;

        var result = await db.Users.UpdateOneAsync(
            x => x.Id == objectId && x.IsActive && x.Role == user.Role,
            Builders<User>.Update
                .Set(x => x.PermissionCodes, uniqueCodes.ToList())
                .Set(x => x.PermissionsInitialized, true)
                .Set(x => x.StudentPortalPermissionsInitialized, user.Role == Roles.Student));
        return result.MatchedCount == 1;
    }

    public async Task<bool> AreValidForUserAsync(string userId, IReadOnlyCollection<string> codes)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(userId, out var id)) return false;
        var user = await db.Users.Find(x => x.Id == id && x.IsActive).FirstOrDefaultAsync();
        return user is not null && user.Role != Roles.Admin &&
            await AreValidForRoleAsync(user.Role, codes);
    }

    private async Task<bool> AreValidForRoleAsync(string role, IReadOnlyCollection<string> codes)
    {
        var expected = codes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        return expected.SequenceEqual(PermissionCodes.EffectiveFor(role, codes, await GetAllAsync()));
    }

    public async Task<bool> AreValidCodesAsync(IReadOnlyCollection<string> codes)
    {
        var uniqueCodes = codes.Distinct(StringComparer.Ordinal).ToArray();
        var validCodes = await db.Permissions.Find(x => uniqueCodes.Contains(x.Code))
            .Project(x => x.Code).ToListAsync();
        return validCodes.Count == uniqueCodes.Length;
    }
}
