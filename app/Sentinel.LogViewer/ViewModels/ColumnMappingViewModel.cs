using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Sentinel.NLogViewer.Wpf;
using Sentinel.LogViewer.Models;

namespace Sentinel.LogViewer.ViewModels;

/// <summary>
/// ViewModel for the Column Mapping dialog
/// </summary>
public class ColumnMappingViewModel : INotifyPropertyChanged
{
	private readonly TextFileFormat _format;
	private readonly List<string> _sampleLines;
	private readonly string _filePath;
	private bool _hasHeaderLine;
	private bool _saveForPattern;
	private string _filePattern = string.Empty;
	private ObservableCollection<ColumnMappingItem> _columns = new();
	private ObservableCollection<ColumnMappingItem> _previewResults = new();

	public ColumnMappingViewModel(TextFileFormat format, List<string> sampleLines, string filePath)
	{
		_format = format ?? throw new ArgumentNullException(nameof(format));
		_sampleLines = sampleLines ?? throw new ArgumentNullException(nameof(sampleLines));
		_filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));

		_hasHeaderLine = format.HasHeaderLine;
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
	/// Gets the detected separator
	/// </summary>
	public string Separator => _format.Separator;

	/// <summary>
	/// Gets the final format configuration
	/// </summary>
	public TextFileFormat FinalFormat
	{
		get
		{
			var mapping = new ColumnMapping();
			foreach (var column in Columns)
			{
				switch (column.MappedField)
				{
					case "Timestamp":
						mapping.TimestampColumn = column.Index;
						break;
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
				Separator = _format.Separator,
				HasHeaderLine = HasHeaderLine,
				StartLineIndex = HasHeaderLine ? 1 : 0,
				ColumnCount = _format.ColumnCount,
				ColumnMapping = mapping
			};
		}
	}

	public ICommand SaveCommand { get; set; }
	public ICommand CancelCommand { get; set; }

	private void InitializeColumns()
	{
		var columns = new ObservableCollection<ColumnMappingItem>();

		// Get sample data line
		var dataLines = _sampleLines.Skip(_format.StartLineIndex).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
		if (dataLines.Count == 0)
			return;

		var sampleLine = dataLines[0];
		var parts = SplitLine(sampleLine, _format.Separator);

		for (int i = 0; i < parts.Length; i++)
		{
			var mappedField = "None";
			if (i == _format.ColumnMapping.TimestampColumn)
				mappedField = "Timestamp";
			else if (i == _format.ColumnMapping.LevelColumn)
				mappedField = "Level";
			else if (i == _format.ColumnMapping.LoggerColumn)
				mappedField = "Logger";
			else if (i == _format.ColumnMapping.MessageColumn)
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

		var dataLines = _sampleLines.Skip(HasHeaderLine ? 1 : 0)
			.Where(l => !string.IsNullOrWhiteSpace(l))
			.Take(5)
			.ToList();

		foreach (var line in dataLines)
		{
			var parts = SplitLine(line, _format.Separator);
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
		var column = Columns.FirstOrDefault(c => c.MappedField == fieldType);
		if (column == null || column.Index < 0 || column.Index >= parts.Length)
			return null;

		return parts[column.Index].Trim();
	}

	private string[] SplitLine(string line, string separator)
	{
		if (separator == "  ") // Multiple spaces
		{
			return line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		}

		return line.Split(new[] { separator }, StringSplitOptions.None);
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

