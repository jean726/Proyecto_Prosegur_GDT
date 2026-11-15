namespace ProsegurGDT.Web.Services;

// Motor híbrido (B1): concatena el vector semántico (ONNX) y el de n-gramas (hash) con pesos √w y √(1-w).
// Con ambos vectores unitarios, Dot(a,b) = w·cos_onnx + (1-w)·cos_hash: el modelo aporta tolerancia a errores de tipeo
// y el hash separa calles distintas del mismo distrito. Sigue siendo unitario, así que AddressRiskService no cambia.
public sealed class HybridEmbeddingService : IEmbeddingService, IDisposable
{
    private readonly IEmbeddingService _semantico;
    private readonly IEmbeddingService _hash;
    private readonly float _a, _b;

    public string Version { get; }
    public int Dimension => _semantico.Dimension + _hash.Dimension;

    public HybridEmbeddingService(IEmbeddingService semantico, IEmbeddingService hash, double pesoSemantico)
    {
        _semantico = semantico;
        _hash = hash;
        pesoSemantico = Math.Clamp(pesoSemantico, 0.0, 1.0);
        _a = (float)Math.Sqrt(pesoSemantico);
        _b = (float)Math.Sqrt(1 - pesoSemantico);
        // ModeloVersion y Metodo admiten 40 caracteres: mantener la versión corta.
        Version = $"hib-{semantico.Version}+{hash.Version}-w{(int)Math.Round(pesoSemantico * 100)}";
        if (Version.Length > 40) throw new InvalidOperationException($"La versión del motor excede 40 caracteres: {Version}");
    }

    public float[] Embed(string textoNormalizado)
    {
        var s = _semantico.Embed(textoNormalizado);
        var h = _hash.Embed(textoNormalizado);
        var v = new float[s.Length + h.Length];
        for (int i = 0; i < s.Length; i++) v[i] = s[i] * _a;
        for (int i = 0; i < h.Length; i++) v[s.Length + i] = h[i] * _b;
        return v;
    }

    public void Dispose() => (_semantico as IDisposable)?.Dispose();
}
