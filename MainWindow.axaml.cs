using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using LinguaLoop.Domain;
using LinguaLoop.Services;
using LinguaLoop.Commands;
using System.Text.RegularExpressions;

namespace LinguaLoop;

public partial class MainWindow : Window, IWindowCommandContext
{
    private readonly AppLogger _logger = new();
    private readonly VocabularyRepository _repository = new();
    private readonly SettingsStore _settingsStore = new();
    private readonly DispatcherTimer _quizTimer = new();
    private readonly PracticeReminderService _practiceReminder;
    private AppSettings _settings = new();
    private Card? _currentCard;
    private PracticeExercise? _currentExercise;
    private IPracticeCoach? _aiCoach;
    private string? _aiLoadError;
    private bool _useReverseDirection;
    private string _displayPromptText = string.Empty;
    private Card? _selectedVocabularyCard;
    private Task? _initializationTask;

    public MainWindow()
    {
        InitializeComponent();
        _practiceReminder = new PracticeReminderService(_logger);
        _practiceReminder.ActionSelected += PracticeReminder_ActionSelected;
        Action<string, Exception> onError = HandleCommandError;
        NextButton.Command = new NextPracticeCommand(this, _logger, onError);
        CheckAnswerButton.Command = new CheckAnswerCommand(this, _logger, onError);
        HintButton.Command = new HintCommand(this, _logger, onError);
        DirectionSwitch.Command = new ReverseDirectionCommand(this, _logger, onError);
        DoneButton.Command = new DoneCommand(this, _logger, onError);
        CopySentenceButton.Command = new CopySentenceCommand(this, _logger, onError);
        AddVocabularyButton.Command = new AddVocabularyCommand(this, _logger, onError);
        SaveVocabularyButton.Command = new SaveVocabularyCommand(this, _logger, onError);
        RemoveVocabularyButton.Command = new RemoveVocabularyCommand(this, _logger, onError);
        CancelVocabularyEditButton.Command = new CancelVocabularyEditCommand(this, _logger, onError);
        RefreshVocabularyButton.Command = new RefreshVocabularyCommand(this, _logger, onError);
        RefreshStatisticsButton.Command = new RefreshStatisticsCommand(this, _logger, onError);
        SaveSettingsButton.Command = new SaveSettingsCommand(this, _logger, onError);
        _logger.Info("Main window initialized; application commands attached.");
        Opened += async (_, _) =>
        {
            var startsInitialization = _initializationTask is null;
            if (startsInitialization)
            {
                _logger.Info("Main window opened; initializing application state.");
                _initializationTask = InitializeAsync();
            }
            else
            {
                _logger.Info("Main window reopened; reusing initialized application state.");
            }

            try
            {
                await _initializationTask!;
                if (startsInitialization)
                    _logger.Info("Application state initialized.");
            }
            catch (Exception exception)
            {
                _initializationTask = null;
                _logger.Error("Application initialization failed", exception);
                PracticeStatus.Text = "LinguaLoop could not initialize. See the local app log for details.";
            }
        };
        Closing += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            Hide();
        };
        _quizTimer.Tick += async (_, _) => await ShowScheduledQuizAsync();
    }

    Window IWindowCommandContext.Owner => this;
    VocabularyRepository IWindowCommandContext.Repository => _repository;
    SettingsStore IWindowCommandContext.SettingsStore => _settingsStore;
    AppSettings IWindowCommandContext.Settings => _settings;
    Card? IWindowCommandContext.CurrentCard { get => _currentCard; set => _currentCard = value; }
    PracticeExercise? IWindowCommandContext.CurrentExercise { get => _currentExercise; set => _currentExercise = value; }
    IPracticeCoach? IWindowCommandContext.Coach { get => _aiCoach; set => _aiCoach = value; }
    string? IWindowCommandContext.AiLoadError => _aiLoadError;
    bool IWindowCommandContext.IsReverseDirection { get => _useReverseDirection; set => _useReverseDirection = value; }
    string IWindowCommandContext.DisplayPromptTextValue => _displayPromptText;
    Card? IWindowCommandContext.SelectedVocabularyCard => _selectedVocabularyCard;
    TextBox IWindowCommandContext.AnswerBox => AnswerBox;
    TextBox IWindowCommandContext.NewPrompt => NewPrompt;
    TextBox IWindowCommandContext.NewTranslation => NewTranslation;
    TextBox IWindowCommandContext.ProviderTokenBox => ProviderTokenBox;
    TextBox IWindowCommandContext.CloudflareAccountIdBox => CloudflareAccountIdBox;
    TextBox IWindowCommandContext.ModelBox => ModelBox;
    TextBlock IWindowCommandContext.PracticeStatus => PracticeStatus;
    TextBlock IWindowCommandContext.FeedbackBox => FeedbackBox;
    TextBlock IWindowCommandContext.HintVocabularyText => HintVocabularyText;
    TextBlock IWindowCommandContext.HintLiteralText => HintLiteralText;
    TextBlock IWindowCommandContext.AddStatus => AddStatus;
    TextBlock IWindowCommandContext.SettingsStatus => SettingsStatus;
    ToggleSwitch IWindowCommandContext.DirectionSwitch => DirectionSwitch;
    ComboBox IWindowCommandContext.PromptLanguage => PromptLanguage;
    ComboBox IWindowCommandContext.ProviderBox => ProviderBox;
    NumericUpDown IWindowCommandContext.IntervalBox => IntervalBox;
    ListBox IWindowCommandContext.VocabularyList => VocabularyList;
    Border IWindowCommandContext.HintPanel => HintPanel;
    Task IWindowCommandContext.LoadNextCardAsync() => LoadNextCardAsync();
    Task IWindowCommandContext.RefreshVocabularyAsync() => RefreshVocabularyAsync();
    Task IWindowCommandContext.RefreshStatisticsAsync() => RefreshStatisticsAsync();
    void IWindowCommandContext.ResetVocabularyEditor() => ResetVocabularyEditor();
    void IWindowCommandContext.DisplayPromptText(string text) => DisplayPromptText(text);
    void IWindowCommandContext.UpdateExerciseDisplay() => UpdateExerciseDisplay();
    PracticeExercise? IWindowCommandContext.CurrentExerciseForDirection() => CurrentExerciseForDirection();
    void IWindowCommandContext.SetBusy(bool busy, string? message) => SetBusy(busy, message);
    void IWindowCommandContext.RestartQuizTimer() => RestartQuizTimer();
    string IWindowCommandContext.DefaultModelForSelectedProvider() => DefaultModelForSelectedProvider();
    string IWindowCommandContext.GetSettingsStatus() => GetSettingsStatus();

    public void ShowForPractice()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        MainTabs.SelectedIndex = 0;
        _ = EnsurePracticeCardAsync();
    }

    public void ShowForVocabulary()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        MainTabs.SelectedIndex = 1;
        _ = RefreshVocabularyAsync();
    }

    private async Task InitializeAsync()
    {
        await _repository.InitializeAsync();
        _settings = await _settingsStore.LoadAsync();
        NormalizeSettings();
        ProviderBox.SelectedIndex = string.Equals(_settings.Provider, AppSettings.GeminiProvider, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        ModelBox.Text = _settings.Model;
        CloudflareAccountIdBox.Text = _settings.CloudflareAccountId;
        IntervalBox.Value = _settings.QuizIntervalMinutes;
        _aiCoach = PracticeCoachFactory.Create(_settings);
        SettingsStatus.Text = GetSettingsStatus();
        RestartQuizTimer();
        await RefreshVocabularyAsync();
        await RefreshStatisticsAsync();
        await LoadNextCardAsync();
    }

    private async Task EnsurePracticeCardAsync()
    {
        if (_currentCard is null)
        {
            await LoadNextCardAsync();
            return;
        }

        // A card may have been selected by a previous operation without an AI
        // exercise being ready yet. Finish that work before exposing the card.
        if (_aiCoach is not null && _currentExercise is null && string.IsNullOrWhiteSpace(_aiLoadError))
            await LoadCardAsync(_currentCard);
    }
    private void PracticeReminder_ActionSelected(object? sender, PracticeReminderAction action)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                if (action == PracticeReminderAction.Answer)
                {
                    _logger.Info("Opening practice from the native notification.");
                    ShowForPractice();
                    return;
                }

                await SkipScheduledPracticeAsync();
            }
            catch (Exception exception)
            {
                _logger.Error("Native notification action failed", exception);
            }
        });
    }

    private async Task SkipScheduledPracticeAsync()
    {
        if (_currentCard is not { } card)
        {
            _logger.Info("Notification skip ignored because no practice card is active.");
            return;
        }

        _logger.Info($"Skipping practice card {card.Id} from the native notification.");
        await _repository.RecordReviewAsync(card, false, "Skipped from system notification.", ReviewDirection.Standard);
        await RefreshStatisticsAsync();
        await LoadRandomCardAsync(card.Id);
    }

    private async Task LoadNextCardAsync()
    {
        await LoadCardAsync(await _repository.GetNextDueAsync());
    }

    private async Task LoadRandomCardAsync(long excludedCardId)
    {
        await LoadCardAsync(await _repository.GetRandomCardAsync(excludedCardId));
    }

    private async Task LoadCardAsync(Card? card)
    {
        _currentCard = card;
        _currentExercise = null;
        _aiLoadError = null;
        _useReverseDirection = false;
        DirectionSwitch.IsChecked = false;
        HintPanel.IsVisible = false;
        AnswerBox.Text = string.Empty;
        FeedbackBox.Text = string.Empty;
        if (_currentCard is null)
        {
            PracticeStatus.Text = "No cards are due right now. Add a card or return when the tray reminds you.";
            DisplayPromptText("You are caught up.");
            return;
        }

        if (_aiCoach is not null)
        {
            try
            {
                PracticeStatus.Text = "Creating a natural example…";
                SetBusy(true, "Creating a natural example…");
                var recentExamples = await _repository.GetRecentExamplesAsync(_currentCard.Id);
                _currentExercise = await _aiCoach.CreateExerciseAsync(_currentCard, recentExamples);
                await _repository.SaveGeneratedExampleAsync(_currentCard.Id, _currentExercise.SourceText);
                await _repository.SaveLexicalMetadataAsync(
                    _currentCard,
                    _currentExercise.LiteralTranslation,
                    _currentExercise.Synonyms);
                UpdateExerciseDisplay();
                AnswerBox.Focus();
                return;
            }
            catch (Exception exception)
            {
                _logger.Error("Could not prepare the next practice exercise", exception);
                _aiLoadError = exception.Message;
                PracticeStatus.Text = $"The AI coach is unavailable. Using the saved answer instead. ({exception.Message})";
            }
            finally
            {
                SetBusy(false);
            }
        }

        DisplayPromptText(_currentCard.Prompt);
        PracticeStatus.Text = $"Translate from {_currentCard.PromptLanguage}. Add an AI provider in Settings for contextual examples.";
        AnswerBox.Focus();
    }

    private void UpdateExerciseDisplay()
    {
        var exercise = CurrentExerciseForDirection();
        if (exercise is null)
            return;

        DisplayPromptText(
            exercise.SourceText,
            _useReverseDirection ? Array.Empty<WordHint>() : exercise.WordHints,
            _useReverseDirection ? null : exercise.VocabularyMatchText);
        PracticeStatus.Text = $"Translate from {exercise.SourceLanguage} to {exercise.TargetLanguage}.";
    }

    private PracticeExercise? CurrentExerciseForDirection()
    {
        if (_currentExercise is null || !_useReverseDirection)
            return _currentExercise;

        return new PracticeExercise(
            _currentExercise.ReferenceTranslation,
            _currentExercise.TargetLanguage,
            _currentExercise.SourceLanguage,
            _currentExercise.SourceText,
            _currentExercise.LiteralTranslation,
            _currentExercise.WordHints,
            _currentExercise.VocabularyMatchText,
            _currentExercise.Synonyms);
    }

    private void DisplayPromptText(
        string text,
        IReadOnlyList<WordHint>? wordHints = null,
        string? vocabularyMatchText = null)
    {
        _displayPromptText = text;
        QuizPrompt.Children.Clear();
        var matchStart = string.IsNullOrWhiteSpace(vocabularyMatchText)
            ? -1
            : text.IndexOf(vocabularyMatchText, StringComparison.OrdinalIgnoreCase);
        var matchEnd = matchStart < 0 ? -1 : matchStart + vocabularyMatchText!.Length;
        var textOffset = 0;
        foreach (var fragment in Regex.Split(text, @"(\p{L}+(?:['’\-]\p{L}+)*)"))
        {
            if (fragment.Length == 0)
                continue;

            var fragmentStart = textOffset;
            textOffset += fragment.Length;
            var isWord = Regex.IsMatch(fragment, @"^\p{L}+(?:['’\-]\p{L}+)*$");
            var highlightsVocabulary = isWord && matchStart >= 0 &&
                fragmentStart < matchEnd && fragmentStart + fragment.Length > matchStart;
            var measuredText = new TextBlock { Text = fragment, FontSize = 24 };
            measuredText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Control textPart;
            if (isWord)
            {
                var wordText = new SelectableTextBlock
                {
                    Text = fragment,
                    FontSize = 24,
                    Padding = new Thickness(0),
                    Background = highlightsVocabulary
                        ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFE6A3"))
                        : Avalonia.Media.Brushes.Transparent,
                    SelectionBrush = Avalonia.Media.Brushes.DodgerBlue,
                    SelectionForegroundBrush = Avalonia.Media.Brushes.White
                };
                var copyItem = new MenuItem { Header = "Copy", Tag = wordText };
                copyItem.Click += CopyPromptWord_Click;
                var contextMenu = new ContextMenu();
                contextMenu.Items.Add(copyItem);
                wordText.ContextMenu = contextMenu;
                textPart = wordText;
            }
            else
            {
                textPart = new SelectableTextBlock { Text = fragment, FontSize = 24 };
            }

            var hint = wordHints?.FirstOrDefault(candidate =>
                string.Equals(candidate.SourceText, fragment, StringComparison.OrdinalIgnoreCase));
            var tooltip = hint is null
                ? "Double-click to select this word, then press Ctrl+C to copy it."
                : $"Synonyms for {hint.SourceText}: {string.Join(", ", hint.Synonyms)}";
            tooltip += "\nDouble-click to select, then press Ctrl+C to copy.";
            ToolTip.SetTip(textPart, tooltip);
            QuizPrompt.Children.Add(textPart);
        }
    }

    private async void CopyPromptWord_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: SelectableTextBlock wordText })
            return;

        var selectedText = string.IsNullOrWhiteSpace(wordText.SelectedText)
            ? wordText.Text
            : wordText.SelectedText;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null || string.IsNullOrWhiteSpace(selectedText))
            return;

        try
        {
            await clipboard.SetTextAsync(selectedText);
            PracticeStatus.Text = "Word copied to the clipboard.";
        }
        catch (Exception exception)
        {
            PracticeStatus.Text = $"Could not copy the word: {exception.Message}";
        }
    }

    private void SetBusy(bool isBusy, string? message = null)
    {
        BusyOverlay.IsVisible = isBusy;
        _logger.Info($"Busy overlay visibility set to {isBusy}.");
        if (message is not null)
            BusyMessage.Text = message;
    }

    private void HandleCommandError(string commandName, Exception exception)
    {
        PracticeStatus.Text = $"{commandName} failed. Details were written to the LinguaLoop log.";
        FeedbackBox.Text = exception.Message;
    }

    private void VocabularyList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (VocabularyList.SelectedItem is not Card card)
        {
            _selectedVocabularyCard = null;
            SaveVocabularyButton.IsEnabled = false;
            RemoveVocabularyButton.IsEnabled = false;
            CancelVocabularyEditButton.IsEnabled = false;
            return;
        }

        _selectedVocabularyCard = card;
        NewPrompt.Text = card.Prompt;
        NewTranslation.Text = card.Translation ?? string.Empty;
        PromptLanguage.SelectedIndex = string.Equals(card.PromptLanguage, "Ukrainian", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        SaveVocabularyButton.IsEnabled = true;
        RemoveVocabularyButton.IsEnabled = true;
        CancelVocabularyEditButton.IsEnabled = true;
        AddStatus.Text = "Editing selected item. Saving changes keeps its review progress.";
    }

    private void ResetVocabularyEditor()
    {
        _selectedVocabularyCard = null;
        VocabularyList.SelectedItem = null;
        NewPrompt.Text = string.Empty;
        NewTranslation.Text = string.Empty;
        PromptLanguage.SelectedIndex = 0;
        SaveVocabularyButton.IsEnabled = false;
        RemoveVocabularyButton.IsEnabled = false;
        CancelVocabularyEditButton.IsEnabled = false;
    }

    private void ProviderBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Avalonia raises this while parsing the XAML, before controls declared below
        // ProviderBox have been assigned their generated fields.
        if (ModelBox is null) return;

        var current = ModelBox.Text?.Trim();
        var switchingToGemini = ProviderBox.SelectedIndex == 1;
        var modelBelongsToOtherProvider = switchingToGemini
            ? current?.StartsWith("@cf/", StringComparison.OrdinalIgnoreCase) == true
            : current?.StartsWith("gemini", StringComparison.OrdinalIgnoreCase) == true;

        if (string.IsNullOrWhiteSpace(current) || modelBelongsToOtherProvider || current.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase))
            ModelBox.Text = DefaultModelForSelectedProvider();
    }

    private async Task RefreshVocabularyAsync() => VocabularyList.ItemsSource = await _repository.GetCardsAsync();

    private async Task RefreshStatisticsAsync()
    {
        var stats = await _repository.GetStatsAsync();
        StatsBox.Text = $"Cards: {stats.TotalCards}\nDue now: {stats.DueCards}\nAnswers: {stats.CorrectAnswers + stats.IncorrectAnswers}\nAccuracy: {stats.Accuracy:P0}\nMastered (21+ day interval): {stats.MasteredCards}";
    }

    private void RestartQuizTimer()
    {
        _quizTimer.Interval = TimeSpan.FromMinutes(Math.Max(5, _settings.QuizIntervalMinutes));
        _quizTimer.Start();
        _logger.Info($"Practice timer started with a {_quizTimer.Interval.TotalMinutes:0}-minute interval.");
    }

    private async Task ShowScheduledQuizAsync()
    {
        try
        {
            _logger.Info("Practice timer tick received; preparing the exercise before showing a notification.");
            await EnsurePracticeCardAsync();

            if (_currentCard is null)
            {
                _logger.Info("No due card was found at the scheduled timer tick.");
                return;
            }

            _practiceReminder.Show();
        }
        catch (Exception exception)
        {
            _logger.Error("Scheduled practice preparation failed", exception);
        }
    }
    private void NormalizeSettings()
    {
        if (!string.Equals(_settings.Provider, AppSettings.GeminiProvider, StringComparison.OrdinalIgnoreCase))
            _settings.Provider = AppSettings.CloudflareProvider;

        if (string.IsNullOrWhiteSpace(_settings.Model) || _settings.Model.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase))
            _settings.Model = string.Equals(_settings.Provider, AppSettings.GeminiProvider, StringComparison.OrdinalIgnoreCase)
                ? AppSettings.DefaultGeminiModel
                : AppSettings.DefaultCloudflareModel;
    }

    private string DefaultModelForSelectedProvider() => ProviderBox.SelectedIndex == 1
        ? AppSettings.DefaultGeminiModel
        : AppSettings.DefaultCloudflareModel;

    private string GetSettingsStatus()
    {
        if (_aiCoach is not null)
            return $"{_settings.Provider} is ready. Its credential is encrypted with Windows DPAPI for this Windows account.";

        return string.Equals(_settings.Provider, AppSettings.GeminiProvider, StringComparison.OrdinalIgnoreCase)
            ? "Add a Gemini API key to enable contextual examples and AI assessment. Exact matching remains available without it."
            : "Add a Cloudflare Account ID and Workers AI API token to enable Gemma 4 examples and AI assessment. Exact matching remains available without them.";
    }
}
