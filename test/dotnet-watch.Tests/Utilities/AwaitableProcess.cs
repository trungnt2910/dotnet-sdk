// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Threading.Tasks.Dataflow;

namespace Microsoft.DotNet.Watch.UnitTests
{
    internal class AwaitableProcess(DotnetCommand spec, ITestOutputHelper logger) : IDisposable
    {
        // cancel just before we hit timeout used on CI (XUnitWorkItemTimeout value in sdk\test\UnitTests.proj)
        private static readonly TimeSpan s_timeout = Environment.GetEnvironmentVariable("HELIX_WORK_ITEM_TIMEOUT") is { } value
            ? TimeSpan.Parse(value).Subtract(TimeSpan.FromSeconds(10)) : TimeSpan.FromMinutes(1);

        private readonly object _testOutputLock = new();

        private readonly DotnetCommand _spec = spec;
        private readonly List<string> _lines = [];
        private readonly BufferBlock<string> _source = new();
        private Process _process;
        private bool _disposed;

        public IEnumerable<string> Output => _lines;
        public int Id => _process.Id;
        public Process Process => _process;

        public void Start()
        {
            if (_process != null)
            {
                throw new InvalidOperationException("Already started");
            }

            var processStartInfo = _spec.GetProcessStartInfo();
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.StandardOutputEncoding = Encoding.UTF8;
            processStartInfo.StandardErrorEncoding = Encoding.UTF8;

            _process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = processStartInfo,
            };

            _process.OutputDataReceived += OnData;
            _process.ErrorDataReceived += OnData;
            _process.Exited += OnExit;

            WriteTestOutput($"{DateTime.Now}: starting process: '{_process.StartInfo.FileName} {_process.StartInfo.Arguments}'");
            _process.Start();
            _process.BeginErrorReadLine();
            _process.BeginOutputReadLine();
            WriteTestOutput($"{DateTime.Now}: process started: '{_process.StartInfo.FileName} {_process.StartInfo.Arguments}'");
        }

        public void ClearOutput()
            => _lines.Clear();

        public async Task<string> GetOutputLineAsync(Predicate<string> success, Predicate<string> failure)
        {
            using var cancellationOnFailure = new CancellationTokenSource();

            if (!Debugger.IsAttached)
            {
                cancellationOnFailure.CancelAfter(s_timeout);
            }

            var failedLineCount = 0;
            while (!_source.Completion.IsCompleted && failedLineCount == 0)
            {
                try
                {
                    while (await _source.OutputAvailableAsync(cancellationOnFailure.Token))
                    {
                        var line = await _source.ReceiveAsync(cancellationOnFailure.Token);
                        _lines.Add(line);
                        if (success(line))
                        {
                            return line;
                        }

                        if (failure(line))
                        {
                            if (failedLineCount == 0)
                            {
                                // Limit the time to collect remaining output after a failure to avoid hangs:
                                cancellationOnFailure.CancelAfter(TimeSpan.FromSeconds(1));
                            }

                            if (failedLineCount > 100)
                            {
                                break;
                            }

                            failedLineCount++;
                        }
                    }
                }
                catch (OperationCanceledException) when (failedLineCount > 0)
                {
                    break;
                }
            }

            return null;
        }

        public async Task<IList<string>> GetAllOutputLinesAsync(CancellationToken cancellationToken)
        {
            var lines = new List<string>();
            while (!_source.Completion.IsCompleted)
            {
                while (await _source.OutputAvailableAsync(cancellationToken))
                {
                    lines.Add(await _source.ReceiveAsync(cancellationToken));
                }
            }
            return lines;
        }

        private void OnData(object sender, DataReceivedEventArgs args)
        {
            var line = args.Data ?? string.Empty;
            if (line.StartsWith("\x1b]"))
            {
                // strip terminal logger progress indicators from line
                line = line.StripTerminalLoggerProgressIndicators();
            }

            WriteTestOutput(line);
            _source.Post(line);
        }

        private void WriteTestOutput(string text)
        {
            lock (_testOutputLock)
            {
                if (!_disposed)
                {
                    logger.WriteLine(text);
                }
            }
        }

        private void OnExit(object sender, EventArgs args)
        {
            // Wait to ensure the process has exited and all output consumed
            _process.WaitForExit();

            // Signal test methods waiting on all process output to be completed:
            _source.Complete();
        }

        public void Dispose()
        {
            _source.Complete();

            lock (_testOutputLock)
            {
                _disposed = true;
            }

            if (_process == null)
            {
                return;
            }

            _process.ErrorDataReceived -= OnData;
            _process.OutputDataReceived -= OnData;

            try
            {
                _process.CancelErrorRead();
            }
            catch
            {
            }

            try
            {
                _process.CancelOutputRead();
            }
            catch
            {
            }

            try
            {
                _process.Kill(entireProcessTree: false);
            }
            catch
            {
            }

            _process.Dispose();
            _process = null;
        }
    }
}
