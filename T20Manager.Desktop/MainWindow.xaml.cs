using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using T20Manager.Desktop.Services;

namespace T20Manager.Desktop;

public partial class MainWindow : Window
{
    private const string RepoAtualizacoes = "https://github.com/SEU_USUARIO/SEU_REPOSITORIO";

    public MainWindow()
    {
        InitializeComponent();

        var pastaFichas = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "T20Manager", "Fichas");
        Directory.CreateDirectory(pastaFichas);

        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();
        services.AddSingleton(new FichaRepositorio(pastaFichas));
        services.AddSingleton<CombateService>();
        services.AddSingleton(new AtualizacaoService(RepoAtualizacoes));
        Resources.Add("services", services.BuildServiceProvider());
    }
}