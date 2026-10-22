using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LMS.Data;
using LMS.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LMS.Services;

public sealed class AiSettings
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class AiQuestionDraftService(IHttpClientFactory clients, IOptions<AiSettings> options,
    MongoContext db, QuestionBankService questions)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<QuestionBankItem> CreateDraftAsync(User user, AiQuestionDraftRequest request, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.Model) ||
            !Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Trợ lý AI chưa được cấu hình.");
        var allowed = await questions.AuthorSubjectsAsync(user);
        if (!ObjectId.TryParse(request.SubjectId, out var subjectId) || allowed.FirstOrDefault(x => x.Id == subjectId) is not { } subject)
            throw new UnauthorizedAccessException();

        var prompt = $"Môn: {subject.Name}. Chủ đề: {request.Topic}. Độ khó: {request.Difficulty}. Mục tiêu: {request.LearningObjective}.";
        var payload = new { model = settings.Model, temperature = 0.3, response_format = new { type = "json_object" }, messages = new object[] {
            new { role = "system", content = "Tạo đúng một câu hỏi trắc nghiệm tiếng Việt. Chỉ trả JSON gồm content, options (đúng 4 chuỗi), correctOption (0-3), explanation. Không đưa dữ liệu cá nhân, không tự phê duyệt hay công bố." },
            new { role = "user", content = prompt }
        }};
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json") };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 5, 120)));
        using var response = await clients.CreateClient(nameof(AiQuestionDraftService)).SendAsync(message, timeout.Token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Nhà cung cấp AI chưa thể tạo câu hỏi.");
        AiDraft? draft;
        try
        {
            using var outer = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            if (!outer.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0 ||
                !choices[0].TryGetProperty("message", out var messageBody) ||
                !messageBody.TryGetProperty("content", out var contentBody))
                throw new JsonException();
            var content = (contentBody.GetString() ?? "").Trim();
            if (content.StartsWith("```"))
            {
                var firstLine = content.IndexOf('\n'); var closing = content.LastIndexOf("```", StringComparison.Ordinal);
                if (firstLine < 0 || closing <= firstLine) throw new JsonException();
                content = content[(firstLine + 1)..closing].Trim();
            }
            draft = JsonSerializer.Deserialize<AiDraft>(content, Json);
        }
        catch (JsonException) { throw new InvalidOperationException("Phản hồi AI không đúng cấu trúc câu hỏi."); }
        if (draft is null || draft.Options is not { Length: 4 } || draft.Options.Any(string.IsNullOrWhiteSpace) ||
            draft.Options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4 || draft.CorrectOption is < 0 or > 3 ||
            string.IsNullOrWhiteSpace(draft.Content) || draft.Content.Length > 5000 || (draft.Explanation?.Length ?? 0) > 5000)
            throw new InvalidOperationException("Phản hồi AI không đáp ứng quy tắc ngân hàng câu hỏi.");
        var question = await questions.CreateAsync(user, new QuestionRequest { SubjectId = request.SubjectId,
            Topic = request.Topic, Difficulty = request.Difficulty, Content = draft.Content,
            Options = draft.Options, CorrectOption = draft.CorrectOption, Explanation = draft.Explanation ?? "" });
        question.AiAssisted = true; question.AiModel = settings.Model; question.AiGeneratedAt = DateTime.UtcNow;
        await db.Questions.ReplaceOneAsync(x => x.Id == question.Id, question);
        return question;
    }

    private sealed record AiDraft(string? Content, string[]? Options, int CorrectOption, string? Explanation);
}
