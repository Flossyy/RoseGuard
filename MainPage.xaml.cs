using System.Collections.Generic;
using Microsoft.Maui.Graphics;
using RoseGuard.Models;
using RoseGuard.Services;

namespace RoseGuard;

public partial class MainPage : ContentPage
{
	private readonly DatabaseService _database = new();
	private readonly Dictionary<DateOnly, SecureNote> _monthNotes = new();
	private readonly Dictionary<DateOnly, Button> _dayButtons = new();

	private DateTime _currentMonth = DateTime.Today;
	private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Today);

	public MainPage()
	{
		InitializeComponent();
		Loaded += OnPageLoaded;
	}

	private async void OnPageLoaded(object? sender, EventArgs e)
	{
		await InitializeAndLoadAsync();
	}

	private async Task InitializeAndLoadAsync()
	{
		await EnsureInitializedAsync();
		await LoadMonthAsync(_currentMonth);
		await SelectDateAsync(_selectedDate);
	}

	private async Task EnsureInitializedAsync()
	{
		try
		{
			await _database.InitializeAsync();
			StatusLabel.Text = "Encrypted database ready.";
		}
		catch (Exception ex)
		{
			StatusLabel.Text = $"Init failed: {ex.Message}";
		}
	}

	private async Task LoadMonthAsync(DateTime month)
	{
		_currentMonth = new DateTime(month.Year, month.Month, 1);
		MonthLabel.Text = _currentMonth.ToString("MMMM yyyy");

		_monthNotes.Clear();
		try
		{
			var notes = await _database.GetNotesForMonthAsync(_currentMonth).ConfigureAwait(false);
			foreach (var note in notes)
			{
				var localDate = DateOnly.FromDateTime(note.NoteDateUtc.ToLocalTime());
				_monthNotes[localDate] = note;
			}
			StatusLabel.Text = $"Loaded {_monthNotes.Count} notes for this month.";
		}
		catch (Exception ex)
		{
			StatusLabel.Text = $"Load failed: {ex.Message}";
		}

		BuildCalendarGrid();
	}

	private void BuildCalendarGrid()
	{
		CalendarGrid.Children.Clear();
		CalendarGrid.RowDefinitions.Clear();
		_dayButtons.Clear();

		for (var i = 0; i < 6; i++)
		{
			CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		}

		var firstDay = _currentMonth;
		var startDate = firstDay.AddDays(-(int)firstDay.DayOfWeek);
		const int totalCells = 42; // 6 weeks view

		for (var index = 0; index < totalCells; index++)
		{
			var day = startDate.AddDays(index);
			var dateOnly = DateOnly.FromDateTime(day);
			var row = index / 7;
			var col = index % 7;

			var isCurrentMonth = day.Month == _currentMonth.Month;
			var hasNote = _monthNotes.ContainsKey(dateOnly);
			var button = new Button
			{
				Text = hasNote ? $"{day.Day} •" : $"{day.Day}",
				CornerRadius = 8,
				Padding = new Thickness(6, 8),
				HeightRequest = 44,
				FontAttributes = hasNote ? FontAttributes.Bold : FontAttributes.None,
				BackgroundColor = isCurrentMonth ? Colors.White : Color.FromRgba(255, 255, 255, 0.6),
				TextColor = isCurrentMonth ? Colors.Black : Color.FromArgb("#555555"),
				BorderWidth = 1,
				BorderColor = Color.FromArgb("#DDDDDD")
			};

			button.Clicked += (_, _) => _ = SelectDateAsync(dateOnly);

			CalendarGrid.Add(button, col, row);
			_dayButtons[dateOnly] = button;
		}

		UpdateSelectionHighlight();
	}

	private async Task SelectDateAsync(DateOnly date)
	{
		_selectedDate = date;
		UpdateSelectionHighlight();
		SelectedDateLabel.Text = date.ToString("D");
		await LoadNoteForSelectedAsync();
	}

	private void UpdateSelectionHighlight()
	{
		foreach (var kvp in _dayButtons)
		{
			var button = kvp.Value;
			var isSelected = kvp.Key == _selectedDate;
			var isCurrentMonth = kvp.Key.Month == _currentMonth.Month;
			var baseBg = isCurrentMonth ? Colors.White : Color.FromRgba(255, 255, 255, 0.6);
			button.BorderColor = isSelected ? Color.FromArgb("#512BD4") : Color.FromArgb("#DDDDDD");
			button.BackgroundColor = isSelected ? Color.FromArgb("#E6E0FF") : baseBg;
			button.TextColor = isCurrentMonth ? Colors.Black : Color.FromArgb("#555555");
		}
	}

	private async Task LoadNoteForSelectedAsync()
	{
		try
		{
			var note = _monthNotes.TryGetValue(_selectedDate, out var cached)
				? cached
				: await _database.GetNoteForDateAsync(_selectedDate).ConfigureAwait(false);

			if (note is not null)
			{
				TitleEntry.Text = note.Title;
				BodyEditor.Text = note.Body;
				_monthNotes[_selectedDate] = note;
				StatusLabel.Text = "Note loaded for this day.";
			}
			else
			{
				TitleEntry.Text = string.Empty;
				BodyEditor.Text = string.Empty;
				StatusLabel.Text = "No note for this day yet.";
			}
		}
		catch (Exception ex)
		{
			StatusLabel.Text = $"Load failed: {ex.Message}";
		}
	}

	private async void OnSaveClicked(object sender, EventArgs e)
	{
		try
		{
			await _database.SaveNoteForDateAsync(_selectedDate, TitleEntry.Text?.Trim() ?? string.Empty, BodyEditor.Text?.Trim() ?? string.Empty);
			StatusLabel.Text = "Saved.";
			await LoadMonthAsync(_currentMonth);
			await LoadNoteForSelectedAsync();
		}
		catch (Exception ex)
		{
			StatusLabel.Text = $"Save failed: {ex.Message}";
		}
	}

	private async void OnPrevMonthClicked(object sender, EventArgs e)
	{
		await ChangeMonthAsync(-1);
	}

	private async void OnNextMonthClicked(object sender, EventArgs e)
	{
		await ChangeMonthAsync(1);
	}

	private async Task ChangeMonthAsync(int offsetMonths)
	{
		var newMonth = _currentMonth.AddMonths(offsetMonths);
		await LoadMonthAsync(newMonth);
		var targetDay = Math.Min(_selectedDate.Day, DateTime.DaysInMonth(newMonth.Year, newMonth.Month));
		var newDate = new DateOnly(newMonth.Year, newMonth.Month, targetDay);
		await SelectDateAsync(newDate);
	}

	private async void OnTodayClicked(object sender, EventArgs e)
	{
		_selectedDate = DateOnly.FromDateTime(DateTime.Today);
		await LoadMonthAsync(DateTime.Today);
		await SelectDateAsync(_selectedDate);
	}
}
