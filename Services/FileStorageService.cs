using LMS.Data;
using LMS.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace LMS.Services;

public class FileStorageService
{
    public const long MaxFileSize = 25 * 1024 * 1024;
    public const int MaxFilesPerUpload = 5;

    private static readonly IReadOnlyDictionary<string, string> AllowedContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xls"] = "application/vnd.ms-excel",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".ppt"] = "application/vnd.ms-powerpoint",
            [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            [".txt"] = "text/plain",
            [".csv"] = "text/csv",
            [".zip"] = "application/zip",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg"
        };

    private readonly MongoContext _db;

    public FileStorageService(MongoContext db) => _db = db;

    public async Task<StoredFileReference> UploadAsync(
        IFormFile file,
        ObjectId ownerId,
        ObjectId classId,
        string purpose,
        ObjectId? assignmentId = null)
    {
        if (file.Length is <= 0 or > MaxFileSize)
            throw new InvalidOperationException("Tệp phải có dung lượng từ 1 byte đến 25 MB.");

        var fileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(fileName);
        if (!AllowedContentTypes.TryGetValue(extension, out var contentType))
            throw new InvalidOperationException("Định dạng tệp chưa được hỗ trợ.");

        var metadata = new BsonDocument
        {
            ["ownerId"] = ownerId,
            ["classId"] = classId,
            ["purpose"] = purpose,
            ["contentType"] = contentType
        };
        if (assignmentId.HasValue)
            metadata["assignmentId"] = assignmentId.Value;

        await using var stream = file.OpenReadStream();
        var id = await _db.FileBucket.UploadFromStreamAsync(
            fileName,
            stream,
            new GridFSUploadOptions { Metadata = metadata });

        return new StoredFileReference
        {
            Id = id,
            FileName = fileName,
            Length = file.Length,
            ContentType = contentType
        };
    }

    public async Task<IReadOnlyList<StoredFileReference>?> ValidateStagedFilesAsync(
        string? rawIds,
        ObjectId ownerId,
        ObjectId classId,
        string purpose,
        ObjectId? assignmentId = null)
    {
        var ids = ParseIds(rawIds);
        if (ids is null || ids.Count > MaxFilesPerUpload)
            return null;

        var references = new List<StoredFileReference>(ids.Count);
        foreach (var id in ids)
        {
            var info = await GetInfoAsync(id);
            if (info?.Metadata is not { } metadata ||
                !HasObjectId(metadata, "ownerId", ownerId) ||
                !HasObjectId(metadata, "classId", classId) ||
                !HasString(metadata, "purpose", purpose) ||
                (assignmentId.HasValue && !HasObjectId(metadata, "assignmentId", assignmentId.Value)))
            {
                return null;
            }

            references.Add(ToReference(info));
        }

        return references;
    }

    public async Task<StoredFileReference?> GetReferenceAsync(ObjectId id)
    {
        var info = await GetInfoAsync(id);
        return info is null ? null : ToReference(info);
    }

    public Task<GridFSDownloadStream> OpenDownloadStreamAsync(ObjectId id) =>
        _db.FileBucket.OpenDownloadStreamAsync(id);

    public async Task<GridFSFileInfo?> GetInfoAsync(ObjectId id)
    {
        var filter = Builders<GridFSFileInfo>.Filter.Eq(x => x.Id, id);
        return await _db.FileBucket.Find(filter).FirstOrDefaultAsync();
    }

    public async Task DeleteIfUnreferencedAsync(ObjectId id)
    {
        if (await _db.Assignments.Find(x => x.Attachments.Any(file => file.Id == id)).AnyAsync() ||
            await _db.Submissions.Find(x => x.Attachments.Any(file => file.Id == id)).AnyAsync())
        {
            return;
        }

        await _db.FileBucket.DeleteAsync(id);
    }

    public async Task<bool> DeleteStagedFileAsync(ObjectId id, ObjectId ownerId)
    {
        var info = await GetInfoAsync(id);
        if (info?.Metadata is not { } metadata || !HasObjectId(metadata, "ownerId", ownerId))
            return false;

        await DeleteIfUnreferencedAsync(id);
        return true;
    }

    public static IReadOnlyList<ObjectId>? ParseIds(string? rawIds)
    {
        if (string.IsNullOrWhiteSpace(rawIds))
            return Array.Empty<ObjectId>();

        var parts = rawIds.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var ids = new List<ObjectId>(parts.Length);
        foreach (var part in parts)
        {
            if (!ObjectId.TryParse(part, out var id) || ids.Contains(id))
                return null;
            ids.Add(id);
        }

        return ids;
    }

    private static StoredFileReference ToReference(GridFSFileInfo info)
    {
        var contentType = info.Metadata?.GetValue("contentType", "application/octet-stream").AsString
            ?? "application/octet-stream";
        return new StoredFileReference
        {
            Id = info.Id,
            FileName = Path.GetFileName(info.Filename),
            Length = info.Length,
            ContentType = contentType
        };
    }

    private static bool HasObjectId(BsonDocument metadata, string key, ObjectId value) =>
        metadata.TryGetValue(key, out var actual) && actual.IsObjectId && actual.AsObjectId == value;

    private static bool HasString(BsonDocument metadata, string key, string value) =>
        metadata.TryGetValue(key, out var actual) && actual.IsString && actual.AsString == value;
}
