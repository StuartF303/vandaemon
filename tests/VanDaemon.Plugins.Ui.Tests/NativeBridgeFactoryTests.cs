using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using VanDaemon.Plugins.Ui.Bridge;
using Xunit;

namespace VanDaemon.Plugins.Ui.Tests;

/// <summary>
/// SC-001 (008-native-bridge-transport): the factory selects the JS-interop transport only when the
/// native object is present, otherwise the stub, always fails safe, and logs which it chose. The log
/// is the deliberate distinguisher — both bridges return identical stub values, so on the unit only
/// the logged selection tells a real round-trip from a silent stub fallback.
/// </summary>
public class NativeBridgeFactoryTests
{
    [Fact]
    public void Create_WhenNativeTransportPresent_SelectsJsInteropBridge_AndLogs()
    {
        var js = new Mock<IJSInProcessRuntime>();
        js.Setup(r => r.Invoke<bool>(NativeBridgeFactory.HasNativeProbe, It.IsAny<object?[]>()))
            .Returns(true);
        var loggerFactory = new CapturingLoggerFactory();

        var bridge = NativeBridgeFactory.Create(js.Object, loggerFactory);

        bridge.Should().BeOfType<JsInteropNativeBridge>();
        loggerFactory.Messages.Should().Contain(m => m.Contains("JsInteropNativeBridge"));
    }

    [Fact]
    public void Create_WhenNativeTransportAbsent_SelectsStub_AndLogs()
    {
        var js = new Mock<IJSInProcessRuntime>();
        js.Setup(r => r.Invoke<bool>(NativeBridgeFactory.HasNativeProbe, It.IsAny<object?[]>()))
            .Returns(false);
        var loggerFactory = new CapturingLoggerFactory();

        var bridge = NativeBridgeFactory.Create(js.Object, loggerFactory);

        bridge.Should().BeOfType<StubNativeBridge>();
        loggerFactory.Messages.Should().Contain(m => m.Contains("StubNativeBridge"));
    }

    [Fact]
    public void Create_WhenRuntimeNotInProcess_FailsSafeToStub()
    {
        var js = new Mock<IJSRuntime>(); // not IJSInProcessRuntime -> cannot probe synchronously
        var loggerFactory = new CapturingLoggerFactory();

        var bridge = NativeBridgeFactory.Create(js.Object, loggerFactory);

        bridge.Should().BeOfType<StubNativeBridge>();
        loggerFactory.Messages.Should().Contain(m => m.Contains("not in-process"));
    }

    [Fact]
    public void Create_WhenProbeThrows_FailsSafeToStub()
    {
        var js = new Mock<IJSInProcessRuntime>();
        js.Setup(r => r.Invoke<bool>(NativeBridgeFactory.HasNativeProbe, It.IsAny<object?[]>()))
            .Throws(new JSException("probe failed"));
        var loggerFactory = new CapturingLoggerFactory();

        var bridge = NativeBridgeFactory.Create(js.Object, loggerFactory);

        bridge.Should().BeOfType<StubNativeBridge>();
    }

    /// <summary>Minimal in-memory <see cref="ILoggerFactory"/> capturing formatted messages.</summary>
    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        public List<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void AddProvider(ILoggerProvider provider)
        {
            // No external providers in tests.
        }

        public void Dispose()
        {
            // Nothing to dispose.
        }

        private sealed class CapturingLogger : ILogger
        {
            private readonly List<string> _messages;

            public CapturingLogger(List<string> messages) => _messages = messages;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => _messages.Add(formatter(state, exception));
        }
    }
}
