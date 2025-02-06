using System;
using System.Diagnostics;

namespace LANCommander.SDK;

public class FileTransferMonitor : IDisposable
{
    private readonly Stopwatch _stopwatch;

    private long _lastBytesTransferred;
    private long _totalBytes;
    private double _smoothedTransferRate;
    
    private const double SmoothingFactor = 0.1;
    private const double RateLimit = 0.5;

    public FileTransferMonitor(long totalBytes)
    {
        _stopwatch = Stopwatch.StartNew();
        _totalBytes = totalBytes;
    }

    public bool CanUpdate()
    {
        return _stopwatch.Elapsed.TotalSeconds > RateLimit;
    }

    public void Update(long bytesTransferred)
    {
        if (!_stopwatch.IsRunning)
            return;
        
        var bytesSinceLastUpdate = bytesTransferred - _lastBytesTransferred;
        var currentSpeed = bytesSinceLastUpdate / _stopwatch.Elapsed.TotalSeconds;
        
        if (_smoothedTransferRate == 0)
            _smoothedTransferRate = currentSpeed;
        else
            _smoothedTransferRate = (_smoothedTransferRate * (1 - SmoothingFactor)) + (currentSpeed * SmoothingFactor);
        
        _lastBytesTransferred = bytesTransferred;
        _stopwatch.Restart();
    }

    public long GetBytesTransferred() => _lastBytesTransferred;
    public long GetSpeed() => (long)_smoothedTransferRate;

    public TimeSpan GetTimeRemaining()
    {
        if (_smoothedTransferRate <= 0)
            return TimeSpan.Zero;

        double secondsRemaining;
        
        var remainingBytes = _totalBytes - _lastBytesTransferred;

        if (_smoothedTransferRate > 0)
            secondsRemaining = remainingBytes / _smoothedTransferRate;
        else
            secondsRemaining = 0;
        
        return TimeSpan.FromSeconds(secondsRemaining);
    }
    
    public void Dispose()
    {
        //_stopwatch.Stop();
    }
}