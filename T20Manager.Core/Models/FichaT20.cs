namespace T20Manager.Core.Models;

public class FichaT20
{
    public string Nome { get; set; } = "";
    public bool EhAmeaca { get; set; }          // cstype == 1 no Roll20
    public string Origem { get; set; } = "";    // pasta de origem: "Ameaças de Arton/Dragões"
    public string Tipo { get; set; } = "";      // "Animal Médio (Lacaio)"
    public string ND { get; set; } = "";        // string: existe "1/2", "1/4"...
    public string Deslocamento { get; set; } = "";
    public string Sentidos { get; set; } = "";
    public string Tesouro { get; set; } = "";

    public int Defesa { get; set; }
    public string Iniciativa { get; set; } = "";
    public string Percepcao { get; set; } = "";
    public string Fortitude { get; set; } = "";
    public string Reflexos { get; set; } = "";
    public string Vontade { get; set; } = "";

    public Recurso Vida { get; set; } = new();
    public Recurso Mana { get; set; } = new();

    public Dictionary<string, int> Atributos { get; set; } = new();   // for, des, con, int, sab, car
    public Dictionary<string, string> Pericias { get; set; } = new(); // "furtividade" -> "+3"

    public List<Ataque> Ataques { get; set; } = new();
    public List<Habilidade> Habilidades { get; set; } = new();
}

public class Recurso
{
    public int Atual { get; set; }
    public int Max { get; set; }
}

public class Ataque
{
    public string Nome { get; set; } = "";
    public bool Distancia { get; set; }         // false = corpo a corpo
    public string Bonus { get; set; } = "";     // "+7"
    public string Dano { get; set; } = "";      // "1d6+5"
    public string Critico { get; set; } = "";   // "20/x2"
    public string TipoDano { get; set; } = "";  // "Perfuração"
}

public class Habilidade
{
    public string Nome { get; set; } = "";
    public string Descricao { get; set; } = "";
}