namespace ProsegurGDT.Web.Services;

public interface IEmbeddingService
{
    string Version { get; }
    int Dimension { get; }
    // true si el modelo ONNX está cargado; false con el respaldo hash.
    bool ModeloIaActivo { get; }
    float[] Embed(string textoNormalizado);
}

// Línea base: vectores de n-gramas de caracteres y palabras (hashing). Se reemplazará por un modelo ONNX.
public class HashingNgramEmbeddingService : IEmbeddingService
{
    public string Version => "ngram-hash-v1";
    public int Dimension => 512;
    public bool ModeloIaActivo => false;

    public float[] Embed(string texto)
    {
        var v = new float[Dimension];
        if (string.IsNullOrWhiteSpace(texto)) return v;

        var t = " " + texto + " ";
        for (int i = 0; i + 3 <= t.Length; i++) Acumular(v, t.AsSpan(i, 3), 1f);
        foreach (var palabra in texto.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            Acumular(v, palabra.AsSpan(), 2f);

        var norma = Math.Sqrt(v.Sum(x => (double)x * x));
        if (norma > 0) for (int i = 0; i < v.Length; i++) v[i] = (float)(v[i] / norma);
        return v;
    }

    // FNV-1a: determinista entre ejecuciones (string.GetHashCode NO lo es en .NET)
    private void Acumular(float[] v, ReadOnlySpan<char> s, float peso)
    {
        uint h = 2166136261;
        foreach (var c in s) { h ^= (uint)c; h *= 16777619; }
        v[(int)(h % (uint)Dimension)] += peso;
    }
}

public static class VectorUtil
{
    public static byte[] ToBytes(float[] v)
    {
        var b = new byte[v.Length * sizeof(float)];
        Buffer.BlockCopy(v, 0, b, 0, b.Length);
        return b;
    }

    public static float[] FromBytes(byte[] b)
    {
        var v = new float[b.Length / sizeof(float)];
        Buffer.BlockCopy(b, 0, v, 0, b.Length);
        return v;
    }

    public static double Dot(float[] a, float[] b)
    {
        var n = Math.Min(a.Length, b.Length);
        double s = 0;
        for (int i = 0; i < n; i++) s += a[i] * b[i];
        return s;
    }
}