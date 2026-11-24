using System.Text.Json;
using T20Manager.Core.Models;

namespace T20Manager.App.Services;

/// <summary>Uma cópia de ficha em combate, com vida e mana próprias.</summary>
public class Combatente
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Base { get; init; } = "";   // nome da ficha original
    public int Numero { get; init; }          // 1, 2, 3...
    public string Nome => $"{Base} {Numero}";
    public FichaT20 Ficha { get; init; } = new();
}

/// <summary>
/// Singleton: o combate sobrevive a recarregar a página, mas não a fechar o app.
/// </summary>
public class CombateService
{
    private readonly List<Combatente> _lista = new();

    public IReadOnlyList<Combatente> Combatentes => _lista;

    public void Adicionar(FichaT20 modelo, int quantidade = 1)
    {
        quantidade = Math.Clamp(quantidade, 1, 20);
        var json = JsonSerializer.Serialize(modelo);

        for (var i = 0; i < quantidade; i++)
        {
            // Cópia profunda: mexer na vida de um não afeta os outros nem a ficha original
            var copia = JsonSerializer.Deserialize<FichaT20>(json)!;

            // Maior número já usado + 1 (não repete mesmo depois de remover alguém)
            var numero = _lista.Where(c => c.Base == modelo.Nome)
                               .Select(c => c.Numero)
                               .DefaultIfEmpty(0)
                               .Max() + 1;

            _lista.Add(new Combatente { Base = modelo.Nome, Numero = numero, Ficha = copia });
        }
    }

    public void Remover(Guid id) => _lista.RemoveAll(c => c.Id == id);

    public void Limpar() => _lista.Clear();
}