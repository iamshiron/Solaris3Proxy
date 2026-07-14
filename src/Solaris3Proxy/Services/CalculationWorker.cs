using Microsoft.Extensions.Options;
using Shiron.Solaris3Proxy.Models;
using Shiron.Solaris3Proxy.Options;

namespace Shiron.Solaris3Proxy.Services;

/// <summary>
/// Background service that runs a calculation on a fixed interval and publishes
/// each result to the <see cref="ICalculationStore"/> for HTTP endpoints to read.
/// </summary>
public sealed class CalculationWorker(
    ICalculationStore store,
    IOptions<CalculationOptions> options,
    ILogger<CalculationWorker> logger) : BackgroundService {
    private readonly TimeSpan _interval =
        TimeSpan.FromMilliseconds(Math.Max(1, options.Value.IntervalMilliseconds));

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        logger.LogInformation("Calculation worker started with interval {Interval}.", _interval);

        using var timer = new PeriodicTimer(_interval);
        var sequence = 0L;
        var sum = 0.0;

        try {
            do {
                var value = Compute(sequence);
                sum += value;

                store.Update(new CalculationSnapshot(
                    Sequence: sequence,
                    ComputedAt: DateTime.UtcNow,
                    Value: value,
                    MovingAverage: sum / (sequence + 1)));

                sequence++;
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        } catch (OperationCanceledException) {
            // Graceful shutdown — nothing to do.
        }

        logger.LogInformation("Calculation worker stopped after {Count} cycles.", sequence);
    }

    /// <summary>
    /// The actual work performed each cycle. Replace this with the real calculation.
    /// </summary>
    private static double Compute(long sequence) =>
        Math.Round(Math.Sin(sequence / 10.0) * 100.0, 4);
}
