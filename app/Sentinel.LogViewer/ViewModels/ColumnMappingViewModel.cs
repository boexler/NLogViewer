using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Sentinel.LogViewer.Wpf;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Parsers;
using Sentinel.LogViewer.Services;

namespace Sentinel.LogViewer.ViewModels;

/// <summary>
/// ViewModel for the Column Mapping dialog
/// </summary>
public class ColumnMappingViewModel : INotifyPropertyChanged
{
	private readonly TextFileFormat _format;
	private readonly List<string> _sampleLines;
	private readonly string _filePath;
	private string _separator;
	private bool _hasHeaderLine;
	private DateOrder _dateOrder;
	private bool _saveForPattern;
	private string _filePattern = string.Empty;
	private ObservableCollection<ColumnMappingItem> _columns = new();
	private ObservableCollection<ColumnMappingItem> _previewResults = new();

	public ColumnMappingViewModel(TextFileFormat format, List<string> sampleLines, string filePath)
	{
		_format = format ?? throw new ArgumentNullException(nameof(format));
		_sampleLines = sampleLines ?? throw new ArgumentNullException(nameof(sampleLines));
		_filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));

		_separator = format.Separator;
		_hasHeaderLine = format.HasHeaderLine;
		_dateOrder = format.DateOrder;
		_filePattern = System.IO.Path.GetFileName(filePath);

		InitializeColumns();
		UpdatePreview();

		SaveCommand = new RelayCommand(Save, CanSave);
		CancelCommand = new RelayCommand(() => { });
	}

	/// <summary>
	/// Gets the detected columns
	/// </summary>
	public ObservableCollection<ColumnMappingItem> Columns
	{
		get => _columns;
		set
		{
			if (_columns != value)
			{
				_columns = value;
				OnPropertyChanged();
			}
		}
	}

	/// <summary>
	/// Gets the preview results
	/// </summary>
	public ObservableCollection<ColumnMappingItem> PreviewResults
	{
		get => _previewResults;
		set
		{
			if (_previewResults != value)
			{
				_previewResults = value;
				OnPropertyChanged();
			}
		}
	}

	/// <summary>
	/// Gets or sets whether the first line is a header
	/// </summary>
	public bool HasHeaderLine
	{
		get => _hasHeaderLine;
		set
		{
			if (_hasHeaderLine != value)
			{
				_hasHeaderLine = value;
				OnPropertyChanged();
				UpdatePreview();
			}
		}
	}

	/// <summary>
	/// Gets or sets how day and month are ordered in dates like 01/02/2025
	/// </summary>
	public DateOrder DateOrder
	{
		get => _dateOrder;
		set
		{
			if (_dateOrder != value)
			{
				_dateOrder = value;
				OnPropertyChanged();
				UpdatePreview();
			}
		}
	}

	/// <summary>
	/// Gets the selectable orders of day and month by display name; the automatic one names what this computer uses
	/// </summary>
	public Dictionary<string, DateOrder> DateOrders { get; } = new()
	{
		{ $"Automatic ({(TimestampParser.ResolveOrder(DateOrder.Auto) == DateOrder.MonthFirst ? "month first" : "day first")} on this computer)", DateOrder.Auto },
		{ "Day first (18/12/2025)", DateOrder.DayFirst },
		{ "Month first (12/18/2025)", DateOrder.MonthFirst }
	};

	/// <summary>
	/// Gets or sets whether to save the mapping for the file pattern
	/// </summary>
	public bool SaveForPattern
	{
		get => _saveForPattern;
		set
		{
			if (_saveForPattern != value)
			{
				_saveForPattern = value;
				OnPropertyChanged();
			}
		}
	}

	/// <summary>
	/// Gets or sets the file pattern for saving
	/// </summary>
	public string FilePattern
	{
		get => _filePattern;
		set
		{
			if (_filePattern != value)
			{
				_filePattern = value;
				OnPropertyChanged();
			}
		}
	}

	/// <summary>
	/// Gets the available field types for mapping
	/// </summary>
	public List<string> FieldTypes { get; } = new() { "None", "Timestamp", "Level", "Logger", "Message" };

	/// <summary>
	/// Gets the selectable separators by display name
	/// </summary>
	public Dictionary<string, string> Separators { get; } = new()
	{
		{ "Tab", "\t" },
		{ "Pipe with spaces ( | )", " | " },
		{ "Pipe (|)", "|" },
		{ "Comma (,)", "," },
		{ "Semicolon (;)", ";" },
		{ "Spaces", TextLineSplitter.SpacesSeparator }
	};

	/// <summary>
	/// Gets or sets the separator; changing it re-splits the columns and clears their mapping
	/// </summary>
	public string Separator
	{
		get => _separator;
		set
		{
			if (_separator != value)
			{
				_separator = value;
				OnPropertyChanged();
				InitializeColumns();
				UpdatePreview();
			}
		}
	}

	/// <summary>
	/// Gets the final format configuration
	/// </summary>
	public TextFileFormat FinalFormat
	{
		get
		{
			var mapping = new ColumnMapping();
			var timestampColumns = Columns.Where(column => column.MappedField == "Timestamp").Select(column => column.Index).OrderBy(index => index).ToList();
			if (timestampColumns.Count > 0)
			{
				// Date and time can be separate columns; the ones next to the first belong to the timestamp.
				mapping.TimestampColumn = timestampColumns[0];
				mapping.TimestampColumnCount = timestampColumns.TakeWhile((index, position) => index == timestampColumns[0] + position).Count();
			}

			foreach (var column in Columns)
			{
				switch (column.MappedField)
				{
					case "Level":
						mapping.LevelColumn = column.Index;
						break;
					case "Logger":
						mapping.LoggerColumn = column.Index;
						break;
					case "Message":
						mapping.MessageColumn = column.Index;
						break;
				}
			}

			return new TextFileFormat
			{
				Separator = Separator,
				QuotedFields = TextFileFormatDetector.DetectQuotedFields(DataLines(), Separator),
				HasHeaderLine = HasHeaderLine,
				StartLineIndex = HasHeaderLine ? 1 : 0,
				ColumnCount = Columns.Count,
				ColumnMapping = mapping,
				DateOrder = DateOrder
			};
		}
	}

	public ICommand SaveCommand { get; set; }
	public ICommand CancelCommand { get; set; }

	private void InitializeColumns()
	{
		var columns = new ObservableCollection<ColumnMappingItem>();

		// Get sample data line
		var sampleLine = DataLines().FirstOrDefault();
		if (sampleLine == null)
			return;

		// The detected column count and mapping only fit the detected separator.
		var isDetectedSeparator = Separator == _format.Separator;
		var parts = TextLineSplitter.Split(sampleLine, Separator, maxColumns: isDetectedSeparator ? _format.ColumnCount : 0);
		var mapping = isDetectedSeparator ? _format.ColumnMapping : new ColumnMapping();

		for (int i = 0; i < parts.Length; i++)
		{
			var mappedField = "None";
			if (mapping.TimestampColumn >= 0 && i >= mapping.TimestampColumn && i < mapping.TimestampColumn + mapping.TimestampColumnCount)
				mappedField = "Timestamp";
			else if (i == mapping.LevelColumn)
				mappedField = "Level";
			else if (i == mapping.LoggerColumn)
				mappedField = "Logger";
			else if (i == mapping.MessageColumn)
				mappedField = "Message";

			var column = new ColumnMappingItem
			{
				Index = i,
				SampleData = parts[i].Trim(),
				MappedField = mappedField
			};

			column.PropertyChanged += (s, e) =>
			{
				if (e.PropertyName == nameof(ColumnMappingItem.MappedField))
				{
					UpdatePreview();
					((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
				}
			};

			columns.Add(column);
		}

		Columns = columns;
	}

	private void UpdatePreview()
	{
		var preview = new ObservableCollection<ColumnMappingItem>();

		var format = FinalFormat;
		foreach (var line in DataLines().Take(5))
		{
			var parts = PlainTextParser.SplitLine(line, format);
			var timestamp = GetColumnValue(parts, "Timestamp");
			var level = GetColumnValue(parts, "Level");
			var logger = GetColumnValue(parts, "Logger");
			var message = GetColumnValue(parts, "Message");

			preview.Add(new ColumnMappingItem
			{
				Index = -1,
				SampleData = $"Timestamp: {timestamp ?? "N/A"} | Level: {level ?? "N/A"} | Logger: {logger ?? "N/A"} | Message: {message ?? "N/A"}"
			});
		}

		PreviewResults = preview;
	}

	private string? GetColumnValue(string[] parts, string fieldType)
	{
		var columns = Columns.Where(c => c.MappedField == fieldType && c.Index >= 0 && c.Index < parts.Length).ToList();
		if (columns.Count == 0)
			return null;

		// A timestamp can be spread over several columns, e.g. the date and the time.
		return fieldType == "Timestamp"
			? string.Join(" ", columns.Select(c => parts[c.Index].Trim()))
			: parts[columns[0].Index].Trim();
	}

	private IEnumerable<string> DataLines()
	{
		return _sampleLines.Skip(HasHeaderLine ? 1 : 0).Where(l => !string.IsNullOrWhiteSpace(l));
	}

	private bool CanSave()
	{
		// At least one field must be mapped
		return Columns.Any(c => c.MappedField != "None");
	}

	private void Save()
	{
		// Validation is handled by CanSave
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}

/// <summary>
/// Represents a column mapping item in the UI
/// </summary>
public class ColumnMappingItem : INotifyPropertyChanged
{
	private string _mappedField = "None";
	private string _sampleData = string.Empty;

	/// <summary>
	/// Gets or sets the column index
	/// </summary>
	public int Index { get; set; }

	/// <summary>
	/// Gets or sets the sample data for this column
	/// </summary>
	public string SampleData
	{
		get => _sampleData;
		set
		{
			if (_sampleData != value)
			{
				_sampleData = value;
				OnPropertyChanged();
			}
		}
	}

	/// <summary>
	/// Gets or sets the mapped field type
	/// </summary>
	public string MappedField
	{
		get => _mappedField;
		set
		{
			if (_mappedField != value)
			{
				_mappedField = value;
				OnPropertyChanged();
			}
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}

