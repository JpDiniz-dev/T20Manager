using Velopack;
using Velopack.Sources;

namespace T20Manager.Desktop.Services;

public class AtualizacaoService
{
    private readonly UpdateManager _mgr;
    private UpdateInfo? _pendente;

    public AtualizacaoService(string repoUrl) =>
        _mgr = new UpdateManager(new GithubSource(repoUrl, string.Empty, false));

    public bool Instalado => _mgr.IsInstalled;

    public string VersaoNova => _pendente?.TargetFullRelease.Version.ToString() ?? "";

    public async Task<bool> VerificarAsync()
    {
        if (!_mgr.IsInstalled) return false;
        _pendente = await _mgr.CheckForUpdatesAsync();
        return _pendente != null;
    }

    public async Task InstalarAsync()
    {
        if (_pendente == null) return;
        await _mgr.DownloadUpdatesAsync(_pendente);
        _mgr.ApplyUpdatesAndRestart(_pendente);
    }
}
