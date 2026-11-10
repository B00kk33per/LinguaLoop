using LinguaLoop.Domain;

namespace LinguaLoop.Services;

public static class LocalCoach
{
    public static GradingResult Grade(Card card, string answer)
    {
        if (string.IsNullOrWhiteSpace(card.Translation))
            return new GradingResult(false, "Add a provider credential to receive AI feedback for cards without a saved translation.", "No fixed answer saved yet.");

        static string Normalize(string text) => string.Concat(text.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit));

        var expectedAnswers = new[] { card.Translation }
            .Concat(card.Synonyms)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(Normalize)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var correct = expectedAnswers.Contains(Normalize(answer), StringComparer.Ordinal);
        return correct
            ? new GradingResult(true, "Correct. Add an AI provider to receive contextual examples and richer feedback.", card.Translation)
            : new GradingResult(false, $"Not quite. A saved answer is: {card.Translation}", card.Translation);
    }
}
