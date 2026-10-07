using LMS.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LMS.Data;

public class MongoSettings
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "LMS_Secure";
}

public class SecuritySettings
{
    public int MaxLoginAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}

public class MongoContext
{
    private readonly IMongoDatabase _database;
    public IMongoCollection<User> Users => _database.GetCollection<User>("Users");
    public IMongoCollection<ClassRoom> Classes => _database.GetCollection<ClassRoom>("Classes");
    public IMongoCollection<Subject> Subjects => _database.GetCollection<Subject>("Subjects");
    public IMongoCollection<Department> Departments => _database.GetCollection<Department>("Departments");
    public IMongoCollection<Lesson> Lessons => _database.GetCollection<Lesson>("Lessons");
    public IMongoCollection<Assignment> Assignments => _database.GetCollection<Assignment>("Assignments");
    public IMongoCollection<Submission> Submissions => _database.GetCollection<Submission>("Submissions");
    public IMongoCollection<ActivityLog> ActivityLogs => _database.GetCollection<ActivityLog>("ActivityLogs");
    public IMongoCollection<TeacherAvailability> TeacherAvailabilities => _database.GetCollection<TeacherAvailability>("TeacherAvailabilities");
    public IMongoCollection<ClassSession> ClassSessions => _database.GetCollection<ClassSession>("ClassSessions");
    public IMongoCollection<SessionEnrollment> SessionEnrollments => _database.GetCollection<SessionEnrollment>("SessionEnrollments");

    public MongoContext(IOptions<MongoSettings> options)
    {
        var client = new MongoClient(options.Value.ConnectionString);
        _database = client.GetDatabase(options.Value.DatabaseName);
        Departments.Indexes.CreateOne(new CreateIndexModel<Department>(
            Builders<Department>.IndexKeys.Ascending(x => x.Code),
            new CreateIndexOptions { Unique = true }));
        Subjects.Indexes.CreateOne(new CreateIndexModel<Subject>(
            Builders<Subject>.IndexKeys.Ascending(x => x.Code),
            new CreateIndexOptions { Unique = true }));
        SessionEnrollments.Indexes.CreateOne(new CreateIndexModel<SessionEnrollment>(
            Builders<SessionEnrollment>.IndexKeys
                .Ascending(x => x.SessionId)
                .Ascending(x => x.StudentId),
            new CreateIndexOptions { Unique = true }));
    }
}
