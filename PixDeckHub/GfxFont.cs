namespace PixDeckHub;

/// <summary>
/// Fuente de mapa de bits estilo Adafruit GFX (la misma que usa el ESP32).
/// Sirve para que la vista previa dibuje Horyzen y Techno exactamente igual que el equipo.
/// </summary>
public sealed class GfxFont
{
    readonly byte[] _bmp;
    readonly short[] _g;   // por glifo: offset, ancho, alto, avance, xOff, yOff
    readonly int _first, _last;
    public int YAdvance { get; }
    public int Ascent { get; }    // pixeles sobre la linea base (como glyph_ab de TFT_eSPI)
    public int Descent { get; }   // pixeles bajo la linea base (glyph_bb)

    public GfxFont(byte[] bitmaps, short[] glyphs, int first, int last, int yAdvance)
    {
        _bmp = bitmaps;
        _g = glyphs;
        _first = first;
        _last = last;
        YAdvance = yAdvance;
        int ab = 0, bb = 0;
        for (int i = 0; i * 6 < _g.Length; i++)
        {
            int h = _g[i * 6 + 2], yo = _g[i * 6 + 5];
            if (h == 0) continue;
            ab = Math.Max(ab, -yo);
            bb = Math.Max(bb, yo + h);
        }
        Ascent = ab;
        Descent = bb;
    }

    int Index(char c) => c < _first || c > _last ? -1 : (c - _first) * 6;

    public float Width(string s, float scale)
    {
        int w = 0;
        foreach (var c in s) { int i = Index(c); if (i >= 0) w += _g[i + 3]; }
        return w * scale;
    }

    public float Height(float scale) => (Ascent + Descent) * scale;

    /// <summary>Dibuja con la esquina superior izquierda del renglon en (x, y).</summary>
    public void Draw(Graphics g, string s, Brush b, float x, float y, float scale)
    {
        float baseline = y + Ascent * scale;
        foreach (var c in s)
        {
            int i = Index(c);
            if (i < 0) continue;
            int off = _g[i], w = _g[i + 1], h = _g[i + 2], adv = _g[i + 3], xo = _g[i + 4], yo = _g[i + 5];
            int bit = 0;
            for (int yy = 0; yy < h; yy++)
                for (int xx = 0; xx < w; xx++, bit++)
                {
                    int idx = off + (bit >> 3);
                    if (idx < _bmp.Length && (_bmp[idx] & (0x80 >> (bit & 7))) != 0)
                        g.FillRectangle(b, x + (xo + xx) * scale, baseline + (yo + yy) * scale, scale, scale);
                }
            x += adv * scale;
        }
    }
}
