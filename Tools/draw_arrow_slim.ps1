$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
public static class ArrowSlim
{
    public static void Frame(int shift, string file)
    {
        int W = 200, H = 28;
        using (Bitmap bmp = new Bitmap(W, H))
        {
            for (int x = 4; x <= 171; x++)
            {
                Put(bmp, x, 10, 222, 196, 150); Put(bmp, x, 11, 222, 202, 156); Put(bmp, x, 12, 206, 178, 132);
                Put(bmp, x, 13, 196, 168, 124); Put(bmp, x, 14, 186, 156, 112); Put(bmp, x, 15, 158, 132, 94);
            }
            for (int x = 0; x <= 10; x++) for (int y = 10; y <= 15; y++) Put(bmp, x, y, 44, 38, 36);
            for (int x = 5; x <= 10; x++) for (int y = 11; y <= 14; y++) Put(bmp, x, y, 222, 142, 58);
            for (int x = 12; x <= 46; x++)
            {
                int h = (int)Math.Round((46 - x) * 0.21) + 1;
                for (int k = 0; k < h; k++)
                {
                    int y = 10 - k + shift;
                    if (k == h / 2) Put(bmp, x, y, 168, 144, 118); else Put(bmp, x, y, 138, 116, 96);
                    if (x % 9 == 0 || x % 9 == 1) Put(bmp, x, y, 118, 96, 78);
                }
            }
            for (int x = 16; x <= 44; x++)
            {
                int h = (int)Math.Round((44 - x) * 0.08) + 1;
                for (int k = 1; k < h; k++) Put(bmp, x, 16 + k + shift, 128, 106, 88);
            }
            for (int x = 171; x <= 176; x++) for (int y = 11; y <= 14; y++) Put(bmp, x, y, 226, 148, 60);
            for (int x = 177; x < 200; x++)
            {
                double t = (x - 176) / 23.0;
                int halfW = (int)Math.Round(8 * (1 - t) * (1 - t * 0.15));
                for (int y = 13 - halfW; y <= 14 + halfW; y++)
                {
                    int shade = 46;
                    if (y == 13 - halfW) shade = 62; else if (y == 14 + halfW) shade = 34;
                    Put(bmp, x, y, shade, shade - 2, shade + 4);
                }
            }
            bmp.Save(file, ImageFormat.Png);
        }
    }

    static void Put(Bitmap bmp, int x, int y, int r, int g, int b)
    {
        if (x >= 0 && x < bmp.Width && y >= 0 && y < bmp.Height)
            bmp.SetPixel(x, y, Color.FromArgb(255, r, g, b));
    }
}
'@

$dir = 'D:\Game Provide\RogueShooter-main\Assets\Art\JianHai\Iso\Projectiles'
[System.IO.Directory]::CreateDirectory($dir) | Out-Null
[ArrowSlim]::Frame(0, ($dir + '\jh_iso_proj_arrow_slim_00.png'))
[ArrowSlim]::Frame(2, ($dir + '\jh_iso_proj_arrow_slim_01.png'))
Get-ChildItem $dir -Filter '*slim*' | Select-Object Name, Length
