using LMS.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace LMS.Data;

public class MongoSettings
{
    public string ConnectionString { get; set; } = "";
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
    public GridFSBucket FileBucket { get; }
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
    public IMongoCollection<Permission> Permissions => _database.GetCollection<Permission>("Permissions");
    public IMongoCollection<AcademicProgram> AcademicPrograms => _database.GetCollection<AcademicProgram>("AcademicPrograms");
    public IMongoCollection<AcademicTerm> AcademicTerms => _database.GetCollection<AcademicTerm>("AcademicTerms");
    public IMongoCollection<ProgramCourse> ProgramCourses => _database.GetCollection<ProgramCourse>("ProgramCourses");
    public IMongoCollection<StudentRegistration> StudentRegistrations => _database.GetCollection<StudentRegistration>("StudentRegistrations");
    public IMongoCollection<PasswordResetToken> PasswordResetTokens => _database.GetCollection<PasswordResetToken>("PasswordResetTokens");
    public IMongoCollection<TuitionInvoice> TuitionInvoices => _database.GetCollection<TuitionInvoice>("TuitionInvoices");

    public MongoContext(IOptions<MongoSettings> options)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            throw new InvalidOperationException(
                "MongoDB is not configured. Set MongoDb__ConnectionString in the environment.");
        if (string.IsNullOrWhiteSpace(settings.DatabaseName))
            throw new InvalidOperationException(
                "MongoDB database name is missing. Set MongoDb__DatabaseName in the environment.");

        var client = new MongoClient(settings.ConnectionString);
        _database = client.GetDatabase(settings.DatabaseName);
        FileBucket = new GridFSBucket(_database, new GridFSBucketOptions { BucketName = "LmsUploads" });
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
        Permissions.Indexes.CreateOne(new CreateIndexModel<Permission>(
            Builders<Permission>.IndexKeys.Ascending(x => x.Code),
            new CreateIndexOptions { Unique = true }));
        AcademicPrograms.Indexes.CreateOne(new CreateIndexModel<AcademicProgram>(
            Builders<AcademicProgram>.IndexKeys.Ascending(x => x.Code),
            new CreateIndexOptions { Unique = true }));
        AcademicTerms.Indexes.CreateOne(new CreateIndexModel<AcademicTerm>(
            Builders<AcademicTerm>.IndexKeys.Ascending(x => x.Code),
            new CreateIndexOptions { Unique = true }));
        ProgramCourses.Indexes.CreateOne(new CreateIndexModel<ProgramCourse>(
            Builders<ProgramCourse>.IndexKeys.Ascending(x => x.ProgramId).Ascending(x => x.SubjectId),
            new CreateIndexOptions { Unique = true }));
        StudentRegistrations.Indexes.CreateOne(new CreateIndexModel<StudentRegistration>(
            Builders<StudentRegistration>.IndexKeys.Ascending(x => x.StudentId).Ascending(x => x.ClassId),
            new CreateIndexOptions { Unique = true }));
        PasswordResetTokens.Indexes.CreateOne(new CreateIndexModel<PasswordResetToken>(
            Builders<PasswordResetToken>.IndexKeys.Ascending(x => x.UserId),
            new CreateIndexOptions { Unique = true }));
        PasswordResetTokens.Indexes.CreateOne(new CreateIndexModel<PasswordResetToken>(
            Builders<PasswordResetToken>.IndexKeys.Ascending(x => x.ExpiresAt),
            new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }));
        TuitionInvoices.Indexes.CreateOne(new CreateIndexModel<TuitionInvoice>(
            Builders<TuitionInvoice>.IndexKeys.Ascending(x => x.StudentId).Ascending(x => x.ClassId),
            new CreateIndexOptions { Unique = true }));
        TuitionInvoices.Indexes.CreateOne(new CreateIndexModel<TuitionInvoice>(
            Builders<TuitionInvoice>.IndexKeys.Ascending(x => x.VnpayTransactionReference),
            new CreateIndexOptions<TuitionInvoice>
            {
                Unique = true,
                PartialFilterExpression = Builders<TuitionInvoice>.Filter.Gt(x => x.VnpayTransactionReference, "")
            }));
    }
}
