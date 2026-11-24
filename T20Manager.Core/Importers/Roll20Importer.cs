using System.Text.Json;
using System.Text.RegularExpressions;
using T20Manager.Core.Models;

namespace T20Manager.Core.Importers;

public class Roll20Importer
{
    // Formato do export: { schema_version, type, character: { name, attribs: [ {name,current,max,id} ] } }
    // Ficha de Ameaça (monstro) tem cstype = 1 e usa chaves "menace_*".
    // O app trabalha só com fichas de Ameaça.

    private static readonly string[] Atributos = { "for", "des", "con", "int", "sab", "car" };

    // O ID da linha tem 20 chars e pode conter "_" e "-", então não dá pra usar [^_]+
    private static readonly Regex RepetivelRx = new(
        @"^repeating_(?<sec>[^_]+)_(?<id>-[A-Za-z0-9_-]{19})_(?<campo>.+)$", RegexOptions.Compiled);

    private static readonly Regex PericiaRx = new(@"^menace_(?<p>.+)total$", RegexOptions.Compiled);
    private static readonly Regex DanoRx = new(@"^\s*[+-]?\d+\s*\((?<d>[^,)]*)", RegexOptions.Compiled);
    private static readonly Regex BonusRx = new(@"^\s*(?<b>[+-]\d+)", RegexOptions.Compiled);

    private record Attr(string Atual, string Max);

    public FichaT20 ImportarDeArquivo(string caminho) => Importar(File.ReadAllText(caminho));

    public FichaT20 Importar(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("character", out var ch) ||
            !ch.TryGetProperty("attribs", out var attribsJson))
            throw new NaoEhFichaException("não é ficha de personagem (handout/outro tipo)");

        var simples = new Dictionary<string, Attr>(StringComparer.OrdinalIgnoreCase);
        var secoes = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);

        foreach (var at in attribsJson.EnumerateArray())
        {
            var nome = at.GetProperty("name").GetString() ?? "";
            var atual = Valor(at, "current");
            var max = Valor(at, "max");

            var m = RepetivelRx.Match(nome);
            if (m.Success)
            {
                var sec = m.Groups["sec"].Value;
                var id = m.Groups["id"].Value;
                if (!secoes.TryGetValue(sec, out var porId)) secoes[sec] = porId = new();
                if (!porId.TryGetValue(id, out var campos)) porId[id] = campos = new(StringComparer.OrdinalIgnoreCase);
                campos[m.Groups["campo"].Value] = atual;
            }
            else simples[nome] = new Attr(atual, max);
        }

        var temDadosDeAmeaca = Inteiro(simples, "menace_defense") > 0 || Inteiro(simples, "menace_hp") > 0;
        if (Inteiro(simples, "cstype") != 1 && !temDadosDeAmeaca)
            throw new InvalidDataException(
                $"cstype={Texto(simples, "cstype")}, menace_defense={Texto(simples, "menace_defense")}, menace_hp={Texto(simples, "menace_hp")}");

        return ImportarAmeaca(ch.GetProperty("name").GetString() ?? "", simples, secoes);
    }

    /// <summary>Importa todos os .json da pasta e subpastas; falhas viram avisos e não interrompem o lote.</summary>
    public (List<FichaT20> Fichas, List<string> Avisos) ImportarPasta(string pasta)
    {
        var fichas = new List<FichaT20>();
        var avisos = new List<string>();

        foreach (var arq in Directory.GetFiles(pasta, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var f = ImportarDeArquivo(arq);
                // Ex.: "Ameaças de Arton/Dragões" (pasta relativa à raiz)
                f.Origem = Path.GetRelativePath(pasta, Path.GetDirectoryName(arq)!).Replace('\\', '/');
                if (f.Habilidades.Count == 0 && f.Ataques.Count == 0)
                {
                    avisos.Add($"[pulada] {Path.GetFileName(arq)}: sem ataques nem habilidades");
                    continue; // não entra na lista de fichas
                }
                fichas.Add(f);
            }
            catch (NaoEhFichaException ex)
            {
                avisos.Add($"[ignorado] {Path.GetFileName(arq)}: {ex.Message}");
            }
            catch (Exception ex)
            {
                avisos.Add($"{Path.GetFileName(arq)}: {ex.Message}");
            }
        }
        return (fichas, avisos);
    }

    // ---------------- AMEAÇA ----------------
    private static FichaT20 ImportarAmeaca(string nomeJson, Dictionary<string, Attr> s,
        Dictionary<string, Dictionary<string, Dictionary<string, string>>> secoes)
    {
        var ficha = new FichaT20
        {
            EhAmeaca = true,
            Nome = Primeiro(Texto(s, "menace_name"), nomeJson),
            Tipo = Texto(s, "menace_type_size"),
            ND = Texto(s, "menace_nd"),
            Deslocamento = Texto(s, "menace_desloc"),
            Sentidos = Texto(s, "menace_perception"),
            Tesouro = Texto(s, "menace_treasures"),
            Defesa = Inteiro(s, "menace_defense"),
            Iniciativa = Texto(s, "menace_init"),
            Percepcao = Texto(s, "menace_percep"),
            Fortitude = Texto(s, "menace_fortitude"),
            Reflexos = Texto(s, "menace_reflex"),
            Vontade = Texto(s, "menace_will"),
        };

        // "vida" tem atual/max (14/14); menace_hp só o total
        var vida = Recurso(s, "vida");
        if (vida.Max == 0) vida = new Recurso { Atual = Inteiro(s, "menace_hp"), Max = Inteiro(s, "menace_hp") };
        ficha.Vida = vida;
        ficha.Mana = new Recurso { Atual = Inteiro(s, "menace_mp"), Max = Inteiro(s, "menace_mp") };

        foreach (var a in Atributos) ficha.Atributos[a] = Inteiro(s, $"menace_{a}");

        // Perícias: qualquer menace_XXXtotal (acrobaciatotal, furtividadetotal, ...)
        foreach (var kv in s)
        {
            var m = PericiaRx.Match(kv.Key);
            if (m.Success) ficha.Pericias[m.Groups["p"].Value] = kv.Value.Atual;
        }

        foreach (var c in Linhas(secoes, "menaceabilities"))
            ficha.Habilidades.Add(new Habilidade
            {
                Nome = Campo(c, "menacenameability").Trim(),
                Descricao = Campo(c, "menaceabilitydescription").Trim(),
            });

        foreach (var c in Linhas(secoes, "menacemelee"))
            ficha.Ataques.Add(CriarAtaque(c, Campo(c, "nomeataque"), Campo(c, "meleedescription"), distancia: false));

        foreach (var c in Linhas(secoes, "menacedistance"))
            ficha.Ataques.Add(CriarAtaque(c, Campo(c, "nomeataque"), Campo(c, "distancedescription"), distancia: true));

        return ficha;
    }

    // ---------------- helpers ----------------
    private static Ataque CriarAtaque(Dictionary<string, string> c, string nome, string descricao, bool distancia)
    {
        // A descrição do Roll20 ("+67 (4d10+31, 19)") já traz o dano com bônus; é a fonte mais confiável.
        var dano = DanoDaDescricao(descricao);
        if (dano == "")
        {
            dano = Campo(c, "danoataque");
            var extra = Campo(c, "danoextraataque");
            if (extra != "" && extra != "0") dano += extra.StartsWith('-') ? extra : "+" + extra;
        }

        var margem = Campo(c, "margemcriticoataque");
        var mult = Campo(c, "multiplicadorcriticoataque");
        var crit = margem != "" ? $"{margem}/x{(mult == "" ? "2" : mult)}" : "";

        var b = BonusRx.Match(descricao);
        return new Ataque
        {
            Nome = nome,
            Distancia = distancia,
            Bonus = b.Success ? b.Groups["b"].Value : "",
            Dano = dano,
            Critico = crit,
            TipoDano = Campo(c, "ataquetipodedano"),
        };
    }

    // "+67 (4d10+31, 19)" -> "4d10+31";  "+7 (1d6+0+5)" -> "1d6+5"
    private static string DanoDaDescricao(string descricao)
    {
        var m = DanoRx.Match(descricao);
        return m.Success ? SimplificarDano(m.Groups["d"].Value) : "";
    }

    // Mantém os termos de dado e soma os números soltos
    private static string SimplificarDano(string expr)
    {
        var dados = new List<string>();
        var fixo = 0;

        foreach (Match m in Regex.Matches(expr, @"([+-]?)\s*([^+\-]+)"))
        {
            var negativo = m.Groups[1].Value == "-";
            var termo = m.Groups[2].Value.Trim();
            if (termo == "") continue;

            if (int.TryParse(termo, out var n)) fixo += negativo ? -n : n;
            else dados.Add((negativo ? "-" : dados.Count > 0 ? "+" : "") + termo);
        }

        if (dados.Count == 0 && fixo == 0) return expr.Trim();

        var texto = string.Concat(dados);
        if (fixo != 0) texto += fixo > 0 ? $"+{fixo}" : fixo.ToString();
        return texto;
    }

    private static string Valor(JsonElement at, string prop) =>
        at.TryGetProperty(prop, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : "";

    private static IEnumerable<Dictionary<string, string>> Linhas(
        Dictionary<string, Dictionary<string, Dictionary<string, string>>> secoes, string nome) =>
        secoes.TryGetValue(nome, out var porId) ? porId.Values : Enumerable.Empty<Dictionary<string, string>>();

    private static string Campo(Dictionary<string, string> campos, params string[] candidatos)
    {
        foreach (var k in candidatos)
            if (campos.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v)) return v;
        return "";
    }

    private static string Primeiro(params string[] valores) =>
        valores.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

    private static string Texto(Dictionary<string, Attr> d, string k) =>
        d.TryGetValue(k, out var v) ? v.Atual : "";

    private static int Inteiro(Dictionary<string, Attr> d, string k) =>
        d.TryGetValue(k, out var v) && int.TryParse(v.Atual, out var n) ? n : 0;

    private static Recurso Recurso(Dictionary<string, Attr> d, string k) =>
        d.TryGetValue(k, out var v)
            ? new Recurso
            {
                Atual = int.TryParse(v.Atual, out var a) ? a : 0,
                Max = int.TryParse(v.Max, out var m) ? m : 0
            }
            : new Recurso();
}

public class NaoEhFichaException : Exception
{
    public NaoEhFichaException(string mensagem) : base(mensagem) { }
}