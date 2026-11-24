using System.Globalization;
using System.Text.Json;
using T20Manager.Core.Models;

namespace T20Manager.App.Services;

/// <summary>Uma ficha carregada do disco. Id = caminho do arquivo dentro de Fichas/.</summary>
public record FichaEntrada(string Id, FichaT20 Ficha)
{
    /// <summary>"Ameaças de Arton/Dragões" (vazio se o arquivo está na raiz).</summary>
    public string Origem => Ficha.Origem is "." ? "" : Ficha.Origem;

    /// <summary>Primeira pasta da origem, usada como "livro" no filtro.</summary>
    public string Livro => Origem.Split('/')[0];

    public double Nd { get; } = FichaRepositorio.NdValor(Ficha.ND);
}

public class FichaRepositorio
{
    public IReadOnlyList<string> NDs =>
    Todas.Select(e => e.Ficha.ND).Where(n => !double.IsNaN(NdValor(n)))
         .Distinct().OrderBy(NdValor).ToList();

    public static double NdValor(string nd)
    {
        var t = (nd ?? "").Trim();
        if (t == "") return double.NaN;

        if (t.Contains('/'))
        {
            var p = t.Split('/');
            if (p.Length == 2 &&
                double.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a) &&
                double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b) && b != 0)
                return a / b;
        }

        if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return n;

        // "S" e "S+" ficam acima do ND 20 (confira se é isso nos seus dados)
        if (t.StartsWith("S", StringComparison.OrdinalIgnoreCase)) return t.Contains('+') ? 22 : 21;

        return double.NaN;
    }

    public IReadOnlyList<FichaEntrada> Todas { get; }
    public IReadOnlyList<string> Livros { get; }

    public FichaRepositorio(IWebHostEnvironment env)
    {
        var pasta = Path.Combine(env.ContentRootPath, "Fichas");
        var lista = new List<FichaEntrada>();

        if (Directory.Exists(pasta))
        {
            foreach (var arq in Directory.EnumerateFiles(pasta, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var ficha = JsonSerializer.Deserialize<FichaT20>(File.ReadAllText(arq));
                    if (ficha != null)
                        lista.Add(new FichaEntrada(Path.GetRelativePath(pasta, arq), ficha));
                }
                catch (JsonException)
                {
                    // arquivo inválido: ignora
                }
            }
        }

        Todas = lista.OrderBy(e => e.Ficha.Nome, StringComparer.CurrentCultureIgnoreCase).ToList();
        Livros = Todas.Select(e => e.Livro).Where(l => l != "").Distinct().OrderBy(l => l).ToList();
    }
}