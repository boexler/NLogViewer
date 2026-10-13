using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Parsers;
using Xunit;

namespace Sentinel.LogViewer.Tests.Parsers;

public class Log4JEventParserTests
{
	private readonly Log4JEventParser _parser = new();

	// -------------------------------------------------------------
	// BASIC TESTS
	// -------------------------------------------------------------

	[Fact]
	public void Parse_ValidXml_ParsesMinimalEvent()
	{
		var xml = @"<log4j:event logger=""TestLogger"" level=""INFO"" timestamp=""1234"" thread=""T1"" xmlns:log4j=""http://jakarta.apache.org/log4j/"">
                        <log4j:message>Hello</log4j:message>
                    </log4j:event>";

		var evt = _parser.Parse(xml);

		Assert.Equal("TestLogger", evt.Logger);
		Assert.Equal(Log4JLevel.Info, evt.Level);
		Assert.Equal("Hello", evt.Message);
		Assert.Equal("T1", evt.Thread);
		Assert.Equal(1234, evt.Timestamp);
	}

	[Fact]
	public void Parse_ValidLevels_AllMappedCorrectly()
	{
		var levels = new[] { "TRACE", "DEBUG", "INFO", "WARN", "ERROR", "FATAL" };

		foreach (var lvl in levels)
		{
			var xml = $@"<log4j:event logger=""L"" level=""{lvl}"" timestamp=""1"" xmlns:log4j=""http://jakarta.apache.org/log4j/""><log4j:message>x</log4j:message></log4j:event>";
			var evt = _parser.Parse(xml);
			Assert.Equal(lvl, evt.Level.ToString().ToUpper());
		}
	}

	// -------------------------------------------------------------
	// ROBUSTNESS TESTS
	// -------------------------------------------------------------

	[Fact]
	public void Parse_XmlWithoutNamespace_AddsNamespaceAutomatically()
	{
		var xml = @"<log4j:event logger=""Test"" level=""INFO"" timestamp=""1""><log4j:message>Msg</log4j:message></log4j:event>";

		var evt = _parser.Parse(xml);

		Assert.Equal("Msg", evt.Message);
		Assert.Equal("Test", evt.Logger);
	}

	[Fact]
	public void Parse_XmlWithUnknownElements_IgnoresThem()
	{
		var xml = @"<log4j:event logger=""L"" level=""INFO"" timestamp=""1"" xmlns:log4j=""http://jakarta.apache.org/log4j/"">
                        <log4j:message>Hello</log4j:message>
                        <log4j:unknown>ThisShouldNotBreak</log4j:unknown>
                    </log4j:event>";

		var evt = _parser.Parse(xml);

		Assert.Equal("Hello", evt.Message);
	}

	[Fact]
	public void Parse_XmlWithThreadAsElement_StillParses()
	{
		var xml = @"<log4j:event logger=""L"" level=""INFO"" timestamp=""1"" xmlns:log4j=""http://jakarta.apache.org/log4j/"">
                        <log4j:message>Hello</log4j:message>
                        <log4j:thread>Thread-42</log4j:thread>
                    </log4j:event>";

		var evt = _parser.Parse(xml);

		Assert.Equal("Thread-42", evt.Thread);
	}

	// -------------------------------------------------------------
	// THROWABLE TESTS
	// -------------------------------------------------------------

	[Fact]
	public void Parse_Throwable_MultiLineStackTrace_ParsesCorrectly()
	{
		var xml = @"<log4j:event logger=""L"" level=""ERROR"" timestamp=""1"" xmlns:log4j=""http://jakarta.apache.org/log4j/"">
                        <log4j:message>Error!</log4j:message>
                        <log4j:throwable><![CDATA[
System.Exception: Test
  at MyClass.Method() in File.cs:line 123
  at Program.Main()
                        ]]></log4j:throwable>
                    </log4j:event>";

		var evt = _parser.Parse(xml);

		Assert.Contains("System.Exception", evt.Throwable);
		Assert.Contains("MyClass.Method", evt.Throwable);
	}

	// -------------------------------------------------------------
	// PROPERTY TESTS
	// -------------------------------------------------------------

	[Fact]
	public void Parse_Properties_ParsesValuesCorrectly()
	{
		var xml = @"<log4j:event logger=""L"" level=""INFO"" timestamp=""1"" xmlns:log4j=""http://jakarta.apache.org/log4j/"">
                        <log4j:message>Msg</log4j:message>
                        <log4j:properties>
                            <log4j:data name=""A"" value=""1""/>
                            <log4j:data name=""B"" value=""2""/>
                        </log4j:properties>
                    </log4j:event>";

		var evt = _parser.Parse(xml);

		Assert.Equal("1", evt.Properties["A"]);
		Assert.Equal("2", evt.Properties["B"]);
	}

	// -------------------------------------------------------------
	// EDGE CASES
	// -------------------------------------------------------------

	[Fact]
	public void Parse_EmptyMessage_ReturnsEmptyString()
	{
		var xml = @"<log4j:event logger=""L"" level=""INFO"" timestamp=""1"" xmlns:log4j=""http://jakarta.apache.org/log4j/""><log4j:message></log4j:message></log4j:event>";

		var evt = _parser.Parse(xml);

		Assert.Equal("", evt.Message);
	}

	[Fact]
	public void Parse_InvalidTimestamp_ReturnsZero()
	{
		var xml = @"<log4j:event logger=""L"" level=""INFO"" timestamp=""abc"" xmlns:log4j=""http://jakarta.apache.org/log4j/"">
                        <log4j:message>Test</log4j:message>
                    </log4j:event>";

		var evt = _parser.Parse(xml);

		Assert.Equal(0, evt.Timestamp);
	}

	[Fact]
	public void Parse_XmlWithCDataMessage_ParsesCorrectly()
	{
		var xml = @"<log4j:event logger=""L"" level=""INFO"" timestamp=""1"" xmlns:log4j=""http://jakarta.apache.org/log4j/"">
                        <log4j:message><![CDATA[Test <xml> & weird chars]]></log4j:message>
                    </log4j:event>";

		var evt = _parser.Parse(xml);

		Assert.Equal("Test <xml> & weird chars", evt.Message);
	}
}