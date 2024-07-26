using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

namespace JavascriptForGrasshopper
{
    internal static class Resources
    {
        public static Bitmap Icon_JS => GetBitmap("logo_javascript.png");
        public static Bitmap Icon_TS => GetBitmap("logo_typescript.png");

        private static readonly Dictionary<string, Bitmap> loadedBitmaps = new Dictionary<string, Bitmap>();
        private static Bitmap GetBitmap(string path)
        {
            if (!loadedBitmaps.TryGetValue(path, out Bitmap bmp))
            {
                bmp = new Bitmap(Assembly.GetAssembly(typeof(Resources)).GetManifestResourceStream(typeof(Resources).FullName + "." + path));
                loadedBitmaps.Add(path, bmp);
            }
            return bmp;
        }
    }
}
