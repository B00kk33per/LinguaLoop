using LinguaLoop.Domain;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace LinguaLoop.Services;

public sealed class VocabularyRepository
{
    private readonly string _connectionString;

    public VocabularyRepository()
    {
        var dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LinguaLoop");
        Directory.CreateDirectory(dataFolder);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(dataFolder, "lingualoop.db") }.ToString();
    }

    public async Task InitializeAsync()
    {
        await using var connection = await OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS cards (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                prompt TEXT NOT NULL,
                translation TEXT NULL,
                synonyms_json TEXT NULL,
                prompt_language TEXT NOT NULL,
                next_review_at TEXT NOT NULL,
                interval_days INTEGER NOT NULL DEFAULT 0,
                ease_factor REAL NOT NULL DEFAULT 2.5,
                correct_count INTEGER NOT NULL DEFAULT 0,
                wrong_count INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS review_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                card_id INTEGER NOT NULL,
                reviewed_at TEXT NOT NULL,
                is_correct INTEGER NOT NULL,
                direction TEXT NOT NULL DEFAULT 'standard',
                feedback TEXT NULL,
                FOREIGN KEY(card_id) REFERENCES cards(id)
            );
            CREATE TABLE IF NOT EXISTS generated_examples (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                card_id INTEGER NOT NULL,
                generated_at TEXT NOT NULL,
                source_text TEXT NOT NULL,
                FOREIGN KEY(card_id) REFERENCES cards(id)
            );
            """;
        await command.ExecuteNonQueryAsync();

        var columnCheck = connection.CreateCommand();
        columnCheck.CommandText = "PRAGMA table_info(review_events);";
        var hasDirectionColumn = false;
        await using (var columns = await columnCheck.ExecuteReaderAsync())
        {
            while (await columns.ReadAsync())
                hasDirectionColumn |= string.Equals(columns.GetString(1), "direction", StringComparison.OrdinalIgnoreCase);
        }

        if (!hasDirectionColumn)
        {
            var migration = connection.CreateCommand();
            migration.CommandText = "ALTER TABLE review_events ADD COLUMN direction TEXT NOT NULL DEFAULT 'standard';";
            await migration.ExecuteNonQueryAsync();
        }

        var cardColumnCheck = connection.CreateCommand();
        cardColumnCheck.CommandText = "PRAGMA table_info(cards);";
        var hasSynonymsColumn = false;
        await using (var columns = await cardColumnCheck.ExecuteReaderAsync())
        {
            while (await columns.ReadAsync())
                hasSynonymsColumn |= string.Equals(columns.GetString(1), "synonyms_json", StringComparison.OrdinalIgnoreCase);
        }

        if (!hasSynonymsColumn)
        {
            var migration = connection.CreateCommand();
            migration.CommandText = "ALTER TABLE cards ADD COLUMN synonyms_json TEXT NULL;";
            await migration.ExecuteNonQueryAsync();
        }
    }

    public async Task AddAsync(string prompt, string? translation, string promptLanguage)
    {
        await using var connection = await OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO cards (prompt, translation, prompt_language, next_review_at) VALUES ($prompt, $translation, $language, $due);";
        command.Parameters.AddWithValue("$prompt", prompt.Trim());
        command.Parameters.AddWithValue("$translation", string.IsNullOrWhiteSpace(translation) ? DBNull.Value : translation.Trim());
        command.Parameters.AddWithValue("$language", promptLanguage);
        command.Parameters.AddWithValue("$due", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(long id, string prompt, string? translation, string promptLanguage)
    {
        await using var connection = await OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE cards SET prompt = $prompt, translation = $translation, synonyms_json = NULL, prompt_language = $language WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$prompt", prompt.Trim());
        command.Parameters.AddWithValue("$translation", string.IsNullOrWhiteSpace(translation) ? DBNull.Value : translation.Trim());
        command.Parameters.AddWithValue("$language", promptLanguage);
        await command.ExecuteNonQueryAsync();
    }

    public async Task RemoveAsync(long id)
    {
        await using var connection = await OpenAsync();
        using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$id", id);

        command.CommandText = "DELETE FROM review_events WHERE card_id = $id;";
        await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM generated_examples WHERE card_id = $id;";
        await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM cards WHERE id = $id;";
        await command.ExecuteNonQueryAsync();
        transaction.Commit();
    }

    public async Task SaveLexicalMetadataAsync(Card card, string literalTranslation, IReadOnlyList<string> synonyms)
    {
        await using var connection = await OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE cards
            SET translation = CASE
                    WHEN translation IS NULL OR TRIM(translation) = '' OR LOWER(TRIM(translation)) = LOWER(TRIM($prompt))
                    THEN $translation
                    ELSE translation
                END,
                synonyms_json = $synonyms
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$translation", literalTranslation.Trim());
        command.Parameters.AddWithValue("$synonyms", JsonSerializer.Serialize(synonyms));
        command.Parameters.AddWithValue("$id", card.Id);
        command.Parameters.AddWithValue("$prompt", card.Prompt);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<string>> GetRecentExamplesAsync(long cardId, int count = 5)
    {
        await using var connection = await OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT source_text FROM generated_examples WHERE card_id = $id ORDER BY id DESC LIMIT $count;";
        command.Parameters.AddWithValue("$id", cardId);
        command.Parameters.AddWithValue("$count", count);
        var examples = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            examples.Add(reader.GetString(0));
        return examples;
    }

    public async Task SaveGeneratedExampleAsync(long cardId, string sourceText, int keepCount = 5)
    {
        await using var connection = await OpenAsync();
        await using var transaction = connection.BeginTransaction();
        var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = "INSERT INTO generated_examples (card_id, generated_at, source_text) VALUES ($id, $generated, $source);";
        insert.Parameters.AddWithValue("$id", cardId);
        insert.Parameters.AddWithValue("$generated", DateTime.UtcNow.ToString("O"));
        insert.Parameters.AddWithValue("$source", sourceText.Trim());
        await insert.ExecuteNonQueryAsync();

        var prune = connection.CreateCommand();
        prune.Transaction = transaction;
        prune.CommandText = """
            DELETE FROM generated_examples
            WHERE card_id = $id AND id NOT IN (
                SELECT id FROM generated_examples WHERE card_id = $id ORDER BY id DESC LIMIT $keep
            );
            """;
        prune.Parameters.AddWithValue("$id", cardId);
        prune.Parameters.AddWithValue("$keep", keepCount);
        await prune.ExecuteNonQueryAsync();
        transaction.Commit();
    }

    public async Task RemoveByExactPromptAsync(string prompt)
    {
        await using var connection = await OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM cards WHERE prompt = $prompt;";
        command.Parameters.AddWithValue("$prompt", prompt);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<Card?> GetNextDueAsync()
    {
        await using var connection = await OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, prompt, translation, prompt_language, next_review_at, interval_days, ease_factor, correct_count, wrong_count, synonyms_json
            FROM cards
            WHERE next_review_at <= $now
            ORDER BY CASE WHEN interval_days = 0 THEN 0 ELSE 1 END, next_review_at ASC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadCard(reader) : null;
    }

    public async Task<Card?> GetRandomCardAsync(long? excludeCardId = null)
    {
        await using var connection = await OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = excludeCardId is null
            ? "SELECT id, prompt, translation, prompt_language, next_review_at, interval_days, ease_factor, correct_count, wrong_count, synonyms_json FROM cards ORDER BY RANDOM() LIMIT 1;"
            : "SELECT id, prompt, translation, prompt_language, next_review_at, interval_days, ease_factor, correct_count, wrong_count, synonyms_json FROM cards WHERE id <> $excludedId ORDER BY RANDOM() LIMIT 1;";
        if (excludeCardId is not null)
            command.Parameters.AddWithValue("$excludedId", excludeCardId.Value);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadCard(reader) : null;
    }

    public async Task<IReadOnlyList<Card>> GetCardsAsync()
    {
        var cards = new List<Card>();
        await using var connection = await OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT id, prompt, translation, prompt_language, next_review_at, interval_days, ease_factor, correct_count, wrong_count, synonyms_json FROM cards ORDER BY next_review_at ASC, prompt COLLATE NOCASE;";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) cards.Add(ReadCard(reader));
        return cards;
    }

    public async Task RecordReviewAsync(
        Card card,
        bool isCorrect,
        string feedback,
        ReviewDirection direction = ReviewDirection.Standard)
    {
        var now = DateTime.UtcNow;
        var isReverse = direction == ReviewDirection.Reverse;
        var nextInterval = isReverse
            ? card.IntervalDays
            : isCorrect
                ? card.IntervalDays switch { <= 0 => 1, 1 => 3, _ => Math.Max(card.IntervalDays + 1, (int)Math.Round(card.IntervalDays * card.EaseFactor)) }
                : 0;
        var nextEase = isReverse
            ? isCorrect ? Math.Min(3.0, card.EaseFactor + 0.02) : Math.Max(1.3, card.EaseFactor - 0.05)
            : isCorrect ? Math.Min(3.0, card.EaseFactor + 0.08) : Math.Max(1.3, card.EaseFactor - 0.2);
        var nextReview = isReverse
            ? now.AddMinutes(isCorrect ? 60 : 20)
            : isCorrect ? now.AddDays(nextInterval) : now.AddMinutes(10);

        await using var connection = await OpenAsync();
        await using var transaction = connection.BeginTransaction();
        var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE cards SET next_review_at = $nextReview, interval_days = $interval, ease_factor = $ease,
              correct_count = correct_count + $correct, wrong_count = wrong_count + $wrong
            WHERE id = $id;
            """;
        update.Parameters.AddWithValue("$nextReview", nextReview.ToString("O"));
        update.Parameters.AddWithValue("$interval", nextInterval);
        update.Parameters.AddWithValue("$ease", nextEase);
        update.Parameters.AddWithValue("$correct", !isReverse && isCorrect ? 1 : 0);
        update.Parameters.AddWithValue("$wrong", !isReverse && !isCorrect ? 1 : 0);
        update.Parameters.AddWithValue("$id", card.Id);
        await update.ExecuteNonQueryAsync();

        var history = connection.CreateCommand();
        history.Transaction = transaction;
        history.CommandText = "INSERT INTO review_events (card_id, reviewed_at, is_correct, direction, feedback) VALUES ($card, $reviewed, $correct, $direction, $feedback);";
        history.Parameters.AddWithValue("$card", card.Id);
        history.Parameters.AddWithValue("$reviewed", now.ToString("O"));
        history.Parameters.AddWithValue("$correct", isCorrect ? 1 : 0);
        history.Parameters.AddWithValue("$direction", isReverse ? "reverse" : "standard");
        history.Parameters.AddWithValue("$feedback", feedback);
        await history.ExecuteNonQueryAsync();
        transaction.Commit();
    }

    public async Task<LearningStats> GetStatsAsync()
    {
        await using var connection = await OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*), COALESCE(SUM(CASE WHEN next_review_at <= $now THEN 1 ELSE 0 END), 0),
              COALESCE(SUM(correct_count), 0), COALESCE(SUM(wrong_count), 0),
              COALESCE(SUM(CASE WHEN interval_days >= 21 THEN 1 ELSE 0 END), 0)
            FROM cards;
            """;
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new LearningStats(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4));
    }

    private async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static Card ReadCard(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0), Prompt = reader.GetString(1), Translation = reader.IsDBNull(2) ? null : reader.GetString(2),
        PromptLanguage = reader.GetString(3), NextReviewAt = DateTime.Parse(reader.GetString(4)), IntervalDays = reader.GetInt32(5),
        EaseFactor = reader.GetDouble(6), CorrectCount = reader.GetInt32(7), WrongCount = reader.GetInt32(8),
        Synonyms = ReadSynonyms(reader)
    };

    private static IReadOnlyList<string> ReadSynonyms(SqliteDataReader reader)
    {
        if (reader.FieldCount <= 9 || reader.IsDBNull(9)) return [];

        try
        {
            return JsonSerializer.Deserialize<string[]>(reader.GetString(9)) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
