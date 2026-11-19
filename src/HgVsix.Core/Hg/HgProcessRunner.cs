using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HgVsix.Core.Hg
{
    // Runs read-only hg commands without a console window and with plain output
    public sealed class HgProcessRunner
    {
        private readonly Func<string> _executableProvider;
        private readonly TimeSpan _timeout;
        private readonly int _maxConcurrencyPerRepository;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        public HgProcessRunner(Func<string> executableProvider, TimeSpan timeout, int maxConcurrencyPerRepository = 2)
        {
            _executableProvider = executableProvider ?? throw new ArgumentNullException(nameof(executableProvider));
            _timeout = timeout;
            _maxConcurrencyPerRepository = Math.Max(1, maxConcurrencyPerRepository);
        }

        public event Action<string>? Log;

        // Stops hg and sets OutputLimitExceeded when stdout grows beyond maxOutputBytes
        public async Task<HgResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken, long maxOutputBytes = long.MaxValue)
        {
            var gate = _gates.GetOrAdd(workingDirectory, _ => new SemaphoreSlim(_maxConcurrencyPerRepository));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await RunCoreAsync(workingDirectory, arguments, cancellationToken, maxOutputBytes).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<HgResult> RunCoreAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken, long maxOutputBytes)
        {
            var executable = _executableProvider();
            var allArguments = new List<string> { "--noninteractive" };
            allArguments.AddRange(arguments);
            var commandLine = BuildCommandLine(allArguments);

            var startInfo = new ProcessStartInfo(executable, commandLine)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.UTF8,
            };
            startInfo.EnvironmentVariables["HGPLAIN"] = "1";
            startInfo.EnvironmentVariables["HGENCODING"] = "utf-8";

            using var process = new Process { StartInfo = startInfo };
            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (!process.Start())
                {
                    throw new HgException($"Could not start {executable}");
                }
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
            {
                throw new HgException($"Could not start {executable}: {ex.Message}", ex);
            }

            process.StandardInput.Close();

            using var timeoutSource = new CancellationTokenSource(_timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
            using var registration = linked.Token.Register(() => TryKill(process));

            var output = new MemoryStream();
            var outputTask = CopyLimitedAsync(process.StandardOutput.BaseStream, output, maxOutputBytes);
            var errorTask = process.StandardError.ReadToEndAsync();
            var limitExceeded = false;

            try
            {
                if (!await outputTask.ConfigureAwait(false))
                {
                    limitExceeded = true;
                    output.SetLength(0);
                    TryKill(process);
                }

                await errorTask.ConfigureAwait(false);
            }
            catch (Exception) when (linked.IsCancellationRequested)
            {
                // Streams break when the process is killed
            }

            await Task.Run(() => process.WaitForExit()).ConfigureAwait(false);

            if (linked.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new HgException($"hg {string.Join(" ", arguments)} timed out after {_timeout.TotalSeconds:0} s");
            }

            var error = errorTask.Status == TaskStatus.RanToCompletion ? errorTask.Result : string.Empty;
            var result = new HgResult(process.ExitCode, output.ToArray(), error, limitExceeded);
            Log?.Invoke($"hg {commandLine} [{workingDirectory}] exit {result.ExitCode} in {stopwatch.ElapsedMilliseconds} ms" +
                (limitExceeded ? " (output limit exceeded)" : string.Empty));
            return result;
        }

        // False when the source holds more than limit bytes. Nothing beyond the limit is buffered.
        private static async Task<bool> CopyLimitedAsync(Stream source, MemoryStream target, long limit)
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
            {
                if (target.Length + read > limit)
                {
                    return false;
                }

                target.Write(buffer, 0, read);
            }

            return true;
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (Exception)
            {
                // Already exited
            }
        }

        // Quotes arguments using the rules of CommandLineToArgvW
        public static string BuildCommandLine(IEnumerable<string> arguments)
        {
            var builder = new StringBuilder();
            foreach (var argument in arguments)
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                AppendQuoted(builder, argument);
            }

            return builder.ToString();
        }

        private static void AppendQuoted(StringBuilder builder, string argument)
        {
            if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0)
            {
                builder.Append(argument);
                return;
            }

            builder.Append('"');
            var backslashes = 0;
            foreach (var c in argument)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (c == '"')
                {
                    builder.Append('\\', backslashes * 2 + 1);
                }
                else
                {
                    builder.Append('\\', backslashes);
                }

                backslashes = 0;
                builder.Append(c);
            }

            builder.Append('\\', backslashes * 2);
            builder.Append('"');
        }
    }
}
