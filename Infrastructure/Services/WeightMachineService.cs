using System.IO.Ports;
using System.Text.RegularExpressions;

namespace ConceptJewelleryPro.Infrastructure.Services;

/// <summary>
/// Reads live weight data from an electronic weighing scale over a serial (COM) port.
/// The service continuously listens for lines containing the configured stable prefix
/// (e.g. "ST,GS") and fires <see cref="WeightReceived"/> with the parsed decimal value.
/// </summary>
public sealed partial class WeightMachineService : IDisposable
{
    // ── Settings (populated by Start) ───────────────────────────────
    private string _commPort      = "COM1";
    private int    _baudRate      = 9600;
    private int    _dataBits      = 8;
    private StopBits   _stopBits  = StopBits.One;
    private Parity     _parity    = Parity.None;
    private Handshake  _handshake = Handshake.None;
    private string _stablePrefix  = "ST,GS";
    private int    _timeoutMs     = 500;

    private SerialPort? _port;
    private bool        _running;
    private readonly object _lock = new();

    // ── Public state ─────────────────────────────────────────────────
    public decimal LiveWeight   { get; private set; }
    public bool    IsConnected  { get; private set; }
    public bool    IsStable     { get; private set; }
    public string  StatusText   { get; private set; } = "Disconnected";

    /// <summary>Fires on the calling thread each time a new stable weight arrives.</summary>
    public event Action<decimal, bool>? WeightUpdated; // (weight, isStable)

    // ── Regex: pull first decimal number from a scale frame ──────────
    [GeneratedRegex(@"\d+\.?\d*")]
    private static partial Regex DecimalPattern();

    // ── Lifecycle ────────────────────────────────────────────────────

    /// <summary>
    /// Applies settings and opens the serial port.
    /// Call from a background thread or fire-and-forget from UI.
    /// </summary>
    public void Start(
        string commPort,
        string baudRate,
        string dataBits,
        string stopBits,
        string parity,
        string handshake,
        string stablePrefix,
        string timeoutMs)
    {
        Stop(); // close any existing port first

        _commPort     = string.IsNullOrWhiteSpace(commPort)     ? "COM1"  : commPort.Trim();
        _baudRate     = int.TryParse(baudRate,  out var br) ? br  : 9600;
        _dataBits     = int.TryParse(dataBits,  out var db) ? db  : 8;
        _timeoutMs    = int.TryParse(timeoutMs, out var to) ? to  : 500;
        _stablePrefix = string.IsNullOrWhiteSpace(stablePrefix) ? "ST,GS" : stablePrefix.Trim();

        _stopBits = stopBits?.Trim() switch
        {
            "1.5" => StopBits.OnePointFive,
            "2"   => StopBits.Two,
            _     => StopBits.One
        };

        _parity = parity?.Trim() switch
        {
            "Even"  => Parity.Even,
            "Odd"   => Parity.Odd,
            "Mark"  => Parity.Mark,
            "Space" => Parity.Space,
            _       => Parity.None
        };

        _handshake = handshake?.Trim() switch
        {
            "RtsCtsCom" => Handshake.RequestToSend,
            "XOnXOff"   => Handshake.XOnXOff,
            _           => Handshake.None
        };

        lock (_lock)
        {
            _running = true;
        }

        // Run IO on background thread so it never blocks UI
        Task.Run(ReadLoop);
    }

    public void Stop()
    {
        lock (_lock)
        {
            _running = false;
        }

        TryClosePort();
        IsConnected = false;
        IsStable    = false;
        StatusText  = "Disconnected";
        WeightUpdated?.Invoke(0m, false);
    }

    public void Dispose() => Stop();

    // ── Private ──────────────────────────────────────────────────────

    private void ReadLoop()
    {
        while (IsRunning())
        {
            try
            {
                OpenPort();

                while (IsRunning() && _port!.IsOpen)
                {
                    string? line = null;
                    try
                    {
                        line = _port.ReadLine();
                    }
                    catch (TimeoutException)
                    {
                        // Only notify if we were previously in a different state
                        if (IsStable || StatusText != "Waiting...")
                        {
                            UpdateState(LiveWeight, stable: false, "Waiting...");
                        }
                        continue;
                    }

                    if (line is null) continue;

                    bool isStable = line.Contains(_stablePrefix, StringComparison.OrdinalIgnoreCase);
                    var match = DecimalPattern().Match(line);
                    if (match.Success && decimal.TryParse(match.Value, out var weight))
                    {
                        UpdateState(weight, isStable,
                            isStable ? $"{weight:F3} g  ✔ Stable" : $"{weight:F3} g  ⚠ Unstable");
                    }
                    else
                    {
                        UpdateState(LiveWeight, isStable, isStable ? "Stable" : "Waiting...");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateState(0m, false, $"Error: {ex.Message}");
                TryClosePort();

                // Back-off before retry
                Thread.Sleep(2000);
            }
        }
    }

    private void OpenPort()
    {
        TryClosePort();

        _port = new SerialPort(_commPort, _baudRate, _parity, _dataBits, _stopBits)
        {
            Handshake       = _handshake,
            ReadTimeout     = _timeoutMs,
            WriteTimeout    = _timeoutMs,
            NewLine         = "\r\n",
            DtrEnable       = true,
            RtsEnable       = _handshake == Handshake.None
        };

        _port.Open();
        IsConnected = true;
        StatusText  = "Connected — waiting...";
        WeightUpdated?.Invoke(LiveWeight, false);
    }

    private void TryClosePort()
    {
        try { _port?.Close(); } catch { /* ignore */ }
        try { _port?.Dispose(); } catch { /* ignore */ }
        _port = null;
    }

    private long _lastUpdateTimeTicks;

    private void UpdateState(decimal weight, bool stable, string status)
    {
        // Guard against duplicate state updates
        if (LiveWeight == weight && IsStable == stable && StatusText == status)
            return;

        var now = Environment.TickCount64;
        // If only weight fluctuated and connection/stability status hasn't changed, throttle to 10 Hz (100ms)
        if (IsStable == stable && StatusText == status && (now - _lastUpdateTimeTicks < 100))
            return;

        _lastUpdateTimeTicks = now;
        LiveWeight  = weight;
        IsStable    = stable;
        StatusText  = status;
        WeightUpdated?.Invoke(weight, stable);
    }

    private bool IsRunning()
    {
        lock (_lock) { return _running; }
    }
}
