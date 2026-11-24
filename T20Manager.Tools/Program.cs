using System.Text.Json;
using T20Manager.Core.Importers;

var raiz = args.Length > 0 ? args[0] : @"C:\Users\jpdin\source\repos\T20Manager\Ameaças";
var saida = args.Length > 1 ? args[1] : @"Fichas";

var importer = new Roll20Importer();
var (fichas, avisos) = importer.ImportarPasta(raiz);

var opcoes = new JsonSerializerOptions { WriteIndented = true };
var invalidos = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }).ToArray();
var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

foreach (var f in fichas)
{
    var dir = Path.Combine(saida, f.Origem);
    Directory.CreateDirectory(dir);

    // Remove caracteres inválidos e espaços nas pontas do nome
    var nomeArq = string.Concat(f.Nome.Select(c => invalidos.Contains(c) ? '-' : c)).Trim();
    if (nomeArq == "") nomeArq = "sem-nome";

    // Evita sobrescrever fichas com o mesmo nome na mesma pasta
    var caminho = Path.Combine(dir, nomeArq + ".json");
    for (int i = 2; !usados.Add(caminho); i++)
        caminho = Path.Combine(dir, $"{nomeArq} ({i}).json");

    File.WriteAllText(caminho, JsonSerializer.Serialize(f, opcoes));
}

Console.WriteLine($"{fichas.Count} fichas importadas, {avisos.Count} avisos.");
avisos.ForEach(Console.WriteLine);