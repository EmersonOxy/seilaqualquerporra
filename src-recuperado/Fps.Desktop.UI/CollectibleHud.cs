using Fps.Desktop.Gameplay;
using Raylib_cs;

namespace Fps.Desktop.UI;

internal static class CollectibleHud
{
    public static void Draw(CollectibleManager items)
    {
        int screenWidth = Raylib.GetScreenWidth();
        int screenHeight = Raylib.GetScreenHeight();
        string counter = $"ITENS  {items.CollectedCount} / {items.TotalCount}";
        Raylib.DrawText(counter, 22, 22, 22, Color.White);
        if (items.Completed)
        {
            Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, new Color(0, 0, 0, 170));
            string title = "MAPA CONCLUIDO";
            int titleFontSize = 40;
            int titleWidth = Raylib.MeasureText(title, titleFontSize);
            Raylib.DrawText(title, (screenWidth - titleWidth) / 2, screenHeight / 2 - 34, titleFontSize, Color.White);
            string subtitle = $"Voce coletou todos os {items.TotalCount} itens";
            Raylib.DrawText(subtitle, (screenWidth - Raylib.MeasureText(subtitle, 20)) / 2, screenHeight / 2 + 24, 20, Color.White);
            return;
        }
        string objective = items.Objective;
        Raylib.DrawText(objective, 22, 52, 18, new Color(235, 235, 235, 220));
    }
}
