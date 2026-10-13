namespace LinguaLoop.Domain;

public sealed class Card
{
    public long Id { get; init; }
    public required string Prompt { get; init; }
    public string? Translation { get; init; }
    public IReadOnlyList<string> Synonyms { get; init; } = [];
    public required string PromptLanguage { get; init; }
    public DateTime NextReviewAt { get; init; }
    public int IntervalDays { get; init; }
    public double EaseFactor { get; init; }
    public int CorrectCount { get; init; }
    public int WrongCount { get; init; }

    public override string ToString()
    {
        var translation = string.IsNullOrWhiteSpace(Translation) ? "AI-guided" : Translation;
        return $"{Prompt}  →  {translation} ({PromptLanguage})";
    }
}

public sealed record LearningStats(int TotalCards, int DueCards, int CorrectAnswers, int IncorrectAnswers, int MasteredCards)
{
    public double Accuracy => CorrectAnswers + IncorrectAnswers == 0
        ? 0
        : (double)CorrectAnswers / (CorrectAnswers + IncorrectAnswers);
}

public sealed record GradingResult(bool IsCorrect, string Feedback, string IdealAnswer);

public sealed class GradingContextMismatchException(string message) : InvalidOperationException(message);

public sealed record PracticeExercise(
    string SourceText,
    string SourceLanguage,
    string TargetLanguage,
    string ReferenceTranslation,
    string LiteralTranslation,
    IReadOnlyList<WordHint> WordHints,
    string VocabularyMatchText,
    IReadOnlyList<string> Synonyms);

public sealed record WordHint(string SourceText, IReadOnlyList<string> Synonyms);

public enum ReviewDirection
{
    Standard,
    Reverse
}

public sealed class AppSettings
{
    public const string CloudflareProvider = "Cloudflare";
    public const string GeminiProvider = "Gemini";
    public const string DefaultCloudflareModel = "@cf/google/gemma-4-26b-a4b-it";
    public const string DefaultGeminiModel = "gemini-2.5-flash";

    // Retained so existing OpenAI settings files can still be read safely. It is no longer used.
    public string? ProtectedApiKey { get; set; }
    public string Provider { get; set; } = CloudflareProvider;
    public string? CloudflareAccountId { get; set; }
    public string? ProtectedCloudflareApiToken { get; set; }
    public string? ProtectedGeminiApiKey { get; set; }
    public string Model { get; set; } = DefaultCloudflareModel;
    public int QuizIntervalMinutes { get; set; } = 30;
}
