// One-time operational migration. Run with mongosh while connected to the LMS database.
// This file is not called by application startup and is not a demo seeder.
const databaseName = db.getName();
if (["admin", "config", "local", "test"].includes(databaseName))
  throw new Error(`Refusing to write to database '${databaseName}'. Connect to the LMS database explicitly.`);

const now = new Date();
const opensAt = new Date(now.getTime() - 60 * 60 * 1000);
const closesAt = new Date(now.getTime() + 30 * 24 * 60 * 60 * 1000);
const admin = db.Users.findOne({ Role: "Admin", IsActive: true });
if (!admin) throw new Error("No active Admin exists for review provenance.");

const classes = db.Classes.find({ IsArchived: { $ne: true }, TeacherId: { $type: "objectId" },
  SubjectId: { $type: "objectId" } }).sort({ CreatedAt: 1 }).toArray();
let createdQuizzes = 0, createdQuestions = 0;

for (const classroom of classes) {
  if (createdQuizzes >= 5) break;
  if (db.Quizzes.countDocuments({ ClassId: classroom._id, IsArchived: { $ne: true } }) > 0) continue;
  const teacher = db.Users.findOne({ _id: classroom.TeacherId, Role: "Teacher", IsActive: true });
  const subject = db.Subjects.findOne({ _id: classroom.SubjectId, IsArchived: { $ne: true } });
  if (!teacher || !subject) continue;

  const templates = [
    [`Mục tiêu phù hợp nhất khi học ${subject.Name} là gì?`,
      ["Hiểu và vận dụng kiến thức vào tình huống thực tế", "Chỉ ghi nhớ thuật ngữ mà không cần hiểu",
       "Bỏ qua dữ liệu và bằng chứng liên quan", "Dùng một kết luận cho mọi trường hợp"],
      "Học phần hướng tới hiểu bản chất và vận dụng kiến thức trong bối cảnh thực tế."],
    [`Khi phân tích một tình huống trong ${subject.Name}, bước nào nên thực hiện trước?`,
      ["Xác định vấn đề và thu thập dữ liệu liên quan", "Chọn kết luận trước khi xem dữ liệu",
       "Bỏ qua giả định của bài toán", "Sao chép kết quả từ tình huống khác"],
      "Xác định đúng vấn đề và dữ liệu là nền tảng cho phân tích có căn cứ."],
    [`Cách đánh giá kết quả học tập ${subject.Name} nào đáng tin cậy nhất?`,
      ["Đối chiếu lập luận với dữ liệu và mục tiêu học tập", "Chỉ dựa vào độ dài câu trả lời",
       "Không cần kiểm tra nguồn thông tin", "Luôn chọn phương án xuất hiện đầu tiên"],
      "Kết quả cần được đánh giá dựa trên mục tiêu, lập luận và bằng chứng liên quan."]
  ];
  const questionIds = [];
  for (let index = 0; index < templates.length; index++) {
    const [content, choices, explanation] = templates[index];
    const questionId = new ObjectId();
    const options = choices.map((text, optionIndex) => ({ Id: String.fromCharCode(65 + optionIndex), Text: text }));
    const version = { Number: 1, Content: content, Topic: "Kiến thức nền tảng",
      Difficulty: index === 0 ? "Easy" : "Medium", Options: options, CorrectOption: 0,
      Explanation: explanation, EditorId: teacher._id, EditorName: teacher.FullName, CreatedAt: now };
    db.Questions.insertOne({ _id: questionId, OwnerId: teacher._id, SubjectId: subject._id,
      Topic: "Kiến thức nền tảng", Difficulty: version.Difficulty, Content: content, Options: options,
      CorrectOption: 0, Explanation: explanation, State: "Published", Revision: 1, IsArchived: false,
      EditorId: teacher._id, EditorName: teacher.FullName, ReviewerId: admin._id,
      ReviewerName: admin.FullName, ReviewComment: "Đã đối chiếu cấu trúc và phạm vi môn học.",
      CreatedAt: now, UpdatedAt: now, ReviewedAt: now, AiAssisted: false, AiModel: "",
      CurrentVersion: 1, Versions: [version] });
    questionIds.push(questionId); createdQuestions++;
  }
  const quizId = new ObjectId();
  db.Quizzes.insertOne({ _id: quizId, ClassId: classroom._id, OwnerId: teacher._id,
    Title: `Ôn tập ${subject.Name}`, Instructions: "Chọn phương án phù hợp nhất.", DurationMinutes: 20,
    OpensAt: opensAt, ClosesAt: closesAt, QuestionIds: questionIds, IsPublished: true,
    IsArchived: false, Revision: 1, CreatedAt: now, UpdatedAt: now });
  db.LearningEvents.insertOne({ Type: "quiz.created", ActorId: admin._id, ActorRole: "Admin",
    DepartmentId: subject.DepartmentId ?? null, ClassId: classroom._id, EntityType: "Quiz",
    EntityId: quizId, Summary: `Đã bổ sung quiz minh họa cho ${subject.Name}`,
    EventKey: `atlas-fill-quiz:${quizId}`, OccurredAt: now });
  createdQuizzes++; print(`CREATED ${quizId} | ${classroom.Name} | ${subject.Name}`);
}
printjson({ database: databaseName, createdQuizzes, createdQuestions,
  totalActiveQuizzes: db.Quizzes.countDocuments({ IsArchived: { $ne: true } }) });
