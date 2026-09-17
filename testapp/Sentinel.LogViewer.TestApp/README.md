# Sentinel LogViewer Test App

This console application generates random log messages and sends them via UDP to test Sentinel LogViewer.

## Features

- Generates random log messages every second
- Random log levels (Trace, Debug, Info, Warn, Error, Fatal)
- Generates exceptions (20% chance) with Error or Fatal level
- Supports both ChainsawTarget (NLog 5) and Log4JXmlTarget (NLog 6)
- Sends logs to UDP port 4000 (configurable in nlog.config)

## Launch Profiles

The application has two launch profiles configured in `Properties/launchSettings.json`:

1. **NLog 5 (ChainsawTarget)** - Uses ChainsawTarget for NLog 5 compatibility
2. **NLog 6 (Log4JXmlTarget)** - Uses Log4JXmlTarget for NLog 6

### Running with Different Profiles

#### Visual Studio
1. Right-click on the project
2. Select "Properties"
3. Go to "Debug" → "General"
4. Select the desired launch profile from the dropdown

#### Command Line
```powershell
# Run with ChainsawTarget (NLog 5)
$env:NLOG_TARGET="chainsaw"; dotnet run

# Run with Log4JXmlTarget (NLog 6)
$env:NLOG_TARGET="log4jxml"; dotnet run
```

## Configuration

The `nlog.config` file contains both target definitions:
- `chainsaw` - ChainsawTarget for NLog 5 compatibility
- `log4jxml` - Log4JXmlTarget for NLog 6

The target to use is determined by the `NLOG_TARGET` environment variable, which defaults to `chainsaw`.

## Usage

1. Start Sentinel LogViewer
2. Configure it to listen on UDP port 4000
3. Run this test application
4. Watch the logs appear in Sentinel LogViewer

Press any key to stop the test application.


## Generated Log Types

- **Normal Logs**: Random messages with random log levels
- **Exception Logs**: 
  - InvalidOperationException (Error level)
  - ArgumentNullException (Error level)
  - DivideByZeroException (Fatal level)

Exceptions are generated with a 20% probability (1 in 5 messages).


