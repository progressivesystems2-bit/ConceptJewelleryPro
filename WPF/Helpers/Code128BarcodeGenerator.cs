using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ConceptJewelleryPro.WPF.Helpers;

/// <summary>
/// Generates standard Code 128 (Subset B) vector barcode images for WPF.
/// </summary>
public static class Code128BarcodeGenerator
{
    private static readonly string[] Patterns = new string[]
    {
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213", // 0-9
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132", // 10-19
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211", // 20-29
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313", // 30-39
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331", // 40-49
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111", // 50-59
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214", // 60-69
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111", // 70-79
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141", // 80-89
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141", // 90-99
        "114131", "311141", "411131", "211412", "211214", "211232", "2331112" // 100-106
    };

    private const int StartB = 104;
    private const int Stop = 106;

    /// <summary>
    /// Generates a crisp vector DrawingImage for the given text using Code 128-B.
    /// </summary>
    /// <param name="text">Text to encode (alphanumeric ASCII)</param>
    /// <param name="height">Target height in device independent pixels</param>
    /// <param name="moduleWidth">Width of a single narrow bar in DIP</param>
    /// <returns>A vector DrawingImage ready for use in Image controls</returns>
    public static ImageSource GenerateBarcodeImage(string text, double height = 32, double moduleWidth = 1.2)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            text = " ";
        }

        var codes = new List<int> { StartB };
        long checksum = StartB;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            int val = (c >= 32 && c <= 126) ? (c - 32) : 0;
            codes.Add(val);
            checksum += (long)(i + 1) * val;
        }

        codes.Add((int)(checksum % 103));
        codes.Add(Stop);

        // Build pattern of modules
        var isBar = true;
        var moduleList = new List<bool>();

        // 10 quiet zone modules
        for (int q = 0; q < 10; q++) moduleList.Add(false);

        foreach (var code in codes)
        {
            var pattern = Patterns[code];
            isBar = true;
            foreach (var ch in pattern)
            {
                int count = ch - '0';
                for (int m = 0; m < count; m++)
                {
                    moduleList.Add(isBar);
                }
                isBar = !isBar;
            }
        }

        // 10 quiet zone modules
        for (int q = 0; q < 10; q++) moduleList.Add(false);

        var drawingGroup = new DrawingGroup();
        using (var dc = drawingGroup.Open())
        {
            double currentX = 0;
            int runCount = 0;
            bool? currentBar = null;

            for (int i = 0; i < moduleList.Count; i++)
            {
                bool bar = moduleList[i];
                if (currentBar == null)
                {
                    currentBar = bar;
                    runCount = 1;
                }
                else if (currentBar == bar)
                {
                    runCount++;
                }
                else
                {
                    if (currentBar.Value)
                    {
                        dc.DrawRectangle(Brushes.Black, null, new Rect(currentX, 0, runCount * moduleWidth, height));
                    }
                    currentX += runCount * moduleWidth;
                    currentBar = bar;
                    runCount = 1;
                }
            }

            if (currentBar != null && currentBar.Value)
            {
                dc.DrawRectangle(Brushes.Black, null, new Rect(currentX, 0, runCount * moduleWidth, height));
            }
        }

        return new DrawingImage(drawingGroup);
    }
}
