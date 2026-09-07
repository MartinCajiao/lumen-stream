using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using Lumen.Core.Client;
using Lumen.Core.Discovery;
using Lumen.Core.Display;
using Lumen.Core.Host;
using Lumen.Core.Overlay;
using Lumen.Core.Paths;
using Lumen.Core.Quality;
using Lumen.Core.Runtime;
using Lumen.Core.Settings;
using Lumen.Core.Wan;
using Lumen.Core.Wan.Relay;

namespace Lumen.Launcher;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IDisplayProbe _probe;
    private readonly HostLogTail _logTail = new();
    private LumenSettings _settings;
    private CancellationTokenSource? _announceCts;
    private Process? _hostProcess;
    private bool _shareOn;
    private bool _relaunchBusy;
    private int _relaunchs;
    private int _shareEpoch;
    private readonly AccountStore _accounts = new();
    private string _status = "";
    private string _loginUser = "";
    private string _loginPassword = "";
    private string _loginPasswordConfirm = "";
    private bool _authIsCreate = true;
    private string _pin = "";
    private string _remoteAddress = "";
    private string _publicIp = "";
    private string _shareCode = "";
    private string _wanMethod = "Esta wifi";
    private bool _pairingBusy;
    private bool _showTailscaleHelp;
    private bool _tailscaleBusy;
    private RelayHostClient? _relayHost;
    private RelayClientTunnel? _relayTunnel;
    private bool _showShareOptions;
    private bool _isInstalling;
    private bool _showAdvanced;
    private StreamStats _stats;

    public MainViewModel(IDisplayProbe? probe = null)
    {
        _probe = probe ?? new WindowsDisplayProbe();
        _settings = LumenSettingsStore.Load();
        if (string.IsNullOrWhiteSpace(_settings.Username) || !_accounts.Exists(_settings.Username))
        {
            _settings.Username = "";
            _authIsCreate = !_accounts.Any();
        }
        else
        {
            _authIsCreate = false;
        }

        _loginUser = _settings.Username;
        RefreshDisplay();
        _stats = OverlayAdvisor.Compose(CurrentProfile, null, null);
        HostBinary = ProcessLocator.FindHost();
        ClientBinary = ProcessLocator.FindClient();
        RebuildComputers();
        RefreshFeatures();

        // No login wall: if there's no local name yet, pick one and enter straight in.
        // The account/password screen was friction with no value for a single-user launcher.
        if (!IsLoggedIn)
        {
            var defaultName = string.IsNullOrWhiteSpace(Environment.MachineName) ? "yo" : Environment.MachineName;
            EnterSession(defaultName);
            return;
        }

        Status = FeatureChecker.Headline(Features);
        if (IsLoggedIn)
        {
            AdoptRunningHost();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ComputerItem> Computers { get; } = [];
    public ObservableCollection<FeatureCheck> Features { get; } = [];

    public LocatedBinary? HostBinary { get; private set; }
    public LocatedBinary? ClientBinary { get; private set; }
    public DisplayInfo Display { get; private set; } = DisplayInfo.Fallback;
    public StreamProfile CurrentProfile => StreamProfile.From(_settings, Display);

    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(_settings.Username);
    public string Greeting => IsLoggedIn ? $"Hola, {_settings.Username}" : "Lumen";
    public string Initial => IsLoggedIn ? char.ToUpperInvariant(_settings.Username[0]).ToString() : "L";
    public bool IsSharing => _shareOn;
    public string ShareLabel => _shareOn ? "Dejar de compartir" : "Compartir este PC";
    public string Headline => FeatureChecker.Headline(Features.ToList());

    public StreamStats Stats
    {
        get => _stats;
        private set => Set(ref _stats, value);
    }

    public string OverlayLine => Stats.OverlayLine;
    public string? OverlayWarning => Stats.Warning;

    public string Status
    {
        get => _status;
        set => Set(ref _status, value);
    }

    public string LoginUser
    {
        get => _loginUser;
        set => Set(ref _loginUser, value);
    }

    public string LoginPassword
    {
        get => _loginPassword;
        set => Set(ref _loginPassword, value);
    }

    public string LoginPasswordConfirm
    {
        get => _loginPasswordConfirm;
        set => Set(ref _loginPasswordConfirm, value);
    }

    public bool AuthIsCreate
    {
        get => _authIsCreate;
        set
        {
            Set(ref _authIsCreate, value);
            OnPropertyChanged(nameof(AuthTitle));
            OnPropertyChanged(nameof(AuthButton));
            OnPropertyChanged(nameof(AuthSwitch));
        }
    }

    public string AuthTitle => AuthIsCreate ? "Crear cuenta" : "Iniciar sesión";
    public string AuthButton => AuthIsCreate ? "Crear cuenta" : "Entrar";
    public string AuthSwitch => AuthIsCreate ? "Ya tengo cuenta" : "Crear cuenta";

    public string Pin
    {
        get => _pin;
        set
        {
            Set(ref _pin, value);
            if (_pin.Trim().Length == 4 && _pin.Trim().All(char.IsDigit))
            {
                _ = PairAsync();
            }
        }
    }

    public string RemoteAddress
    {
        get => _remoteAddress;
        set => Set(ref _remoteAddress, value);
    }

    public string RelayServer
    {
        get => _settings.Wan.RelayServer;
        set
        {
            var trimmed = (value ?? "").Trim();
            if (trimmed == _settings.Wan.RelayServer)
            {
                return;
            }

            _settings.Wan = _settings.Wan with { RelayServer = trimmed };
            LumenSettingsStore.Save(_settings);
            OnPropertyChanged(nameof(RelayServer));
        }
    }

    public string LocalIp => NetworkAddresses.LocalIpv4() ?? "esta red";
    public string PublicIp => string.IsNullOrWhiteSpace(_publicIp) ? "detectando…" : _publicIp;
    public string? TailscaleIp => NetworkAddresses.TailscaleIpv4();
    public string ShareCode => string.IsNullOrWhiteSpace(_shareCode) ? (TailscaleIp ?? LocalIp) : _shareCode;
    public string ShareHelp => WanBootstrap.ShareHint(_wanMethod);

    public bool ShowTailscaleHelp
    {
        get => _showTailscaleHelp;
        set => Set(ref _showTailscaleHelp, value);
    }

    public bool TailscaleBusy
    {
        get => _tailscaleBusy;
        set
        {
            Set(ref _tailscaleBusy, value);
            OnPropertyChanged(nameof(TailscaleButtonLabel));
        }
    }

    public string TailscaleButtonLabel =>
        _tailscaleBusy
            ? "Bajando Tailscale…"
            : TailscaleHelper.IsInstalled
                ? "Abrir Tailscale"
                : "Instalar Tailscale (gratis)";

    public async Task InstallTailscaleAsync()
    {
        if (_tailscaleBusy)
        {
            return;
        }

        var binary = TailscaleHelper.FindBinary();
        if (binary is not null && !TailscaleHelper.IsConnected)
        {
            var gui = Path.Combine(Path.GetDirectoryName(binary)!, "tailscale-ipn.exe");
            Process.Start(new ProcessStartInfo
            {
                FileName = File.Exists(gui) ? gui : binary,
                UseShellExecute = true
            });
            Status = "Abre Tailscale, entra con tu cuenta y repite en el otro PC. Cuando conecte, el código cambia solo.";
            return;
        }

        TailscaleBusy = true;
        try
        {
            var progress = new Progress<string>(m => Status = m);
            await TailscaleHelper.DownloadAndLaunchInstallerAsync(progress, CancellationToken.None).ConfigureAwait(true);
            Status = "Instala Tailscale y entra con tu cuenta. Hazlo también en el otro PC (misma cuenta). Cuando conecte, el código cambia solo a 100.x.";
        }
        catch (Exception ex)
        {
            Status = $"No pude bajar Tailscale: {ex.Message}. Bájalo de tailscale.com en las dos PCs.";
        }
        finally
        {
            TailscaleBusy = false;
        }
    }

    public bool ShowAdvanced
    {
        get => _showAdvanced;
        set => Set(ref _showAdvanced, value);
    }

    public bool PrivacyMode
    {
        get => _settings.PrivacyMode;
        set
        {
            _settings.PrivacyMode = value;
            Persist();
        }
    }

    public bool WacomPressureTilt
    {
        get => _settings.WacomPressureTilt;
        set
        {
            _settings.WacomPressureTilt = value;
            Persist();
        }
    }

    public QualityMode Quality
    {
        get => _settings.Quality;
        set
        {
            _settings.Quality = value;
            Persist();
        }
    }

    public IReadOnlyList<FpsPreset> FpsOptions { get; } = FpsPresetExtensions.All;

    public bool ShowShareOptions
    {
        get => _showShareOptions;
        set => Set(ref _showShareOptions, value);
    }

    public bool IsInstalling
    {
        get => _isInstalling;
        set => Set(ref _isInstalling, value);
    }

    public FpsPreset Fps
    {
        get => _settings.Fps;
        set
        {
            _settings.Fps = value;
            Persist();
        }
    }

    public bool AutoShareOnBoot
    {
        get => _settings.AutoShareOnBoot;
        set
        {
            _settings.AutoShareOnBoot = value;
            UserSessionHost.SetEnabled(value, Environment.ProcessPath ?? AppContext.BaseDirectory);
            Persist();
        }
    }

    public string HostName => _settings.HostName;

    public void RefreshDisplay()
    {
        Display = _probe.Primary;
        OnPropertyChanged(nameof(Display));
    }

    public void TickOverlay()
    {
        _logTail.Poll();
        Stats = _logTail.ToStats(CurrentProfile);
        OnPropertyChanged(nameof(OverlayLine));
        OnPropertyChanged(nameof(OverlayWarning));
        if (_shareOn && HostProcess.IsLive())
        {
            _relaunchs = 0;
        }
        else if (_shareOn && !_relaunchBusy && !HostProcess.IsLive() && !HostProcess.IsRunning(_hostProcess))
        {
            _ = KeepShareAliveAsync();
        }

        if (ShowTailscaleHelp && TailscaleHelper.IsConnected)
        {
            ShowTailscaleHelp = false;
            if (_shareOn)
            {
                var ts = TailscaleIp;
                if (!string.IsNullOrWhiteSpace(ts) && _shareCode != ts)
                {
                    _shareCode = ts;
                    _wanMethod = "Tailscale";
                    Status = $"Tailscale conectó. Nuevo código para otra casa: {ts}";
                    NotifyShare();
                }
            }
        }

        OnPropertyChanged(nameof(IsSharing));
        OnPropertyChanged(nameof(ShareLabel));
    }

    public void SubmitAuth()
    {
        var result = AuthIsCreate
            ? _accounts.Create(LoginUser, LoginPassword, LoginPasswordConfirm)
            : _accounts.SignIn(LoginUser, LoginPassword);

        if (!result.Ok)
        {
            Status = result.Error ?? "No se pudo.";
            return;
        }

        EnterSession(result.Username!);
    }

    public void ToggleAuthMode()
    {
        AuthIsCreate = !AuthIsCreate;
        LoginPassword = "";
        LoginPasswordConfirm = "";
        Status = AuthIsCreate ? "Crea tu cuenta. Usuario y contraseña, sin email." : "Entra con tu usuario y contraseña.";
    }

    private void EnterSession(string name)
    {
        _settings.Username = name;
        _settings.HostName = $"{name} · {Environment.MachineName}";
        _settings.Fps = FpsPreset.Native;
        _settings.UseNativeResolution = true;
        LumenSettingsStore.Save(_settings);
        LoginPassword = "";
        LoginPasswordConfirm = "";
        ApplyConfigsQuiet();
        RefreshFeatures();
        RebuildComputers();
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(Greeting));
        OnPropertyChanged(nameof(Initial));
        OnPropertyChanged(nameof(HostName));
        Status = Headline;
        _ = ScanAsync();
        _ = EnsureReadyAsync(needHost: true, needClient: false);
        AdoptRunningHost();
    }

    public void Logout()
    {
        StopHost();
        // No login wall anymore: dropping the session just re-enters with a fresh
        // default name. The password screen is gone for good.
        var freshName = (Environment.MachineName + "-" + Random.Shared.Next(100, 999)).Trim();
        EnterSession(freshName);
    }

    public Task ToggleShareAsync()
    {
        if (_shareOn)
        {
            StopHost();
            return Task.CompletedTask;
        }

        return ConfirmShareAsync();
    }

    public void ToggleShare() => _ = ToggleShareAsync();

    public void CancelShareOptions() => ShowShareOptions = false;

    public async Task ConfirmShareAsync()
    {
        ShowShareOptions = false;
        if (!await EnsureReadyAsync(needHost: true, needClient: false).ConfigureAwait(true))
        {
            return;
        }

        await StartHostAsync().ConfigureAwait(true);
        NotifyShare();
    }

    public async Task ConnectToAsync(ComputerItem item)
    {
        if (item.IsThisPc)
        {
            Status = "Ese eres tú. Pulsa Compartir este PC.";
            return;
        }

        if (item.State == PcPowerState.Off)
        {
            Status = $"{item.Title} no está disponible. Enciéndelo y pulsa Compartir en ese PC.";
            return;
        }

        await ConnectAddressAsync(item.Address, item.Title).ConfigureAwait(true);
    }

    public async Task ConnectTypedAsync()
    {
        if (string.IsNullOrWhiteSpace(RemoteAddress))
        {
            Status = "Pega el código del PC gamer y pulsa Conectar.";
            return;
        }

        AddRemoteComputer();
        var (host, _) = ParseRemote(RemoteAddress);
        await ConnectAddressAsync(host, host).ConfigureAwait(true);
    }

    private async Task ConnectAddressAsync(string host, string title)
    {
        if (!await EnsureReadyAsync(needHost: false, needClient: true).ConfigureAwait(true))
        {
            return;
        }

        // A relay code (relay:123456@host:47991) means the host is behind CGNAT and
        // we reach it through a Lumen relay. We open a local tunnel and point
        // Moonlight at 127.0.0.1 — no overlay app, no inbound port needed here.
        var relayCode = RelayCode.TryParse(host);
        if (relayCode is not null)
        {
            await ConnectViaRelayAsync(relayCode, title).ConfigureAwait(true);
            return;
        }

        Status = "Llamando al otro PC…";
        var known = KnownComputerStore.Load();
        var pc = known.FirstOrDefault(k => k.Address == host);
        var port = pc?.Port ?? CurrentProfile.Wan.HostPort;
        var probe = await HostProbe.CheckAsync(host, port, CancellationToken.None)
            .ConfigureAwait(true);
        if (!probe.Reachable)
        {
            if (NetworkAddresses.IsTailscaleIpv4(host) && !TailscaleHelper.IsConnected)
            {
                ShowTailscaleHelp = true;
            }

            Status = probe.Message;
            return;
        }

        ApplyConfigsQuiet();
        var client = ProcessLocator.FindClient();
        if (client is null)
        {
            Status = "Falta el programa para entrar. Espera un momento y pulsa Conectar otra vez.";
            return;
        }

        var pairFirst = pc is null;
        var pairingUnknown = false;
        if (pc is not null)
        {
            var paired = await MoonlightPairingProbe.IsPairedAsync(host, port, CancellationToken.None)
                .ConfigureAwait(true);
            if (paired is null)
            {
                pairingUnknown = true;
                pairFirst = !pc.ReadyToStream;
            }
            else
            {
                pairFirst = !paired.Value;
            }
        }

        try
        {
            SessionLauncher.StartClient(CurrentProfile, client, host, pairOnly: pairFirst);
            if (pairingUnknown && pc is not null)
            {
                pc.ReadyToStream = true;
                KnownComputerStore.Save(known);
            }

            Status = pairFirst
                ? "En Moonlight sale un PIN. Escríbelo en el PC gamer (el que pulsó Compartir) y pulsa Listo. Luego aquí pulsa Conectar otra vez."
                : $"Entrando a {title}…";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    /// <summary>
    /// Connects to a host that published a relay code. Opens a local tunnel for every
    /// GameStream port and points Moonlight at 127.0.0.1. Pairing (PIN) and the video
    /// stream both go through the tunnel, so this works from another house even when
    /// both ISPs use CGNAT — no Tailscale, no inbound port on either PC.
    /// </summary>
    private async Task ConnectViaRelayAsync(RelayCode code, string title)
    {
        if (!await EnsureReadyAsync(needHost: false, needClient: true).ConfigureAwait(true))
        {
            return;
        }

        ApplyConfigsQuiet();
        var client = ProcessLocator.FindClient();
        if (client is null)
        {
            Status = "Falta el programa para entrar. Espera un momento y pulsa Conectar otra vez.";
            return;
        }

        Status = "Conectando con el relay Lumen…";
        try
        {
            _relayTunnel?.Dispose();
            _relayTunnel = new RelayClientTunnel(code.Relay.Host, code.Relay.Port, code.Code);
            await _relayTunnel.StartAsync(CancellationToken.None).ConfigureAwait(true);

            // Tunnel every GameStream port to a local loopback port. Moonlight talks
            // to 127.0.0.1 and the relay carries it to the host's Apollo behind CGNAT.
            var tcpControl = _relayTunnel.ListenFor(47989);
            _relayTunnel.ListenFor(47984);
            _relayTunnel.ListenFor(47990);
            _relayTunnel.ListenForUdp(47998);
            _relayTunnel.ListenForUdp(47999);
            _relayTunnel.ListenForUdp(48000);
            _relayTunnel.ListenForUdp(48010);

            // Check pairing through the tunnel (127.0.0.1:tcpControl -> host 47989).
            var paired = await MoonlightPairingProbe.IsPairedAsync("127.0.0.1", tcpControl, CancellationToken.None)
                .ConfigureAwait(true);
            var pairFirst = paired is null || !paired.Value;

            SessionLauncher.StartClient(CurrentProfile, client, "127.0.0.1", pairOnly: pairFirst);
            Status = pairFirst
                ? "En Moonlight sale un PIN. Escríbelo en el PC gamer (el que pulsó Compartir) y pulsa Listo. Luego aquí pulsa Conectar otra vez."
                : $"Entrando a {title} (por relay)…";
        }
        catch (Exception ex)
        {
            Status = $"No se pudo cruzar el relay: {ex.Message}. Vuelve a compartir en el PC gamer y copia el código nuevo.";
        }
    }

    private (string Host, int Port) ParseRemote(string raw)
    {
        var host = raw.Trim();
        var port = CurrentProfile.Wan.HostPort;
        // A relay code (relay:123456@host:47991) is opaque: do not split it on ':'.
        if (RelayCode.IsRelayCode(host))
        {
            return (host, port);
        }

        if (host.Contains(':', StringComparison.Ordinal) && !host.StartsWith('[') && !IPAddress.TryParse(host, out _))
        {
            var parts = host.Split(':', 2);
            host = parts[0];
            if (int.TryParse(parts[1], out var parsed))
            {
                port = parsed;
            }
        }

        return (host, port);
    }

    public void StartHost() => _ = StartHostAsync();

    public async Task StartHostAsync()
    {
        var epoch = _shareEpoch;
        ApplyConfigsQuiet();
        RefreshBinaries();
        if (HostBinary is null)
        {
            Status = "Preparando este PC…";
            return;
        }

        try
        {
            Status = "Abriendo internet para que puedan entrar de fuera…";
            var stun = await StunClient.QueryPublicIpv4Async(CancellationToken.None).ConfigureAwait(true);
            if (epoch != _shareEpoch)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(stun))
            {
                _publicIp = stun;
                _settings.Wan = _settings.Wan with { ExternalIp = stun, EnableUpnp = true, EnableStun = true };
                LumenSettingsStore.Save(_settings);
            }

            Status = _relaunchBusy
                ? "Apollo se cerró. Lo vuelvo a abrir…"
                : "Este PC se está transformando en el stream…";
            _hostProcess = await SessionLauncher.StartHostAsync(
                    CurrentProfile,
                    HostBinary,
                    _settings.Username,
                    CancellationToken.None)
                .ConfigureAwait(true);
            if (epoch != _shareEpoch)
            {
                HostProcess.StopAll();
                _hostProcess = null;
                return;
            }

            BeginAnnounce();
            var wan = await WanBootstrap.OpenAsync(HostBinary.Path, CurrentProfile.Wan.HostPort, CancellationToken.None)
                .ConfigureAwait(true);
            if (epoch != _shareEpoch)
            {
                HostProcess.StopAll();
                _hostProcess = null;
                return;
            }

            _shareCode = wan.Address;
            _wanMethod = wan.Method;
            if (!string.IsNullOrWhiteSpace(wan.PublicIpv4))
            {
                _publicIp = wan.PublicIpv4;
            }

            // OpenAsync already classified the NAT. If the chosen code is the LAN
            // one (CGNAT/double NAT), try the autonomous relay first (no overlay app),
            // then fall back to the Tailscale card if no relay is configured.
            var natBlocked = wan.Method == "Esta wifi"
                && !string.IsNullOrWhiteSpace(wan.PublicIpv4)
                && !NetworkAddresses.IsPrivateIpv4(wan.PublicIpv4);
            if (natBlocked)
            {
                var relay = RelayEndpoint.TryParse(_settings.Wan.RelayServer);
                if (relay is not null)
                {
                    Status = "Tu internet tiene doble NAT. Conectando con el relay Lumen (sin instalar nada)…";
                    try
                    {
                        _relayHost?.Dispose();
                        _relayHost = new RelayHostClient(relay.Host, relay.Port, relay.Secret);
                        var relayCode = await _relayHost.ConnectAsync(CancellationToken.None).ConfigureAwait(true);
                        if (epoch != _shareEpoch)
                        {
                            _relayHost.Dispose();
                            _relayHost = null;
                            return;
                        }

                        _shareCode = new RelayCode(relayCode, relay).ToString();
                        _wanMethod = "Relay";
                        natBlocked = false;
                        Status = $"Listo para otra casa. Código: {_shareCode}. Va por el relay Lumen: no installs nada, cruza CGNAT.";
                    }
                    catch (Exception ex)
                    {
                        Status = $"El relay no conectó: {ex.Message}. Configúralo en Ajustes (relay = host:puerto:secreto).";
                    }
                }

                if (natBlocked)
                {
                    ShowTailscaleHelp = !TailscaleHelper.IsConnected;
                }
            }

            _ = RefreshPublicIpAsync();
            _shareOn = true;
            _relaunchs = 0;
            if (natBlocked)
            {
                Status = $"Listo en esta wifi. Código: {_shareCode}. Para la casa de tu tía: pon un relay Lumen en Ajustes, o instala Tailscale en las dos PCs. Tu internet tiene doble NAT y el código público nunca entra.";
            }
            else if (_wanMethod != "Relay")
            {
                Status = $"{(wan.Method == "Esta wifi" ? "Listo en esta wifi" : "Listo")}. Código: {wan.Address}. {WanBootstrap.ShareHint(wan.Method)}";
            }
            if (!wan.FirewallOk)
            {
                Status += " Aviso: Windows no dejó abrir el firewall (necesita permisos de administrador). Si nadie entra, cierra Lumen, ábrelo como administrador y comparte otra vez.";
            }

            NotifyShare();
        }
        catch (Exception ex)
        {
            if (epoch != _shareEpoch)
            {
                return;
            }

            _hostProcess = HostProcess.FindRunning();
            var live = HostProcess.IsLive();
            if (live)
            {
                _shareOn = true;
                Status = "Listo. En el otro PC pulsa Conectar.";
            }
            else if (_relaunchBusy)
            {
                Status = "Apollo se cerró. Lo vuelvo a abrir…";
            }
            else
            {
                _shareOn = false;
                Status = ex.Message;
            }

            NotifyShare();
        }
    }

    public void StopHost()
    {
        _shareEpoch++;
        _shareOn = false;
        _relaunchBusy = false;
        _relaunchs = 0;
        _shareCode = "";
        _wanMethod = "Esta wifi";
        _announceCts?.Cancel();
        _relayHost?.Dispose();
        _relayHost = null;
        HostProcess.StopAll();
        _hostProcess = null;
        Status = "Ya no se comparte este PC.";
        NotifyShare();
    }

    public async Task PairAsync()
    {
        if (_pairingBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Pin))
        {
            Status = "Escribe el PIN que ves en Moonlight en el otro PC.";
            return;
        }

        _pairingBusy = true;
        try
        {
            var ok = await PairingClient.SubmitPinAsync(Pin.Trim(), _settings.Username, CancellationToken.None);
            Status = ok
                ? "PIN correcto. En el otro PC pulsa Conectar otra vez."
                : "Ese PIN no vale o Apollo no está abierto aquí. El PIN se escribe en el PC gamer, el que comparte.";
        }
        catch (Exception ex)
        {
            Status = $"No pude emparejar: {ex.Message}";
        }
        finally
        {
            _pairingBusy = false;
        }
    }

    public void AddRemoteComputer()
    {
        var raw = RemoteAddress.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            Status = "Escribe el código que sale en el otro PC al compartir.";
            return;
        }

        var (host, port) = ParseRemote(raw);
        var known = KnownComputerStore.Load();
        var existing = known.FirstOrDefault(k => k.Address == host && k.Port == port);
        if (existing is null)
        {
            known.Add(new KnownComputer
            {
                Name = host,
                Address = host,
                Owner = host,
                Port = port,
                Manual = true,
                LastSeenUtc = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.Manual = true;
            existing.LastSeenUtc = DateTimeOffset.UtcNow;
        }

        KnownComputerStore.Save(known);
        RebuildComputers();
        Status = $"Añadido {host}. Pulsa Conectar.";
    }

    public async Task ScanAsync()
    {
        try
        {
            var found = await LanBeacon.ScanAsync(TimeSpan.FromSeconds(2), CancellationToken.None);
            var local = TryLocalIp();
            var remote = found.Where(h => h.Address != local).ToList();
            MergeKnown(remote);
            RebuildComputers(remote);
        }
        catch (Exception)
        {
            RebuildComputers();
        }
    }

    public void UseSharp()
    {
        Quality = QualityMode.SharpLan;
        Status = "Imagen nítida (texto y 4:4:4).";
    }

    public void UseGame()
    {
        Quality = QualityMode.Game;
        Status = "Modo juego (más fluido).";
    }

    private void ApplyConfigsQuiet()
    {
        var profile = CurrentProfile;
        SunshineConfigWriter.Write(profile);
        AppsJsonWriter.Write(profile);
        ClientSettingsApplier.Apply(profile);
        RefreshFeatures();
    }

    private void Persist()
    {
        LumenSettingsStore.Save(_settings);
        Stats = OverlayAdvisor.Compose(CurrentProfile, _logTail.LastCaptureHz, _logTail.LastRequestedFps);
        RefreshFeatures();
        OnPropertyChanged(nameof(PrivacyMode));
        OnPropertyChanged(nameof(WacomPressureTilt));
        OnPropertyChanged(nameof(Quality));
        OnPropertyChanged(nameof(Fps));
        OnPropertyChanged(nameof(AutoShareOnBoot));
        OnPropertyChanged(nameof(Headline));
    }

    private void RefreshFeatures()
    {
        Features.Clear();
        foreach (var check in FeatureChecker.Evaluate(CurrentProfile, HostBinary is not null, ClientBinary is not null))
        {
            Features.Add(check);
        }

        OnPropertyChanged(nameof(Headline));
    }

    private void RebuildComputers(IReadOnlyList<DiscoveredHost>? remote = null)
    {
        var ip = TryLocalIp() ?? "este PC";
        Computers.Clear();

        var known = KnownComputerStore.Load();
        if (remote is not null)
        {
            MergeKnown(remote);
            known = KnownComputerStore.Load();
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var pc in known.Where(k => k.Address != ip))
        {
            Computers.Add(ComputerItem.FromKnown(pc, now));
        }
    }

    private static void MergeKnown(IReadOnlyList<DiscoveredHost> remote)
    {
        var known = KnownComputerStore.Load();
        foreach (var host in remote)
        {
            var existing = known.FirstOrDefault(k => k.Address == host.Address && k.Port == host.Port);
            if (existing is null)
            {
                known.Add(new KnownComputer
                {
                    Name = host.Name,
                    Address = host.Address,
                    Owner = host.Owner,
                    Port = host.Port,
                    PanelHz = host.PanelHz,
                    LastSeenUtc = DateTimeOffset.UtcNow
                });
            }
            else
            {
                existing.Name = host.Name;
                existing.Owner = host.Owner;
                existing.PanelHz = host.PanelHz;
                existing.LastSeenUtc = DateTimeOffset.UtcNow;
            }
        }

        KnownComputerStore.Save(known);
    }

    private void RefreshBinaries()
    {
        HostBinary = ProcessLocator.FindHost();
        ClientBinary = ProcessLocator.FindClient();
        OnPropertyChanged(nameof(HostBinary));
        OnPropertyChanged(nameof(ClientBinary));
        RefreshFeatures();
    }

    public async Task<bool> EnsureReadyAsync(bool needHost = true, bool needClient = true)
    {
        RefreshBinaries();
        var hostOk = !needHost || HostBinary is not null;
        var clientOk = !needClient || ClientBinary is not null;
        if (hostOk && clientOk)
        {
            return true;
        }

        IsInstalling = true;
        Status = !hostOk && !clientOk
            ? "Un momento…"
            : !hostOk
                ? "Preparando este PC. Si Windows pide permiso, pulsa Sí…"
                : "Preparando la conexión…";
        var progress = new Progress<InstallProgress>(p => Status = p.Message);
        var error = await DependencyInstaller.EnsureAsync(progress, needHost: needHost, needClient: needClient)
            .ConfigureAwait(true);
        RefreshBinaries();
        IsInstalling = false;
        if (error is not null)
        {
            Status = error;
            return false;
        }

        hostOk = !needHost || HostBinary is not null;
        clientOk = !needClient || ClientBinary is not null;
        Status = hostOk && clientOk
            ? "Listo."
            : "No se pudo preparar. Cierra el antivirus un momento y reintenta.";
        return hostOk && clientOk;
    }

    private async Task KeepShareAliveAsync()
    {
        if (_relaunchBusy || !_shareOn)
        {
            return;
        }

        if (_relaunchs >= 8)
        {
            _shareOn = false;
            Status = "Apollo se cerró varias veces. Pulsa Compartir otra vez.";
            NotifyShare();
            return;
        }

        _relaunchBusy = true;
        _relaunchs++;
        try
        {
            Status = "Apollo se cerró. Lo vuelvo a abrir…";
            await StartHostAsync().ConfigureAwait(true);
        }
        finally
        {
            _relaunchBusy = false;
        }
    }

    private void AdoptRunningHost()
    {
        _hostProcess = HostProcess.FindRunning();
        if (!HostProcess.IsLive() && !HostProcess.IsRunning(_hostProcess))
        {
            return;
        }

        _shareOn = true;
        BeginAnnounce();
        _ = RefreshPublicIpAsync();
        NotifyShare();
        Status = $"Ya se está compartiendo. Código: {ShareCode}";
    }

    private void NotifyShare()
    {
        OnPropertyChanged(nameof(IsSharing));
        OnPropertyChanged(nameof(ShareLabel));
        OnPropertyChanged(nameof(ShareHelp));
        OnPropertyChanged(nameof(ShareCode));
        OnPropertyChanged(nameof(LocalIp));
        OnPropertyChanged(nameof(PublicIp));
        RebuildComputers();
    }

    private void BeginAnnounce()
    {
        _announceCts?.Cancel();
        _announceCts = new CancellationTokenSource();
        var ip = NetworkAddresses.LocalIpv4() ?? "127.0.0.1";
        LanBeacon.StartAnnouncing(
            new DiscoveredHost(Environment.MachineName, ip, CurrentProfile.Wan.HostPort, Display.RefreshHz, _settings.Username),
            _announceCts.Token);
    }

    private async Task RefreshPublicIpAsync()
    {
        var stun = await StunClient.QueryPublicIpv4Async(CancellationToken.None).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(stun))
        {
            _publicIp = stun;
            if (_settings.Wan.ExternalIp != stun)
            {
                _settings.Wan = _settings.Wan with { ExternalIp = stun, EnableUpnp = true, EnableStun = true };
                LumenSettingsStore.Save(_settings);
            }
        }
        else
        {
            _publicIp = TailscaleIp ?? LocalIp;
        }

        OnPropertyChanged(nameof(PublicIp));
        OnPropertyChanged(nameof(ShareCode));
        OnPropertyChanged(nameof(ShareHelp));
        OnPropertyChanged(nameof(TailscaleIp));
        if (IsSharing && string.IsNullOrWhiteSpace(_shareCode))
        {
            Status = $"Compartido. En el otro PC pulsa Conectar. Código: {ShareCode}";
        }
    }

    private static string? TryLocalIp() => NetworkAddresses.LocalIpv4();

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(name);
    }

    private void OnPropertyChanged(string? name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
