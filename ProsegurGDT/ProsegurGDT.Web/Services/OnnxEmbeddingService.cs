using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ProsegurGDT.Web.Services;

// Motor de IA (B1): embeddings con paraphrase-multilingual-MiniLM-L12-v2 (ONNX cuantizado int8, 384 dimensiones).
// Se ejecuta en CPU, sin servicios externos. Config: IA:ModeloRuta = carpeta con model_quantized.onnx y tokenizer.json.
public sealed class OnnxEmbeddingService : IEmbeddingService, IDisposable
{
    public const string ArchivoModelo = "model_quantized.onnx";
    public const string ArchivoTokenizador = "tokenizer.json";
    private const int MaxTokens = 128;

    private readonly InferenceSession _sesion;
    private readonly UnigramTokenizer _tok;
    private readonly string _salida;
    private readonly bool _usaTipos;

    public string Version => "onnx-minilm-v2";
    public int Dimension => 384;
    public bool ModeloIaActivo => true;

    public OnnxEmbeddingService(string carpetaModelo)
    {
        _tok = UnigramTokenizer.Cargar(Path.Combine(carpetaModelo, ArchivoTokenizador));
        var opciones = new Microsoft.ML.OnnxRuntime.SessionOptions { InterOpNumThreads = 1, IntraOpNumThreads = 2 };
        _sesion = new InferenceSession(Path.Combine(carpetaModelo, ArchivoModelo), opciones);
        _salida = _sesion.OutputMetadata.Keys.First();
        _usaTipos = _sesion.InputMetadata.ContainsKey("token_type_ids");
    }

    public static bool Disponible(string carpetaModelo) =>
        File.Exists(Path.Combine(carpetaModelo, ArchivoModelo)) &&
        File.Exists(Path.Combine(carpetaModelo, ArchivoTokenizador));

    public float[] Embed(string textoNormalizado)
    {
        var v = new float[Dimension];
        if (string.IsNullOrWhiteSpace(textoNormalizado)) return v;

        // El normalizador del proyecto deja MAYÚSCULAS sin tildes; el modelo distingue mayúsculas, así que se pasa a minúsculas.
        var ids = _tok.Codificar(textoNormalizado.ToLowerInvariant(), MaxTokens);
        int n = ids.Length;
        var dims = new[] { 1, n };
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(ids, dims)),
            NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(Enumerable.Repeat(1L, n).ToArray(), dims)),
        };
        if (_usaTipos)
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(new long[n], dims)));

        using var resultado = _sesion.Run(inputs);
        var oculto = resultado.First(r => r.Name == _salida).AsTensor<float>(); // [1, n, 384]

        // Promedio sobre los tokens (todos con máscara 1, sin relleno) y normalización L2: Dot() supone vectores unitarios.
        for (int t = 0; t < n; t++)
            for (int d = 0; d < Dimension; d++) v[d] += oculto[0, t, d];
        double norma = 0;
        for (int d = 0; d < Dimension; d++) { v[d] /= n; norma += (double)v[d] * v[d]; }
        norma = Math.Sqrt(norma);
        if (norma > 0) for (int d = 0; d < Dimension; d++) v[d] = (float)(v[d] / norma);
        return v;
    }

    public void Dispose() => _sesion.Dispose();
}

// Tokenizador Unigram (SentencePiece) de XLM-R leído desde tokenizer.json de Hugging Face. Evita un paquete NuGet extra.
internal sealed class UnigramTokenizer
{
    private const char Marca = '▁'; // ▁
    private readonly Dictionary<string, (int Id, double Puntaje)> _piezas;
    private readonly int _largoMax;
    private readonly int _unk;
    private readonly double _puntajeUnk;
    private const int Bos = 0, Eos = 2;

    private UnigramTokenizer(Dictionary<string, (int, double)> piezas, int unk)
    {
        _piezas = piezas;
        _unk = unk;
        _largoMax = piezas.Keys.Max(k => k.Length);
        _puntajeUnk = piezas.Values.Min(p => p.Item2) - 10.0;
    }

    public static UnigramTokenizer Cargar(string ruta)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(ruta));
        var modelo = doc.RootElement.GetProperty("model");
        var piezas = new Dictionary<string, (int, double)>();
        int id = 0;
        foreach (var p in modelo.GetProperty("vocab").EnumerateArray())
        {
            piezas[p[0].GetString()!] = (id, p[1].GetDouble());
            id++;
        }
        return new UnigramTokenizer(piezas, modelo.GetProperty("unk_id").GetInt32());
    }

    // <s> piezas </s>, truncado a maxTokens.
    public long[] Codificar(string texto, int maxTokens)
    {
        var ids = new List<long> { Bos };
        foreach (var palabra in texto.Normalize(NormalizationForm.FormKC)
                     .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            ids.AddRange(Viterbi(Marca + palabra).Select(i => (long)i));
        if (ids.Count > maxTokens - 1) ids.RemoveRange(maxTokens - 1, ids.Count - (maxTokens - 1));
        ids.Add(Eos);
        return ids.ToArray();
    }

    // Segmentación de mayor puntaje total (algoritmo de Viterbi sobre el vocabulario Unigram).
    private List<int> Viterbi(string s)
    {
        int n = s.Length;
        var mejor = new double[n + 1];
        var desde = new int[n + 1];
        var idPieza = new int[n + 1];
        Array.Fill(mejor, double.NegativeInfinity);
        mejor[0] = 0;
        for (int i = 0; i < n; i++)
        {
            if (double.IsNegativeInfinity(mejor[i])) continue;
            bool alguna = false;
            for (int l = 1; l <= _largoMax && i + l <= n; l++)
            {
                if (!_piezas.TryGetValue(s.Substring(i, l), out var p)) continue;
                alguna = true;
                if (mejor[i] + p.Puntaje > mejor[i + l])
                { mejor[i + l] = mejor[i] + p.Puntaje; desde[i + l] = i; idPieza[i + l] = p.Id; }
            }
            if (!alguna && mejor[i] + _puntajeUnk > mejor[i + 1])
            { mejor[i + 1] = mejor[i] + _puntajeUnk; desde[i + 1] = i; idPieza[i + 1] = _unk; }
        }
        var res = new List<int>();
        for (int pos = n; pos > 0; pos = desde[pos]) res.Add(idPieza[pos]);
        res.Reverse();
        return res;
    }
}
