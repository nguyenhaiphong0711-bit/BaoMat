namespace LMS.Services;

public sealed class EmailSettings
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string AppPassword { get; set; } = "";
    public string SenderEmail { get; set; } = "";
    public string SenderName { get; set; } = "LearnSpace";
}
