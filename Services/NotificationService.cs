using LMS.Data;
using LMS.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LMS.Services;

public sealed class NotificationService(MongoContext db)
{
    public Task AddAsync(ObjectId userId, string type, string title, string message, string link) =>
        db.Notifications.InsertOneAsync(new Notification { UserId = userId, Type = type, Title = title,
            Message = message, Link = link });

    public async Task NotifyAdminsAsync(string type, string title, string message, string link)
    {
        var ids = await db.Users.Find(x => x.Role == Roles.Admin && x.IsActive).Project(x => x.Id).ToListAsync();
        if (ids.Count > 0) await db.Notifications.InsertManyAsync(ids.Select(id => new Notification
            { UserId = id, Type = type, Title = title, Message = message, Link = link }));
    }

    public Task<List<Notification>> ListAsync(ObjectId userId) => db.Notifications.Find(x => x.UserId == userId)
        .SortByDescending(x => x.CreatedAt).Limit(50).ToListAsync();

    public Task<long> UnreadAsync(ObjectId userId) => db.Notifications.CountDocumentsAsync(x => x.UserId == userId && x.ReadAt == null);

    public Task MarkReadAsync(ObjectId userId, ObjectId? id = null) => db.Notifications.UpdateManyAsync(
        id.HasValue ? Builders<Notification>.Filter.Where(x => x.UserId == userId && x.Id == id.Value)
            : Builders<Notification>.Filter.Where(x => x.UserId == userId && x.ReadAt == null),
        Builders<Notification>.Update.Set(x => x.ReadAt, DateTime.UtcNow));
}
