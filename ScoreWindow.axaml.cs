using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Data.Sqlite;
using MuaythaiApp.Database;
using MuaythaiApp.Security;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MuaythaiApp;

public partial class ScoreWindow : Window
{
    private Match? CurrentMatch;
    private int selectedJudgeId = 1;
    private bool isLoadingJudgeScores;
    private bool isUpdatingMatchContext;
    private string selectedManualWinner = "Red";
    private readonly DispatcherTimer countdownTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private TimerSession timerSession = TimerSession.CreateDefault();

    public ScoreWindow()
    {
        InitializeComponent();
        SetupCommonBehavior();

        if (!AppSession.IsAdmin)
            Opened += (_, __) => Close();
    }

    public ScoreWindow(Match m)
    {
        InitializeComponent();
        SetupCommonBehavior();

        if (!AppSession.IsAdmin)
        {
            Opened += (_, __) => Close();
            return;
        }

        CurrentMatch = m;

        SetupWindow(resetSelections: true);
    }

    private void SetupCommonBehavior()
    {
        countdownTimer.Tick += CountdownTimerTick;
        AddHandler(KeyDownEvent, WindowKeyDown, RoutingStrategies.Tunnel);
        Closed += (_, __) => countdownTimer.Stop();
    }

    private void SetupWindow(bool resetSelections)
    {
        if (CurrentMatch == null)
            return;

        isUpdatingMatchContext = true;

        try
        {
            BoutText.Text = CurrentMatch.OrderNo.ToString();
            CategoryText.Text = CurrentMatch.AgeCategory;
            WeightText.Text = CurrentMatch.WeightCategory;
            RoundsText.Text = "3";
            RedNameText.Text = CurrentMatch.Fighter1Name;
            BlueNameText.Text = CurrentMatch.Fighter2Name;

            JudgesCountCombo.ItemsSource ??= new List<int> { 3, 5 };
            JudgesCountCombo.SelectedItem = CurrentMatch.JudgesCount is 3 or 5
                ? CurrentMatch.JudgesCount
                : 5;

            ResultMethodCombo.ItemsSource ??= new List<string>
            {
                "W.P",
                "W.O",
                "Disq",
                "Ret",
                "No Contest",
                "K.O",
                "R.S.C"
            };
            ResultRoundCombo.ItemsSource ??= new List<string> { "1", "2", "3" };

            if (resetSelections)
            {
                selectedJudgeId = 1;
                ResultMethodCombo.SelectedItem = "W.P";
                selectedManualWinner = "Red";
                ResultRoundCombo.SelectedItem = "3";
                StyleTieBreakBox.IsChecked = false;
                DefenseTieBreakBox.IsChecked = false;
                OtherTieBreakBox.IsChecked = false;
            }

            UpdateResultControls();
            LoadTimerSettings(resetSelections);
            LoadJudgeNumbers();
            UpdateOfficialsVisibility();
            LoadScoresForSelectedJudge();
            RecalculateTotals();
        }
        finally
        {
            isUpdatingMatchContext = false;
        }
    }

    private void LoadTimerSettings(bool resetState)
    {
        if (CurrentMatch == null)
            return;

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT
            RoundCount,
            RoundDurationSeconds,
            BreakDurationSeconds
        FROM Categories
        WHERE Division = @division
          AND Gender = @gender
          AND CategoryName = @categoryName
        ORDER BY Id
        LIMIT 1
        ";
        command.Parameters.AddWithValue("@division", CurrentMatch.AgeCategory);
        command.Parameters.AddWithValue("@gender", CurrentMatch.Gender);
        command.Parameters.AddWithValue("@categoryName", CurrentMatch.WeightCategory);

        using var reader = command.ExecuteReader();

        if (reader.Read())
        {
            var roundCount = reader.IsDBNull(0) ? 3 : reader.GetInt32(0);
            var roundDuration = reader.IsDBNull(1) ? 120 : reader.GetInt32(1);
            var breakDuration = reader.IsDBNull(2) ? 60 : reader.GetInt32(2);

            timerSession = resetState
                ? new TimerSession(roundCount, roundDuration, breakDuration)
                : timerSession.WithDurations(roundCount, roundDuration, breakDuration);
        }
        else if (resetState)
        {
            timerSession = TimerSession.CreateDefault();
        }

        countdownTimer.Stop();
        UpdateTimerUi();
    }

    private void JudgesCountChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (isUpdatingMatchContext || CurrentMatch == null || JudgesCountCombo.SelectedItem == null)
            return;

        var judgesCount = Convert.ToInt32(JudgesCountCombo.SelectedItem);

        if (CurrentMatch.JudgesCount == judgesCount)
            return;

        using var c = DatabaseHelper.CreateConnection();
        c.Open();

        var cmd = c.CreateCommand();
        cmd.CommandText =
        @"
        UPDATE Matches
        SET JudgesCount = @judgesCount
        WHERE Id = @id
        ";
        cmd.Parameters.AddWithValue("@judgesCount", judgesCount);
        cmd.Parameters.AddWithValue("@id", CurrentMatch.Id);
        cmd.ExecuteNonQuery();

        CurrentMatch.JudgesCount = judgesCount;
        LoadJudgeNumbers();
        UpdateOfficialsVisibility();
        LoadScoresForSelectedJudge();
        RecalculateTotals();
    }

    private void JudgeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (isLoadingJudgeScores)
            return;

        selectedJudgeId = GetSelectedJudgeId();
        LoadScoresForSelectedJudge();
        RecalculateTotals();
    }

    private void RoundInputChanged(object? sender, RoutedEventArgs e)
    {
        if (isLoadingJudgeScores)
            return;

        RecalculateTotals();
    }

    private void SaveScoreClick(object? sender, RoutedEventArgs e)
    {
        if (CurrentMatch == null)
            return;

        var judgeId = GetSelectedJudgeId();

        if (judgeId <= 0)
            return;

        var rounds = BuildRoundScores();

        if (rounds.Any(x => x.HasAnyInput && !x.HasScore))
        {
            WinnerText.Text = "Enter both red and blue points for the round before saving.";
            return;
        }

        var completedRounds = rounds
            .Where(x => x.HasScore)
            .ToList();

        if (completedRounds.Count == 0)
        {
            WinnerText.Text = "Enter at least one round score before saving.";
            return;
        }

        using var c = DatabaseHelper.CreateConnection();
        c.Open();

        foreach (var round in completedRounds)
        {
            var delete = c.CreateCommand();
            delete.CommandText =
            @"
            DELETE FROM JudgeScores
            WHERE MatchId = @matchId
              AND JudgeId = @judgeId
              AND RoundNo = @roundNo
            ";
            delete.Parameters.AddWithValue("@matchId", CurrentMatch.Id);
            delete.Parameters.AddWithValue("@judgeId", judgeId);
            delete.Parameters.AddWithValue("@roundNo", round.RoundNo);
            delete.ExecuteNonQuery();

            var insert = c.CreateCommand();
            insert.CommandText =
            @"
            INSERT INTO JudgeScores
            (
                MatchId,
                RoundNo,
                JudgeId,
                RedPoints,
                BluePoints,
                RedWarning,
                BlueWarning
            )
            VALUES
            (
                @matchId,
                @roundNo,
                @judgeId,
                @redPoints,
                @bluePoints,
                @redWarning,
                @blueWarning
            )
            ";

            insert.Parameters.AddWithValue("@matchId", CurrentMatch.Id);
            insert.Parameters.AddWithValue("@roundNo", round.RoundNo);
            insert.Parameters.AddWithValue("@judgeId", judgeId);
            insert.Parameters.AddWithValue("@redPoints", round.RedPoints);
            insert.Parameters.AddWithValue("@bluePoints", round.BluePoints);
            insert.Parameters.AddWithValue("@redWarning", round.RedWarning ? 1 : 0);
            insert.Parameters.AddWithValue("@blueWarning", round.BlueWarning ? 1 : 0);
            insert.ExecuteNonQuery();
        }

        var autoSaved = TrySaveComputedResult(c);
        WinnerText.Text = autoSaved
            ? "Scores saved and match result updated."
            : $"Judge {judgeId} scores saved.";
    }

    private void LoadJudgeNumbers()
    {
        if (CurrentMatch == null)
            return;

        var judgeNumbers = Enumerable.Range(1, CurrentMatch.JudgesCount <= 0 ? 3 : CurrentMatch.JudgesCount)
            .Cast<object>()
            .ToList();
        var judgeToSelect = selectedJudgeId;
        if (judgeToSelect <= 0 || judgeToSelect > judgeNumbers.Count)
            judgeToSelect = 1;

        isLoadingJudgeScores = true;
        JudgeCombo.ItemsSource = judgeNumbers;
        JudgeCombo.SelectedItem = judgeNumbers.FirstOrDefault(x => Convert.ToInt32(x) == judgeToSelect);
        isLoadingJudgeScores = false;
        selectedJudgeId = judgeToSelect;
    }

    private void ShowWinnerClick(object? sender, RoutedEventArgs e)
    {
        if (CurrentMatch == null)
            return;

        using var c = DatabaseHelper.CreateConnection();
        c.Open();

        if (!string.Equals(ResultMethodCombo.SelectedItem?.ToString(), "W.P", StringComparison.OrdinalIgnoreCase))
        {
            if (!SaveManualResult(c))
                WinnerText.Text = "Choose Red or Blue to save the result.";

            return;
        }

        if (!SaveOrUpdateResult(c))
        {
            if (!SaveOrUpdateResult(c, allowPartialJudges: true))
                WinnerText.Text = "Winner can be calculated after at least one judge finishes 3 rounds.";
        }
    }

    private void NextFightClick(object? sender, RoutedEventArgs e)
    {
        if (CurrentMatch == null)
            return;

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT
            Id,
            OrderNo,
            Fighter1Name,
            Fighter2Name,
            AgeCategory,
            WeightCategory,
            Gender,
            JudgesCount,
            DayNumber
        FROM Matches
        WHERE DayNumber = @dayNumber
          AND
          (
              OrderNo > @orderNo
              OR (OrderNo = @orderNo AND Id > @id)
          )
        ORDER BY OrderNo, Id
        LIMIT 1
        ";
        command.Parameters.AddWithValue("@dayNumber", CurrentMatch.DayNumber <= 0 ? 1 : CurrentMatch.DayNumber);
        command.Parameters.AddWithValue("@orderNo", CurrentMatch.OrderNo);
        command.Parameters.AddWithValue("@id", CurrentMatch.Id);

        using var reader = command.ExecuteReader();

        if (!reader.Read())
        {
            WinnerText.Text = "No next fight in this day.";
            return;
        }

        CurrentMatch = new Match
        {
            Id = reader.GetInt32(0),
            OrderNo = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
            Fighter1Name = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Fighter2Name = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            AgeCategory = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            WeightCategory = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            Gender = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            JudgesCount = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
            DayNumber = reader.IsDBNull(8) ? 1 : reader.GetInt32(8)
        };

        SetupWindow(resetSelections: true);
        WinnerText.Text = $"Loaded next fight: Bout {CurrentMatch.OrderNo}.";
    }

    private void WindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space)
            return;

        if (e.Source is TextBox || e.Source is ComboBox)
            return;

        ToggleTimerState();
        e.Handled = true;
    }

    private void ToggleTimerState()
    {
        if (timerSession.IsFinished)
        {
            WinnerText.Text = "Timing finished for this bout.";
            return;
        }

        if (countdownTimer.IsEnabled)
        {
            countdownTimer.Stop();
            timerSession = timerSession.Pause();
            UpdateTimerUi();
            return;
        }

        timerSession = timerSession.StartOrResume();
        countdownTimer.Start();
        UpdateTimerUi();
    }

    private void CountdownTimerTick(object? sender, EventArgs e)
    {
        timerSession = timerSession.Tick();

        if (!timerSession.IsRunning)
            countdownTimer.Stop();

        UpdateTimerUi();
    }

    private void UpdateTimerUi()
    {
        TimerDisplayText.Text = FormatTimer(timerSession.RemainingSeconds);
        TimerRoundText.Text = $"{timerSession.CurrentRound}/{timerSession.RoundCount}";
        TimerBreakText.Text = FormatTimer(timerSession.BreakDurationSeconds);

        if (timerSession.IsFinished)
        {
            TimerPhaseText.Text = "Finished";
            TimerHintText.Text = "Bout timing is complete.";
            return;
        }

        if (timerSession.IsBreak)
        {
            TimerPhaseText.Text = "Break";
            TimerHintText.Text = countdownTimer.IsEnabled
                ? "Break is running."
                : "Space: resume break.";
            return;
        }

        TimerPhaseText.Text = $"Round {timerSession.CurrentRound}";

        if (timerSession.WaitingForNextRound)
        {
            TimerHintText.Text = "Break finished. Press Space to start next round.";
        }
        else if (countdownTimer.IsEnabled)
        {
            TimerHintText.Text = "Space: pause timer.";
        }
        else
        {
            TimerHintText.Text = "Space: start timer.";
        }
    }

    private static string FormatTimer(int totalSeconds)
    {
        if (totalSeconds < 0)
            totalSeconds = 0;

        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    private void LoadScoresForSelectedJudge()
    {
        ClearScoreInputs();

        if (CurrentMatch == null)
            return;

        var judgeId = GetSelectedJudgeId();

        if (judgeId <= 0)
            return;

        using var c = DatabaseHelper.CreateConnection();
        c.Open();

        var cmd = c.CreateCommand();
        cmd.CommandText =
        @"
        SELECT
            RoundNo,
            RedPoints,
            BluePoints,
            RedWarning,
            BlueWarning
        FROM JudgeScores
        WHERE MatchId = @matchId
          AND JudgeId = @judgeId
        ORDER BY RoundNo
        ";
        cmd.Parameters.AddWithValue("@matchId", CurrentMatch.Id);
        cmd.Parameters.AddWithValue("@judgeId", judgeId);

        using var r = cmd.ExecuteReader();
        isLoadingJudgeScores = true;

        try
        {
            while (r.Read())
            {
                var roundNo = r.GetInt32(0);
                var redPoints = r.GetInt32(1);
                var bluePoints = r.GetInt32(2);
                var redWarning = r.GetInt32(3) == 1;
                var blueWarning = r.GetInt32(4) == 1;

                switch (roundNo)
                {
                    case 1:
                        Round1RedInputBox.Text = redPoints.ToString();
                        Round1BlueInputBox.Text = bluePoints.ToString();
                        Round1RedWarningBox.IsChecked = redWarning;
                        Round1BlueWarningBox.IsChecked = blueWarning;
                        break;
                    case 2:
                        Round2RedInputBox.Text = redPoints.ToString();
                        Round2BlueInputBox.Text = bluePoints.ToString();
                        Round2RedWarningBox.IsChecked = redWarning;
                        Round2BlueWarningBox.IsChecked = blueWarning;
                        break;
                    case 3:
                        Round3RedInputBox.Text = redPoints.ToString();
                        Round3BlueInputBox.Text = bluePoints.ToString();
                        Round3RedWarningBox.IsChecked = redWarning;
                        Round3BlueWarningBox.IsChecked = blueWarning;
                        break;
                }
            }
        }
        finally
        {
            isLoadingJudgeScores = false;
        }
    }

    private void RecalculateTotals()
    {
        var rounds = BuildRoundScores();

        Round1RedPointsText.Text = rounds[0].HasScore ? rounds[0].RedPoints.ToString() : string.Empty;
        Round1BluePointsText.Text = rounds[0].HasScore ? rounds[0].BluePoints.ToString() : string.Empty;
        Round2RedPointsText.Text = rounds[1].HasScore ? rounds[1].RedPoints.ToString() : string.Empty;
        Round2BluePointsText.Text = rounds[1].HasScore ? rounds[1].BluePoints.ToString() : string.Empty;
        Round3RedPointsText.Text = rounds[2].HasScore ? rounds[2].RedPoints.ToString() : string.Empty;
        Round3BluePointsText.Text = rounds[2].HasScore ? rounds[2].BluePoints.ToString() : string.Empty;

        var completedRounds = rounds.Where(x => x.HasScore).ToList();

        if (completedRounds.Count == 0)
        {
            RedTotalText.Text = string.Empty;
            BlueTotalText.Text = string.Empty;
            WinnerText.Text = string.Equals(ResultMethodCombo.SelectedItem?.ToString(), "W.P", StringComparison.OrdinalIgnoreCase)
                ? $"Judge {GetSelectedJudgeId()} has no saved score yet."
                : "Manual result method selected. Use Show Winner to save the result.";
            return;
        }

        var redTotal = completedRounds.Sum(x => x.RedPoints);
        var blueTotal = completedRounds.Sum(x => x.BluePoints);

        RedTotalText.Text = redTotal.ToString();
        BlueTotalText.Text = blueTotal.ToString();

        WinnerText.Text = completedRounds.Count == rounds.Count
            ? $"Current Judge Winner: {DetermineCurrentJudgeWinner()}"
            : $"Judge {GetSelectedJudgeId()} score is incomplete.";
    }

    private List<RoundScore> BuildRoundScores()
    {
        return new List<RoundScore>
        {
            BuildRoundScore(1, Round1RedInputBox, Round1BlueInputBox, Round1RedWarningBox, Round1BlueWarningBox),
            BuildRoundScore(2, Round2RedInputBox, Round2BlueInputBox, Round2RedWarningBox, Round2BlueWarningBox),
            BuildRoundScore(3, Round3RedInputBox, Round3BlueInputBox, Round3RedWarningBox, Round3BlueWarningBox)
        };
    }

    private RoundScore BuildRoundScore(
        int roundNo,
        TextBox redInputBox,
        TextBox blueInputBox,
        CheckBox redWarningBox,
        CheckBox blueWarningBox)
    {
        var redText = redInputBox.Text?.Trim();
        var blueText = blueInputBox.Text?.Trim();
        var hasRedScore = !string.IsNullOrWhiteSpace(redText);
        var hasBlueScore = !string.IsNullOrWhiteSpace(blueText);

        if (!hasRedScore && !hasBlueScore)
        {
            return new RoundScore
            {
                RoundNo = roundNo,
                HasAnyInput = false,
                HasScore = false
            };
        }

        var redWarning = redWarningBox.IsChecked == true;
        var blueWarning = blueWarningBox.IsChecked == true;
        var redBase = ClampPoints(ParsePoints(redText));
        var blueBase = ClampPoints(ParsePoints(blueText));

        var redPoints = redBase - (redWarning ? 1 : 0);
        var bluePoints = blueBase - (blueWarning ? 1 : 0);

        var highestPoints = Math.Max(redPoints, bluePoints);

        if (highestPoints < 10)
        {
            var adjustment = 10 - highestPoints;
            redPoints += adjustment;
            bluePoints += adjustment;
        }

        redPoints = ClampPoints(redPoints);
        bluePoints = ClampPoints(bluePoints);

        return new RoundScore
        {
            RoundNo = roundNo,
            RedPoints = redPoints,
            BluePoints = bluePoints,
            RedWarning = redWarning,
            BlueWarning = blueWarning,
            HasAnyInput = hasRedScore || hasBlueScore,
            HasScore = hasRedScore && hasBlueScore
        };
    }

    private string DetermineCurrentJudgeWinner()
    {
        var rounds = BuildRoundScores();
        var completedRounds = rounds.Where(x => x.HasScore).ToList();

        if (completedRounds.Count < 3)
            return "Pending";

        var redTotal = completedRounds.Sum(x => x.RedPoints);
        var blueTotal = completedRounds.Sum(x => x.BluePoints);

        if (redTotal > blueTotal)
            return "Red";

        if (blueTotal > redTotal)
            return "Blue";

        if (StyleTieBreakBox.IsChecked == true)
            return "Tie - Better style";

        if (DefenseTieBreakBox.IsChecked == true)
            return "Tie - Better defense";

        if (OtherTieBreakBox.IsChecked == true)
            return "Tie - Other";

        return "Tie";
    }

    private void ResultMethodChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateResultControls();
        RecalculateTotals();
    }

    private void SelectRedWinnerClick(object? sender, RoutedEventArgs e)
    {
        selectedManualWinner = "Red";
        UpdateWinnerButtons();
        TrySaveWinnerFromButtons();
    }

    private void SelectBlueWinnerClick(object? sender, RoutedEventArgs e)
    {
        selectedManualWinner = "Blue";
        UpdateWinnerButtons();
        TrySaveWinnerFromButtons();
    }

    private void TrySaveWinnerFromButtons()
    {
        if (CurrentMatch == null)
            return;

        var method = ResultMethodCombo.SelectedItem?.ToString() ?? string.Empty;

        if (string.Equals(method, "W.P", StringComparison.OrdinalIgnoreCase))
        {
            WinnerText.Text = "For early finish, choose a non-points Result Method first, then press Red or Blue.";
            return;
        }

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();

        if (!SaveManualResult(connection))
            WinnerText.Text = "Manual result could not be saved.";
    }

    private void UpdateResultControls()
    {
        var isPointsDecision = string.Equals(ResultMethodCombo.SelectedItem?.ToString(), "W.P", StringComparison.OrdinalIgnoreCase);

        StyleTieBreakBox.IsEnabled = isPointsDecision;
        DefenseTieBreakBox.IsEnabled = isPointsDecision;
        OtherTieBreakBox.IsEnabled = isPointsDecision;
        RedWinnerButton.IsEnabled = !isPointsDecision;
        BlueWinnerButton.IsEnabled = !isPointsDecision;
        UpdateWinnerButtons();

        if (isPointsDecision)
        {
            ResultRoundCombo.SelectedItem ??= "3";
            return;
        }

        ResultRoundCombo.SelectedItem ??= "1";
    }

    private void UpdateWinnerButtons()
    {
        var isManualMode = !string.Equals(ResultMethodCombo.SelectedItem?.ToString(), "W.P", StringComparison.OrdinalIgnoreCase);
        var selectedBackground = "#1F1F1F";
        var selectedForeground = "White";
        var unselectedBackground = "#E5E7EB";
        var unselectedForeground = "Black";

        RedWinnerButton.Background = selectedManualWinner == "Red" && isManualMode
            ? Avalonia.Media.Brush.Parse(selectedBackground)
            : Avalonia.Media.Brush.Parse(unselectedBackground);
        RedWinnerButton.Foreground = selectedManualWinner == "Red" && isManualMode
            ? Avalonia.Media.Brush.Parse(selectedForeground)
            : Avalonia.Media.Brush.Parse(unselectedForeground);

        BlueWinnerButton.Background = selectedManualWinner == "Blue" && isManualMode
            ? Avalonia.Media.Brush.Parse(selectedBackground)
            : Avalonia.Media.Brush.Parse(unselectedBackground);
        BlueWinnerButton.Foreground = selectedManualWinner == "Blue" && isManualMode
            ? Avalonia.Media.Brush.Parse(selectedForeground)
            : Avalonia.Media.Brush.Parse(unselectedForeground);
    }

    private int ParsePoints(string? value)
    {
        return int.TryParse(value, out var points) ? points : 0;
    }

    private int ClampPoints(int value)
    {
        if (value < 0)
            return 0;

        if (value > 10)
            return 10;

        return value;
    }

    private bool TrySaveComputedResult(SqliteConnection connection)
    {
        if (CurrentMatch == null)
            return false;

        var judgesRequired = CurrentMatch.JudgesCount is 3 or 5
            ? CurrentMatch.JudgesCount
            : 5;

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT COUNT(*)
        FROM
        (
            SELECT JudgeId
            FROM JudgeScores
            WHERE MatchId = @matchId
            GROUP BY JudgeId
            HAVING COUNT(DISTINCT RoundNo) = 3
        )
        ";
        command.Parameters.AddWithValue("@matchId", CurrentMatch.Id);

        var savedJudgeCount = Convert.ToInt32(command.ExecuteScalar() ?? 0);

        if (savedJudgeCount < judgesRequired)
            return false;

        return SaveOrUpdateResult(connection);
    }

    private int GetSelectedJudgeId()
    {
        if (JudgeCombo.SelectedItem != null &&
            int.TryParse(JudgeCombo.SelectedItem.ToString(), out var judgeId))
        {
            return judgeId;
        }

        if (JudgeCombo.SelectedIndex >= 0)
            return JudgeCombo.SelectedIndex + 1;

        return selectedJudgeId > 0 ? selectedJudgeId : 1;
    }

    private void ClearScoreInputs()
    {
        isLoadingJudgeScores = true;

        try
        {
            Round1RedInputBox.Text = string.Empty;
            Round1BlueInputBox.Text = string.Empty;
            Round2RedInputBox.Text = string.Empty;
            Round2BlueInputBox.Text = string.Empty;
            Round3RedInputBox.Text = string.Empty;
            Round3BlueInputBox.Text = string.Empty;
            Round1RedWarningBox.IsChecked = false;
            Round1BlueWarningBox.IsChecked = false;
            Round2RedWarningBox.IsChecked = false;
            Round2BlueWarningBox.IsChecked = false;
            Round3RedWarningBox.IsChecked = false;
            Round3BlueWarningBox.IsChecked = false;
        }
        finally
        {
            isLoadingJudgeScores = false;
        }
    }

    private bool SaveOrUpdateResult(SqliteConnection connection, bool allowPartialJudges = false)
    {
        if (CurrentMatch == null)
            return false;

        var scores = new List<(int JudgeId, int RedTotal, int BlueTotal)>();

        var cmd = connection.CreateCommand();
        cmd.CommandText =
        @"
        SELECT
            JudgeId,
            SUM(RedPoints) AS RedTotal,
            SUM(BluePoints) AS BlueTotal
        FROM JudgeScores
        WHERE MatchId = @matchId
        GROUP BY JudgeId
        HAVING COUNT(DISTINCT RoundNo) = 3
        ORDER BY JudgeId
        ";
        cmd.Parameters.AddWithValue("@matchId", CurrentMatch.Id);

        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                scores.Add((
                    r.GetInt32(0),
                    r.IsDBNull(1) ? 0 : r.GetInt32(1),
                    r.IsDBNull(2) ? 0 : r.GetInt32(2)));
            }
        }

        if (scores.Count == 0)
            return false;

        var judgesRequired = CurrentMatch.JudgesCount is 3 or 5
            ? CurrentMatch.JudgesCount
            : 5;

        if (!allowPartialJudges && scores.Count < judgesRequired)
            return false;

        if (allowPartialJudges && scores.Count == 0)
            return false;

        var redJudges = scores.Count(x => x.RedTotal > x.BlueTotal);
        var blueJudges = scores.Count(x => x.BlueTotal > x.RedTotal);
        var winner = redJudges == blueJudges
            ? "Tie"
            : redJudges > blueJudges
                ? "Red"
                : "Blue";
        var method = ResultMethodCombo.SelectedItem?.ToString() ?? "W.P";
        var resultRound = int.TryParse(ResultRoundCombo.SelectedItem?.ToString(), out var parsedRound)
            ? parsedRound
            : 3;

        WinnerText.Text = allowPartialJudges && scores.Count < judgesRequired
            ? $"Winner: {winner} | {method} ({redJudges}-{blueJudges}) partial"
            : $"Winner: {winner} | {method} ({redJudges}-{blueJudges})";

        var resultDelete = connection.CreateCommand();
        resultDelete.CommandText = "DELETE FROM MatchResult WHERE MatchId = @matchId";
        resultDelete.Parameters.AddWithValue("@matchId", CurrentMatch.Id);
        resultDelete.ExecuteNonQuery();

        var resultInsert = connection.CreateCommand();
        resultInsert.CommandText =
        @"
        INSERT INTO MatchResult
        (
            MatchId,
            Winner,
            Method,
            Round,
            JudgeRed,
            JudgeBlue
        )
        VALUES
        (
            @matchId,
            @winner,
            @method,
            @round,
            @judgeRed,
            @judgeBlue
        )
        ";

        resultInsert.Parameters.AddWithValue("@matchId", CurrentMatch.Id);
        resultInsert.Parameters.AddWithValue("@winner", winner);
        resultInsert.Parameters.AddWithValue("@method", method);
        resultInsert.Parameters.AddWithValue("@round", resultRound);
        resultInsert.Parameters.AddWithValue("@judgeRed", redJudges);
        resultInsert.Parameters.AddWithValue("@judgeBlue", blueJudges);
        resultInsert.ExecuteNonQuery();

        TournamentProgressionService.RebuildNextDayFrom(
            CurrentMatch.DayNumber <= 0 ? 1 : CurrentMatch.DayNumber,
            CurrentMatch.JudgesCount is 3 or 5 ? CurrentMatch.JudgesCount : 5);

        return true;
    }

    private bool SaveManualResult(SqliteConnection connection)
    {
        if (CurrentMatch == null)
            return false;

        var method = ResultMethodCombo.SelectedItem?.ToString() ?? string.Empty;
        var winner = selectedManualWinner;

        if (string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(winner))
            return false;

        if (string.Equals(method, "No Contest", StringComparison.OrdinalIgnoreCase))
            winner = "Tie";

        if (!string.Equals(winner, "Red", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(winner, "Blue", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(winner, "Tie", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var resultRound = int.TryParse(ResultRoundCombo.SelectedItem?.ToString(), out var parsedRound)
            ? parsedRound
            : 1;

        var resultDelete = connection.CreateCommand();
        resultDelete.CommandText = "DELETE FROM MatchResult WHERE MatchId = @matchId";
        resultDelete.Parameters.AddWithValue("@matchId", CurrentMatch.Id);
        resultDelete.ExecuteNonQuery();

        var resultInsert = connection.CreateCommand();
        resultInsert.CommandText =
        @"
        INSERT INTO MatchResult
        (
            MatchId,
            Winner,
            Method,
            Round,
            JudgeRed,
            JudgeBlue
        )
        VALUES
        (
            @matchId,
            @winner,
            @method,
            @round,
            @judgeRed,
            @judgeBlue
        )
        ";

        resultInsert.Parameters.AddWithValue("@matchId", CurrentMatch.Id);
        resultInsert.Parameters.AddWithValue("@winner", winner);
        resultInsert.Parameters.AddWithValue("@method", method);
        resultInsert.Parameters.AddWithValue("@round", resultRound);
        resultInsert.Parameters.AddWithValue("@judgeRed", 0);
        resultInsert.Parameters.AddWithValue("@judgeBlue", 0);
        resultInsert.ExecuteNonQuery();

        WinnerText.Text = $"Winner: {winner} | {method} (Round {resultRound})";

        TournamentProgressionService.RebuildNextDayFrom(
            CurrentMatch.DayNumber <= 0 ? 1 : CurrentMatch.DayNumber,
            CurrentMatch.JudgesCount is 3 or 5 ? CurrentMatch.JudgesCount : 5);

        return true;
    }

    private void UpdateOfficialsVisibility()
    {
        var judgesCount = CurrentMatch?.JudgesCount is 3 or 5
            ? CurrentMatch.JudgesCount
            : 3;

        Judge4Panel.IsVisible = judgesCount == 5;
        Judge5Panel.IsVisible = judgesCount == 5;
    }

    private class RoundScore
    {
        public int RoundNo { get; set; }
        public int RedPoints { get; set; }
        public int BluePoints { get; set; }
        public bool RedWarning { get; set; }
        public bool BlueWarning { get; set; }
        public bool HasAnyInput { get; set; }
        public bool HasScore { get; set; }
    }

    private sealed class TimerSession
    {
        public int RoundCount { get; }
        public int RoundDurationSeconds { get; }
        public int BreakDurationSeconds { get; }
        public int CurrentRound { get; }
        public int RemainingSeconds { get; }
        public bool IsBreak { get; }
        public bool IsRunning { get; }
        public bool WaitingForNextRound { get; }
        public bool IsFinished { get; }

        public TimerSession(int roundCount, int roundDurationSeconds, int breakDurationSeconds)
            : this(
                roundCount <= 0 ? 3 : roundCount,
                roundDurationSeconds <= 0 ? 120 : roundDurationSeconds,
                breakDurationSeconds <= 0 ? 60 : breakDurationSeconds,
                currentRound: 1,
                remainingSeconds: roundDurationSeconds <= 0 ? 120 : roundDurationSeconds,
                isBreak: false,
                isRunning: false,
                waitingForNextRound: false,
                isFinished: false)
        {
        }

        private TimerSession(
            int roundCount,
            int roundDurationSeconds,
            int breakDurationSeconds,
            int currentRound,
            int remainingSeconds,
            bool isBreak,
            bool isRunning,
            bool waitingForNextRound,
            bool isFinished)
        {
            RoundCount = roundCount;
            RoundDurationSeconds = roundDurationSeconds;
            BreakDurationSeconds = breakDurationSeconds;
            CurrentRound = currentRound;
            RemainingSeconds = remainingSeconds;
            IsBreak = isBreak;
            IsRunning = isRunning;
            WaitingForNextRound = waitingForNextRound;
            IsFinished = isFinished;
        }

        public static TimerSession CreateDefault() => new(3, 120, 60);

        public TimerSession WithDurations(int roundCount, int roundDurationSeconds, int breakDurationSeconds)
        {
            return new TimerSession(
                roundCount <= 0 ? 3 : roundCount,
                roundDurationSeconds <= 0 ? 120 : roundDurationSeconds,
                breakDurationSeconds <= 0 ? 60 : breakDurationSeconds,
                Math.Min(CurrentRound, roundCount <= 0 ? 3 : roundCount),
                ResolveRemainingSeconds(roundDurationSeconds, breakDurationSeconds),
                IsBreak,
                false,
                WaitingForNextRound,
                IsFinished);
        }

        public TimerSession StartOrResume()
        {
            if (IsFinished)
                return this;

            if (WaitingForNextRound)
            {
                return new TimerSession(
                    RoundCount,
                    RoundDurationSeconds,
                    BreakDurationSeconds,
                    CurrentRound,
                    RoundDurationSeconds,
                    isBreak: false,
                    isRunning: true,
                    waitingForNextRound: false,
                    isFinished: false);
            }

            return new TimerSession(
                RoundCount,
                RoundDurationSeconds,
                BreakDurationSeconds,
                CurrentRound,
                RemainingSeconds,
                IsBreak,
                isRunning: true,
                waitingForNextRound: false,
                isFinished: false);
        }

        public TimerSession Pause()
        {
            return new TimerSession(
                RoundCount,
                RoundDurationSeconds,
                BreakDurationSeconds,
                CurrentRound,
                RemainingSeconds,
                IsBreak,
                isRunning: false,
                WaitingForNextRound,
                IsFinished);
        }

        public TimerSession Tick()
        {
            if (!IsRunning || IsFinished)
                return this;

            if (RemainingSeconds > 1)
            {
                return new TimerSession(
                    RoundCount,
                    RoundDurationSeconds,
                    BreakDurationSeconds,
                    CurrentRound,
                    RemainingSeconds - 1,
                    IsBreak,
                    isRunning: true,
                    waitingForNextRound: false,
                    isFinished: false);
            }

            if (!IsBreak)
            {
                if (CurrentRound >= RoundCount)
                {
                    return new TimerSession(
                        RoundCount,
                        RoundDurationSeconds,
                        BreakDurationSeconds,
                        CurrentRound,
                        0,
                        isBreak: false,
                        isRunning: false,
                        waitingForNextRound: false,
                        isFinished: true);
                }

                return new TimerSession(
                    RoundCount,
                    RoundDurationSeconds,
                    BreakDurationSeconds,
                    CurrentRound,
                    BreakDurationSeconds,
                    isBreak: true,
                    isRunning: true,
                    waitingForNextRound: false,
                    isFinished: false);
            }

            return new TimerSession(
                RoundCount,
                RoundDurationSeconds,
                BreakDurationSeconds,
                CurrentRound + 1,
                RoundDurationSeconds,
                isBreak: false,
                isRunning: false,
                waitingForNextRound: true,
                isFinished: false);
        }

        private int ResolveRemainingSeconds(int roundDurationSeconds, int breakDurationSeconds)
        {
            if (IsFinished)
                return 0;

            if (WaitingForNextRound || !IsBreak)
                return roundDurationSeconds <= 0 ? 120 : roundDurationSeconds;

            return breakDurationSeconds <= 0 ? 60 : breakDurationSeconds;
        }
    }
}
