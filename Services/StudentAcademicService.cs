using LMS.Data;
using LMS.Models;
using LMS.ViewModels;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LMS.Services;

public sealed class StudentAcademicService(MongoContext db)
{
    public async Task<StudentAcademicDashboardViewModel> GetDashboardAsync(User student)
    {
        if (student.Role != Roles.Student)
            return new StudentAcademicDashboardViewModel();

        var registrations = await db.StudentRegistrations.Find(x =>
            x.StudentId == student.Id && x.Status == StudentRegistrationStatuses.Active).ToListAsync();
        var classIds = registrations.Select(x => x.ClassId).Distinct().ToArray();
        var classes = classIds.Length == 0
            ? new List<ClassRoom>()
            : await db.Classes.Find(Builders<ClassRoom>.Filter.And(
                Builders<ClassRoom>.Filter.In(x => x.Id, classIds),
                Builders<ClassRoom>.Filter.Eq(x => x.IsArchived, false))).ToListAsync();
        var subjectIds = classes.Select(x => x.SubjectId).Distinct().ToArray();
        var subjects = subjectIds.Length == 0
            ? new List<Subject>()
            : await db.Subjects.Find(Builders<Subject>.Filter.In(x => x.Id, subjectIds)).ToListAsync();
        var terms = await db.AcademicTerms.Find(_ => true).ToListAsync();
        var programCourses = student.ProgramId is { } programId
            ? await db.ProgramCourses.Find(x => x.ProgramId == programId).ToListAsync()
            : new List<ProgramCourse>();
        var invoices = classIds.Length == 0
            ? new List<TuitionInvoice>()
            : await db.TuitionInvoices.Find(Builders<TuitionInvoice>.Filter.And(
                Builders<TuitionInvoice>.Filter.Eq(x => x.StudentId, student.Id),
                Builders<TuitionInvoice>.Filter.In(x => x.ClassId, classIds))).ToListAsync();
        var assignments = classIds.Length == 0
            ? new List<Assignment>()
            : await db.Assignments.Find(Builders<Assignment>.Filter.And(
                Builders<Assignment>.Filter.In(x => x.ClassId, classIds),
                Builders<Assignment>.Filter.Eq(x => x.IsArchived, false))).ToListAsync();
        var assignmentIds = assignments.Select(x => x.Id).ToArray();
        var submissions = assignmentIds.Length == 0
            ? new List<Submission>()
            : await db.Submissions.Find(Builders<Submission>.Filter.And(
                Builders<Submission>.Filter.Eq(x => x.StudentId, student.Id),
                Builders<Submission>.Filter.In(x => x.AssignmentId, assignmentIds))).ToListAsync();

        var subjectsById = subjects.ToDictionary(x => x.Id);
        var termsById = terms.ToDictionary(x => x.Id);
        var coursesBySubject = programCourses
            .GroupBy(x => x.SubjectId)
            .ToDictionary(group => group.Key, group => group.First());
        var invoiceByClass = invoices
            .GroupBy(x => x.ClassId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(x => x.UpdatedAt).First());
        var assignmentsByClass = assignments.GroupBy(x => x.ClassId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var assignmentById = assignments.ToDictionary(x => x.Id);
        var submissionsByAssignment = submissions
            .Where(x => x.Grade.HasValue)
            .GroupBy(x => x.AssignmentId)
            .ToDictionary(group => group.Key, group => group.Select(x => x.Grade!.Value).Average());

        var courseItems = new List<StudentCourseProgressViewModel>();
        var tuitionItems = new List<StudentTuitionItemViewModel>();
        foreach (var classroom in classes)
        {
            if (!subjectsById.TryGetValue(classroom.SubjectId, out var subject))
                continue;
            var registration = registrations.FirstOrDefault(x => x.ClassId == classroom.Id);
            if (registration is null)
                continue;
            var termId = classroom.AcademicTermId ?? registration.AcademicTermId;
            termsById.TryGetValue(termId, out var term);
            coursesBySubject.TryGetValue(classroom.SubjectId, out var programCourse);
            var credits = programCourse?.Credits ?? 0;

            var classAssignments = assignmentsByClass.GetValueOrDefault(classroom.Id) ?? new List<Assignment>();
            var gradedScores = classAssignments
                .Where(assignment => submissionsByAssignment.ContainsKey(assignment.Id))
                .Select(assignment => submissionsByAssignment[assignment.Id])
                .ToArray();
            courseItems.Add(new StudentCourseProgressViewModel
            {
                ClassId = classroom.Id.ToString(),
                ClassName = classroom.Name,
                SubjectCode = subject.Code,
                SubjectName = subject.Name,
                TermName = term?.Name ?? "Học kỳ không xác định",
                AcademicYear = term?.AcademicYear ?? "",
                Credits = credits,
                GradedAssignmentCount = gradedScores.Length,
                AssignmentCount = classAssignments.Count,
                AverageAssignmentGrade = gradedScores.Length == 0 ? null : gradedScores.Average()
            });

            if (term is null)
                continue;
            var invoice = await EnsureInvoiceAsync(student.Id, classroom, term, credits);
            if (invoice is not null)
                invoiceByClass[classroom.Id] = invoice;

            var rate = invoice?.TuitionPerCredit ?? term.TuitionPerCredit;
            tuitionItems.Add(new StudentTuitionItemViewModel
            {
                ClassId = classroom.Id.ToString(),
                ClassName = classroom.Name,
                SubjectCode = subject.Code,
                SubjectName = subject.Name,
                TermName = term.Name,
                Credits = invoice?.Credits ?? credits,
                TuitionPerCredit = rate,
                AmountVnd = invoice?.AmountVnd ?? decimal.ToInt64(decimal.Round(rate * credits, 0, MidpointRounding.AwayFromZero)),
                Status = invoice?.Status ?? TuitionInvoiceStatuses.Pending,
                InvoiceId = invoice?.Id.ToString(),
                CanPay = invoice is { Status: TuitionInvoiceStatuses.Pending, AmountVnd: > 0 }
            });
        }

        var allGrades = submissionsByAssignment
            .Where(pair => assignmentById.ContainsKey(pair.Key))
            .Select(pair => pair.Value)
            .ToArray();
        var program = student.ProgramId is { } id
            ? await db.AcademicPrograms.Find(x => x.Id == id).FirstOrDefaultAsync()
            : null;

        return new StudentAcademicDashboardViewModel
        {
            ProgramName = program?.Name ?? "Chưa được gán ngành học",
            TotalCredits = courseItems.Sum(x => x.Credits),
            OverallAssignmentAverage = allGrades.Length == 0 ? null : allGrades.Average(),
            Courses = courseItems.OrderByDescending(x => x.AcademicYear).ThenBy(x => x.SubjectCode).ToList(),
            TuitionItems = tuitionItems.OrderBy(x => x.TermName).ThenBy(x => x.SubjectCode).ToList()
        };
    }

    public async Task<(bool Success, string? PaymentUrl, string Message)> CreatePaymentUrlAsync(
        User student,
        string classId,
        string clientIpAddress,
        string returnUrl,
        string? ipnUrl,
        VnpayPaymentService vnpay,
        bool requireHttps,
        CancellationToken cancellationToken)
    {
        if (student.Role != Roles.Student || !ObjectId.TryParse(classId, out var classObjectId))
            return (false, null, "Không tìm thấy khoản học phí.");

        var registration = await db.StudentRegistrations.Find(x =>
            x.StudentId == student.Id &&
            x.ClassId == classObjectId &&
            x.Status == "Active").FirstOrDefaultAsync(cancellationToken);
        if (registration is null)
            return (false, null, "Chỉ có thể thanh toán môn học bạn đang theo học.");

        var classroom = await db.Classes.Find(x => x.Id == classObjectId && !x.IsArchived)
            .FirstOrDefaultAsync(cancellationToken);
        if (classroom is null)
            return (false, null, "Không tìm thấy lớp học.");

        var termId = classroom.AcademicTermId ?? registration.AcademicTermId;
        var term = await db.AcademicTerms.Find(x => x.Id == termId).FirstOrDefaultAsync(cancellationToken);
        var programCourse = student.ProgramId is { } programId
            ? await db.ProgramCourses.Find(x =>
                x.ProgramId == programId && x.SubjectId == classroom.SubjectId).FirstOrDefaultAsync(cancellationToken)
            : null;
        if (term is null || programCourse is null || term.TuitionPerCredit <= 0)
            return (false, null, "Học phí chưa được cấu hình cho học kỳ hoặc môn học này. Vui lòng liên hệ quản trị viên.");

        var invoice = await EnsureInvoiceAsync(
            student.Id, classroom, term, programCourse.Credits, cancellationToken);
        if (invoice is null)
            return (false, null, "Không thể tạo khoản học phí. Vui lòng tải lại trang.");
        if (invoice.Status == TuitionInvoiceStatuses.Paid)
            return (false, null, "Khoản học phí này đã được thanh toán.");
        if (invoice.Status != TuitionInvoiceStatuses.Pending || invoice.AmountVnd <= 0)
            return (false, null, "Khoản học phí hiện không thể thanh toán.");

        var nowUtc = DateTime.UtcNow;
        var paymentExpiresAt = nowUtc.AddMinutes(-15);
        if (invoice.PaymentInitiatedAt > paymentExpiresAt)
            return (false, null, "Yêu cầu thanh toán hiện đang chờ xử lý. Vui lòng đợi tối đa 15 phút trước khi thử lại.");

        var reference = Guid.NewGuid().ToString("N");
        var pendingExpiredPayment = Builders<TuitionInvoice>.Filter.Or(
            Builders<TuitionInvoice>.Filter.Eq(x => x.PaymentInitiatedAt, null),
            Builders<TuitionInvoice>.Filter.Lte(x => x.PaymentInitiatedAt, paymentExpiresAt));
        var updateResult = await db.TuitionInvoices.UpdateOneAsync(
            Builders<TuitionInvoice>.Filter.And(
                Builders<TuitionInvoice>.Filter.Eq(x => x.Id, invoice.Id),
                Builders<TuitionInvoice>.Filter.Eq(x => x.Status, TuitionInvoiceStatuses.Pending),
                pendingExpiredPayment),
            Builders<TuitionInvoice>.Update
                .Set(x => x.VnpayTransactionReference, reference)
                .Set(x => x.PaymentInitiatedAt, nowUtc)
                .Set(x => x.UpdatedAt, nowUtc),
            cancellationToken: cancellationToken);
        if (updateResult.ModifiedCount != 1)
            return (false, null, "Khoản học phí vừa được cập nhật. Vui lòng tải lại trang.");

        invoice.VnpayTransactionReference = reference;
        try
        {
            var url = vnpay.CreatePaymentUrl(
                invoice, clientIpAddress, returnUrl, ipnUrl, nowUtc, requireHttps);
            return (true, url, "Đang chuyển tới VNPAY.");
        }
        catch (VnpayConfigurationException)
        {
            await db.TuitionInvoices.UpdateOneAsync(
                x => x.Id == invoice.Id && x.VnpayTransactionReference == reference && x.Status == TuitionInvoiceStatuses.Pending,
                Builders<TuitionInvoice>.Update
                    .Set(x => x.VnpayTransactionReference, "")
                    .Set(x => x.PaymentInitiatedAt, (DateTime?)null),
                cancellationToken: cancellationToken);
            return (false, null, "Cổng VNPAY chưa được cấu hình. Vui lòng liên hệ quản trị viên.");
        }
    }

    public async Task<(bool Success, string Message, string ResponseCode)> ProcessCallbackAsync(
        VnpayCallbackResult callback,
        CancellationToken cancellationToken)
    {
        if (!callback.IsValidSignature || !callback.IsMerchantValid)
            return (false, "Chữ ký hoặc mã merchant không hợp lệ.", "97");

        var invoice = await db.TuitionInvoices.Find(x =>
            x.VnpayTransactionReference == callback.TransactionReference)
            .FirstOrDefaultAsync(cancellationToken);
        if (invoice is null)
            return (false, "Không tìm thấy khoản học phí.", "01");
        if (invoice.AmountVnd != callback.AmountVnd || callback.AmountVnd <= 0)
            return (false, "Số tiền thanh toán không khớp với hóa đơn.", "04");

        if (invoice.Status == TuitionInvoiceStatuses.Paid &&
            invoice.VnpayTransactionNumber == callback.TransactionNumber)
            return (true, "Khoản học phí đã được xác nhận thanh toán.", "00");
        if (invoice.Status == TuitionInvoiceStatuses.Paid)
            return (false, "Hóa đơn đã được xác nhận bằng một giao dịch khác.", "02");

        if (callback.ResponseCode != "00" || callback.TransactionStatus != "00")
            return (false, "Giao dịch chưa thanh toán thành công. Bạn có thể thử thanh toán lại.", "00");

        var paidAt = DateTime.UtcNow;
        var result = await db.TuitionInvoices.UpdateOneAsync(
            x => x.Id == invoice.Id &&
                x.Status == TuitionInvoiceStatuses.Pending &&
                x.VnpayTransactionReference == callback.TransactionReference &&
                x.AmountVnd == callback.AmountVnd,
            Builders<TuitionInvoice>.Update
                .Set(x => x.Status, TuitionInvoiceStatuses.Paid)
                .Set(x => x.VnpayTransactionNumber, callback.TransactionNumber)
                .Set(x => x.PaidAt, paidAt)
                .Set(x => x.UpdatedAt, paidAt),
            cancellationToken: cancellationToken);
        if (result.ModifiedCount == 1)
            return (true, "Thanh toán học phí thành công.", "00");

        var latest = await db.TuitionInvoices.Find(x => x.Id == invoice.Id).FirstOrDefaultAsync(cancellationToken);
        return latest is { Status: TuitionInvoiceStatuses.Paid } &&
               latest.VnpayTransactionNumber == callback.TransactionNumber
            ? (true, "Khoản học phí đã được xác nhận thanh toán.", "00")
            : (false, "Khoản học phí đã thay đổi trạng thái. Vui lòng làm mới trang.", "02");
    }

    private async Task<TuitionInvoice?> EnsureInvoiceAsync(
        MongoDB.Bson.ObjectId studentId,
        ClassRoom classroom,
        AcademicTerm term,
        int credits,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var amount = term.TuitionPerCredit > 0 && credits > 0
            ? decimal.ToInt64(decimal.Round(
                term.TuitionPerCredit * credits, 0, MidpointRounding.AwayFromZero))
            : 0;

        await db.TuitionInvoices.UpdateOneAsync(
            x => x.StudentId == studentId && x.ClassId == classroom.Id,
            Builders<TuitionInvoice>.Update
                .SetOnInsert(x => x.StudentId, studentId)
                .SetOnInsert(x => x.ClassId, classroom.Id)
                .SetOnInsert(x => x.AcademicTermId, term.Id)
                .SetOnInsert(x => x.Credits, credits)
                .SetOnInsert(x => x.TuitionPerCredit, term.TuitionPerCredit)
                .SetOnInsert(x => x.AmountVnd, amount)
                .SetOnInsert(x => x.Status, TuitionInvoiceStatuses.Pending)
                .SetOnInsert(x => x.CreatedAt, now)
                .SetOnInsert(x => x.UpdatedAt, now),
            new UpdateOptions { IsUpsert = true },
            cancellationToken);

        if (amount > 0)
        {
            await db.TuitionInvoices.UpdateOneAsync(
                x => x.StudentId == studentId &&
                    x.ClassId == classroom.Id &&
                    x.Status == TuitionInvoiceStatuses.Cancelled,
                Builders<TuitionInvoice>.Update
                    .Set(x => x.Status, TuitionInvoiceStatuses.Pending)
                    .Set(x => x.AcademicTermId, term.Id)
                    .Set(x => x.Credits, credits)
                    .Set(x => x.TuitionPerCredit, term.TuitionPerCredit)
                    .Set(x => x.AmountVnd, amount)
                    .Set(x => x.VnpayTransactionReference, "")
                    .Set(x => x.VnpayTransactionNumber, "")
                    .Set(x => x.PaymentInitiatedAt, (DateTime?)null)
                    .Set(x => x.PaidAt, (DateTime?)null)
                    .Set(x => x.UpdatedAt, now),
                cancellationToken: cancellationToken);

            await db.TuitionInvoices.UpdateOneAsync(
                x => x.StudentId == studentId &&
                    x.ClassId == classroom.Id &&
                    x.Status == TuitionInvoiceStatuses.Pending &&
                    x.AmountVnd <= 0,
                Builders<TuitionInvoice>.Update
                    .Set(x => x.AcademicTermId, term.Id)
                    .Set(x => x.Credits, credits)
                    .Set(x => x.TuitionPerCredit, term.TuitionPerCredit)
                    .Set(x => x.AmountVnd, amount)
                    .Set(x => x.UpdatedAt, now),
                cancellationToken: cancellationToken);
        }

        return await db.TuitionInvoices.Find(x =>
            x.StudentId == studentId && x.ClassId == classroom.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
