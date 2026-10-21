using LMS.Data;
using LMS.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LMS.Services;

public sealed class LessonProgressService(MongoContext db, LearningEventService events)
{
    public async Task<IReadOnlyDictionary<ObjectId, DateTime>> GetAsync(ObjectId studentId, IEnumerable<ObjectId> lessonIds)
    {
        var ids = lessonIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<ObjectId, DateTime>();
        var rows = await db.LessonCompletions.Find(x => x.StudentId == studentId && ids.Contains(x.LessonId)).ToListAsync();
        return rows.ToDictionary(x => x.LessonId, x => x.CompletedAt);
    }

    public async Task<DateTime> CompleteAsync(Lesson lesson, User student)
    {
        var now = DateTime.UtcNow;
        var result = await db.LessonCompletions.UpdateOneAsync(
            x => x.LessonId == lesson.Id && x.StudentId == student.Id,
            Builders<LessonCompletion>.Update
                .SetOnInsert(x => x.LessonId, lesson.Id)
                .SetOnInsert(x => x.ClassId, lesson.ClassId)
                .SetOnInsert(x => x.StudentId, student.Id)
                .SetOnInsert(x => x.CompletedAt, now),
            new UpdateOptions { IsUpsert = true });
        var row = await db.LessonCompletions.Find(x => x.LessonId == lesson.Id && x.StudentId == student.Id).FirstAsync();
        if (result.UpsertedId is not null)
            await events.RecordAsync("lesson.completed", student, "Lesson", lesson.Id,
                $"Đã hoàn thành bài học: {lesson.Title}", student.Id, student.DepartmentId, lesson.ClassId);
        return row.CompletedAt;
    }

    public async Task<bool> ReopenAsync(Lesson lesson, User student)
    {
        var result = await db.LessonCompletions.DeleteOneAsync(x => x.LessonId == lesson.Id && x.StudentId == student.Id);
        if (result.DeletedCount == 0) return false;
        await events.RecordAsync("lesson.reopened", student, "Lesson", lesson.Id,
            $"Đã bỏ đánh dấu hoàn thành: {lesson.Title}", student.Id, student.DepartmentId, lesson.ClassId);
        return true;
    }
}
