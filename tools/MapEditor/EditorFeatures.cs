using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Fps.Desktop.Scenes;
using Raylib_cs;

namespace MapEditor;

internal static unsafe partial class Program
{
    private static readonly List<MapObject> selectedObjects = new();
    private static MapPortalNode? activePortal;
    private static MapCollectible? activeItem;
    private static int hierarchyTab;   // 0 objects, 1 portals, 2 collectibles
    private static int mainTab;        // 0 create, 1 tools, 2 links, 3 file
    private static int inspectorTab;   // 0 transform, 1 faces, 2 settings
    private static bool linking;
    private static MapPortalNode? linkSource;
    private static bool pendingMarquee, activeMarquee, marqueeCtrl;
    private static Vector2 marqueeFrom, marqueeTo;
    private static float toastRemaining;
    private static string toastText = "";
    private static bool toastError;
    private static Font uiFont;
    private static bool smoothFont;

    private static void InitializeEditorFont()
    {
        string fonts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
        foreach (string name in new[] { "segoeui.ttf", "arial.ttf", "tahoma.ttf" })
        {
            string path = Path.Combine(fonts, name);
            if (!File.Exists(path)) continue;
            uiFont = Raylib.LoadFontEx(path, 30, null, 0);
            smoothFont = uiFont.Texture.Id != 0;
            if (smoothFont)
            {
                Raylib.SetTextureFilter(uiFont.Texture, TextureFilter.Bilinear);
                return;
            }
        }
    }

    private static void DrawTextSmooth(string message, int x, int y, int size, Color color)
    {
        if (smoothFont)
            Raylib.DrawTextEx(uiFont, message, new Vector2(x, y), size, .5f, color);
        else
            Raylib.DrawText(message, x, y, size, color);
    }

    private static void Toast(string text, bool error = false)
    {
        toastText = text;
        toastRemaining = error ? 5f : 3.2f;
        toastError = error;
    }

    private static void DrawToast()
    {
        if (toastRemaining <= 0f) return;
        int x = (int)((ViewRight - 460f) * .5f);
        int y = 88;
        Raylib.DrawRectangle(x, y, 460, 48, toastError ?
            new Color(108, 52, 55, 238) : new Color(43, 88, 70, 238));
        Raylib.DrawRectangleLines(x, y, 460, 48, Color.LightGray);
        DrawTextSmooth(toastText.Length > 54 ? toastText[..54] : toastText,
            x + 14, y + 13, 19, Color.White);
    }

    private static void ChangeMode(int newMode)
    {
        mode = newMode;
        Toast(mode switch { 0 => "Mover: setas XYZ", 1 => "Girar: aneis XYZ", _ => "Tamanho: cubos XYZ" });
    }

    private static void ClearSelection()
    {
        selectedObjects.Clear();
        selected = null;
        activePortal = null;
        activeItem = null;
    }

    private static void SelectObject(MapObject obj, bool ctrl)
    {
        if (!ctrl) ClearSelection();
        if (ctrl && selectedObjects.Contains(obj))
            selectedObjects.Remove(obj);
        else if (!selectedObjects.Contains(obj))
            selectedObjects.Add(obj);
        selected = selectedObjects.Count > 0 ? selectedObjects[^1] : null;
        activePortal = null; activeItem = null;
        hierarchyTab = 0;
    }

    private static void SelectPortal(MapPortalNode portal)
    {
        ClearSelection();
        activePortal = portal;
        hierarchyTab = 1;
        if (!linking) return;
        if (linkSource == null)
        {
            linkSource = portal;
            Toast("Agora clique no portal de destino");
        }
        else if (linkSource.Id != portal.Id)
        {
            try
            {
                document.LinkPortals(linkSource.Id, portal.Id);
                Toast("Portais ligados: " + linkSource.Name + " -> " + portal.Name);
                Changed();
                linking = false; linkSource = null;
            }
            catch (Exception ex) { Toast(ex.Message, true); }
        }
    }

    private static void SelectItem(MapCollectible item)
    {
        ClearSelection();
        activeItem = item;
        hierarchyTab = 2;
    }

    private static int HoverPortalIndex(Vector2 mouse)
    {
        int answer = -1;
        float closest = 25f;
        for (int i = 0; i < document.PortalNodes.Count; i++)
        {
            Vector2 screen = Raylib.GetWorldToScreen(document.PortalNodes[i].Center, camera);
            float distance = Vector2.Distance(mouse, screen);
            if (distance < closest) { closest = distance; answer = i; }
        }
        return answer;
    }

    private static int HoverItemIndex(Vector2 mouse)
    {
        int answer = -1;
        float closest = 20f;
        for (int i = 0; i < document.Collectibles.Count; i++)
        {
            MapCollectible item = document.Collectibles[i];
            if (!item.Visible) continue;
            Vector2 screen = Raylib.GetWorldToScreen(item.Position, camera);
            float distance = Vector2.Distance(mouse, screen);
            if (distance < closest) { closest = distance; answer = i; }
        }
        return answer;
    }

    private static void ViewClick(Vector2 mouse, bool ctrl)
    {
        if (linking)
        {
            int portalIndex = HoverPortalIndex(mouse);
            if (portalIndex >= 0) SelectPortal(document.PortalNodes[portalIndex]);
            else Toast("Clique no marcador de um portal", true);
            return;
        }
        int portal = HoverPortalIndex(mouse);
        if (portal >= 0) { SelectPortal(document.PortalNodes[portal]); return; }
        int item = HoverItemIndex(mouse);
        if (item >= 0) { SelectItem(document.Collectibles[item]); return; }
        MapObject? hit = Pick(mouse);
        if (hit != null) { SelectObject(hit, ctrl); return; }
        pendingMarquee = true;
        marqueeFrom = mouse;
        marqueeTo = mouse;
        marqueeCtrl = ctrl;
        if (!ctrl) ClearSelection();
    }

    private static void UpdateMarquee(Vector2 mouse)
    {
        if (!pendingMarquee) return;
        marqueeTo = mouse;
        if (Vector2.DistanceSquared(marqueeFrom, mouse) > 64f)
            activeMarquee = true;
        if (MouseDown(0)) return;
        if (activeMarquee)
        {
            float x0 = MathF.Min(marqueeFrom.X, marqueeTo.X);
            float x1 = MathF.Max(marqueeFrom.X, marqueeTo.X);
            float y0 = MathF.Min(marqueeFrom.Y, marqueeTo.Y);
            float y1 = MathF.Max(marqueeFrom.Y, marqueeTo.Y);
            if (!marqueeCtrl) ClearSelection();
            foreach (MapObject obj in document.Objects)
            {
                if (!obj.Visible) continue;
                Vector2 point = Raylib.GetWorldToScreen(obj.Position, camera);
                if (point.X >= x0 && point.X <= x1 && point.Y >= y0 && point.Y <= y1 &&
                    !selectedObjects.Contains(obj))
                    selectedObjects.Add(obj);
            }
            selected = selectedObjects.Count > 0 ? selectedObjects[^1] : null;
            if (selected != null) hierarchyTab = 0;
        }
        pendingMarquee = false;
        activeMarquee = false;
    }

    private static void DrawMarquee()
    {
        if (!activeMarquee) return;
        float x = MathF.Min(marqueeFrom.X, marqueeTo.X);
        float y = MathF.Min(marqueeFrom.Y, marqueeTo.Y);
        float width = MathF.Abs(marqueeTo.X - marqueeFrom.X);
        float height = MathF.Abs(marqueeTo.Y - marqueeFrom.Y);
        Rectangle bounds = new(x, y, width, height);
        Raylib.DrawRectangleRec(bounds, new Color(75, 138, 203, 48));
        Raylib.DrawRectangleLinesEx(bounds, 2f, new Color(96, 176, 241, 255));
    }

    private static Vector3? SelectedPivot() =>
        selected != null ? selected.Position :
        activePortal != null ? activePortal.Center :
        activeItem != null ? activeItem.Position : null;

    private static void MoveSelected(Vector3 delta)
    {
        foreach (MapObject obj in selectedObjects) obj.Position += delta;
        if (activePortal != null) activePortal.Center += delta;
        if (activeItem != null) activeItem.Position += delta;
        Changed();
    }

    private static void TransformSelection(int axisIndex, float delta)
    {
        Vector3 axis = Axis(axisIndex);
        if (selected != null)
        {
            foreach (MapObject obj in selectedObjects)
            {
                if (mode == 0) obj.Position += axis * delta;
                if (mode == 1) obj.Rotation += axis * (delta * 18f);
                if (mode == 2) ResizeAxis(obj, axisIndex, delta, uniform);
            }
        }
        else if (activePortal != null && mode == 0)
            activePortal.Center += axis * delta;
        else if (activeItem != null && mode == 0)
            activeItem.Position += axis * delta;
        Changed();
    }

    private static void DrawGizmo()
    {
        Vector3? center = SelectedPivot();
        if (!center.HasValue) return;
        Vector3 p = center.Value;
        float distance = Vector3.Distance(eye, p);
        float length = Math.Clamp(distance * .075f, 1.2f, 5f);
        for (int i = 0; i < 3; i++)
        {
            Vector3 dir = Axis(i);
            Color color = i == 0 ? Color.Red : i == 1 ? Color.Green : Color.Blue;
            if (mode == 0)
            {
                Raylib.DrawLine3D(p, p + dir * length, color);
                Raylib.DrawCylinderEx(p + dir * (length - .34f), p + dir * length,
                    .14f, 0f, 10, color);
            }
            else if (mode == 1)
            {
                Vector3 a = Axis((i + 1) % 3);
                Vector3 b = Axis((i + 2) % 3);
                for (int j = 0; j < 48; j++)
                {
                    float t0 = j * MathF.Tau / 48f;
                    float t1 = (j + 1) * MathF.Tau / 48f;
                    Raylib.DrawLine3D(p + (a * MathF.Cos(t0) + b * MathF.Sin(t0)) * length * .68f,
                        p + (a * MathF.Cos(t1) + b * MathF.Sin(t1)) * length * .68f, color);
                }
            }
            else
            {
                Raylib.DrawLine3D(p, p + dir * length, color);
                Raylib.DrawCubeV(p + dir * length, new Vector3(.27f), color);
            }
        }
    }

    private static int PickNewGizmo(Vector2 mouse)
    {
        Vector3? center = SelectedPivot();
        if (!center.HasValue) return -1;
        Vector3 p = center.Value;
        float length = Math.Clamp(Vector3.Distance(eye, p) * .075f, 1.2f, 5f);
        for (int i = 0; i < 3; i++)
        {
            Vector2 a = Raylib.GetWorldToScreen(p, camera);
            Vector2 b = Raylib.GetWorldToScreen(p + Axis(i) * length, camera);
            if (mode == 1)
            {
                // Click near the ring circumference instead of the old straight handle.
                Vector3 sideA = Axis((i + 1) % 3);
                Vector3 sideB = Axis((i + 2) % 3);
                for (int j = 0; j < 32; j++)
                {
                    float t = j * MathF.Tau / 32f;
                    Vector2 projected = Raylib.GetWorldToScreen(p + (sideA * MathF.Cos(t) +
                        sideB * MathF.Sin(t)) * length * .68f, camera);
                    if (Vector2.DistanceSquared(projected, mouse) <= 12f * 12f) return i;
                }
            }
            else
            {
                Vector2 ab = b - a;
                float t = ab.LengthSquared() > 1f ?
                    Math.Clamp(Vector2.Dot(mouse - a, ab) / ab.LengthSquared(), .25f, 1.2f) : 0f;
                if (Vector2.Distance(mouse, a + ab * t) <= 13f) return i;
            }
        }
        return -1;
    }

    private static void DrawWorldMarkers()
    {
        foreach (MapCollectible item in document.Collectibles)
        {
            if (!item.Visible) continue;
            Raylib.DrawSphere(item.Position, .31f,
                item == activeItem ? new Color(255, 235, 145, 255) : Color.Gold);
            Raylib.DrawSphereWires(item.Position, .39f, 8, 8, Color.Yellow);
        }
        foreach (MapPortalNode node in document.PortalNodes)
        {
            Vector3 normal = Vector3.Normalize(node.Normal);
            Vector3 tangent = Vector3.Cross(MathF.Abs(normal.Y) < .95f ? Vector3.UnitY : Vector3.UnitX, normal);
            tangent = Vector3.Normalize(tangent);
            Vector3 bitangent = Vector3.Cross(normal, tangent);
            for (int j = 0; j < 40; j++)
            {
                float t0 = j * MathF.Tau / 40f, t1 = (j + 1) * MathF.Tau / 40f;
                Vector3 a = node.Center + node.Radius * (tangent * MathF.Cos(t0) + bitangent * MathF.Sin(t0));
                Vector3 b = node.Center + node.Radius * (tangent * MathF.Cos(t1) + bitangent * MathF.Sin(t1));
                Raylib.DrawLine3D(a, b, node == activePortal ? Color.Yellow : Color.SkyBlue);
            }
            Raylib.DrawSphere(node.Center, node == activePortal ? .28f : .21f,
                node == activePortal ? Color.Yellow : Color.SkyBlue);
            if (node.DestinationId is Guid targetId)
            {
                MapPortalNode? target = document.PortalNodes.Find(x => x.Id == targetId);
                if (target == null) continue;
                Vector3 diff = target.Center - node.Center;
                if (diff.LengthSquared() < .2f) continue;
                Vector3 tip = target.Center - Vector3.Normalize(diff) * .5f;
                Raylib.DrawLine3D(node.Center, tip, new Color(170, 120, 250, 255));
                Raylib.DrawSphere(tip, .21f, Color.Purple);
            }
        }
        foreach (MapObject obj in selectedObjects)
            Raylib.DrawCubeWiresV(obj.Position, obj.Size, new Color(245, 210, 100, 255));
        DrawGizmo();
    }

    private static void AddCollectible()
    {
        MapCollectible item = new() { Name = "Esfera " + (document.Collectibles.Count + 1),
            Position = eye + Forward() * 6f };
        document.Collectibles.Add(item);
        SelectItem(item);
        Changed();
        Toast("Esfera adicionada. Selecione e posicione.");
    }

    private static void AddPortalNode()
    {
        MapPortalNode node = new()
        {
            Name = "Portal " + (document.PortalNodes.Count + 1),
            Center = eye + Forward() * 7f,
            Normal = Vector3.UnitZ, Radius = 2.36f
        };
        document.PortalNodes.Add(node);
        SelectPortal(node);
        Changed();
        Toast("Portal criado. Defina seu destino na aba Portais.");
    }

    private static void BeginLink()
    {
        mainTab = 2; hierarchyTab = 1;
        linking = true; linkSource = activePortal;
        Toast(activePortal != null ? "Clique no portal de destino" : "Clique na origem e depois no destino");
    }

    private static void ReverseSelectedLink()
    {
        MapPortalNode? source = activePortal;
        if (source != null && source.DestinationId == null)
            source = document.PortalNodes.Find(x => x.DestinationId == activePortal!.Id);
        if (source == null || source.DestinationId == null)
        {
            Toast("Selecione uma ligacao para inverter", true);
            return;
        }
        Guid destination = source.DestinationId.Value;
        document.ReverseLink(source.Id);
        activePortal = document.PortalNodes.Find(x => x.Id == destination);
        Changed();
        Toast("Origem e destino invertidos");
    }

    private static void DeleteSelectedEntities()
    {
        if (selectedObjects.Count > 0)
        {
            foreach (MapObject o in selectedObjects) document.Objects.Remove(o);
            Toast(selectedObjects.Count + " objeto(s) excluido(s)");
            ClearSelection(); Changed(); return;
        }
        if (activeItem != null)
        {
            document.Collectibles.Remove(activeItem);
            ClearSelection(); Changed(); Toast("Coletavel excluido"); return;
        }
        if (activePortal == null) return;
        Guid id = activePortal.Id;
        foreach (MapPortalNode node in document.PortalNodes)
            if (node.DestinationId == id) node.DestinationId = null;
        document.PortalNodes.Remove(activePortal);
        ClearSelection(); Changed(); Toast("Portal excluido e ligacoes atualizadas");
    }

    private static void DuplicateSelectedEntities()
    {
        if (selectedObjects.Count == 0)
        {
            if (activeItem != null)
            {
                MapCollectible item = new() { Name = activeItem.Name + " copia",
                    Position = activeItem.Position + Vector3.UnitX, RequiredPortal = activeItem.RequiredPortal };
                document.Collectibles.Add(item);
                SelectItem(item);
                Changed(); Toast("Esfera duplicada");
            }
            return;
        }
        List<MapObject> copied = new();
        foreach (MapObject source in selectedObjects)
        {
            MapObject copy = new()
            {
                Name = source.Name + " copia", Kind = source.Kind,
                Position = source.Position + new Vector3(1f, 0f, 1f),
                Size = source.Size, Rotation = source.Rotation, Solid = source.Solid,
                Surface = source.Surface, Visible = source.Visible
            };
            document.Objects.Add(copy);
            copied.Add(copy);
        }
        selectedObjects.Clear();
        selectedObjects.AddRange(copied);
        selected = selectedObjects[^1];
        Changed();
        Toast(copied.Count + " objeto(s) duplicado(s)");
    }
}


internal static unsafe partial class Program
{
    private static Action<float>? nodeNumberSetter;

    private static void DrawModernInterface()
    {
        int w = Raylib.GetScreenWidth();
        int h = Raylib.GetScreenHeight();
        float right = ViewRight;
        Color dark = new(24, 27, 33, 255);
        Color panel = new(36, 40, 48, 255);

        Raylib.DrawRectangle(0, 0, w, 82, dark);
        string[] top = { "Criar", "Ferramentas", "Portais", "Arquivo" };
        for (int i = 0; i < top.Length; i++)
        {
            Rectangle r = new(12 + i * 130, 9, 125, 29);
            bool active = mainTab == i;
            Raylib.DrawRectangleRec(r, active ? new Color(64, 97, 135, 255) :
                new Color(44, 49, 60, 255));
            if (Button(r, top[i])) mainTab = i;
        }

        float bx = 15f;
        if (mainTab == 0)
        {
            if (ToolbarButton(ref bx, "Cubo", 94)) Add(MapObjectKind.Box);
            if (ToolbarButton(ref bx, "Rampa", 94)) Add(MapObjectKind.Ramp);
            if (ToolbarButton(ref bx, "Cilindro", 112)) Add(MapObjectKind.Cylinder);
            if (ToolbarButton(ref bx, "Escada", 100)) Add(MapObjectKind.Stairs);
            if (ToolbarButton(ref bx, "Esc. vertical", 128)) Add(MapObjectKind.Ladder);
            if (ToolbarButton(ref bx, "Esfera", 97)) AddCollectible();
            if (ToolbarButton(ref bx, "Portal", 96)) AddPortalNode();
        }
        else if (mainTab == 1)
        {
            if (ToolbarButton(ref bx, mode == 0 ? "[Mover] (1)" : "Mover (1)", 132)) ChangeMode(0);
            if (ToolbarButton(ref bx, mode == 1 ? "[Girar] (2)" : "Girar (2)", 132)) ChangeMode(1);
            if (ToolbarButton(ref bx, mode == 2 ? "[Tamanho] (3)" : "Tamanho (3)", 148)) ChangeMode(2);
            if (ToolbarButton(ref bx, uniform ? "Escala uniforme: SIM" : "Escala uniforme: NAO", 205))
                uniform = !uniform;
            if (ToolbarButton(ref bx, "Duplicar Ctrl+D", 160)) DuplicateSelectedEntities();
            if (ToolbarButton(ref bx, "Excluir Del", 135)) DeleteSelectedEntities();
        }
        else if (mainTab == 2)
        {
            if (ToolbarButton(ref bx, "Novo portal", 133)) AddPortalNode();
            if (ToolbarButton(ref bx, linking ? "[Selecionar destino]" : "Vincular origem -> destino", 240))
                BeginLink();
            if (ToolbarButton(ref bx, "Inverter sentido", 171)) ReverseSelectedLink();
            if (ToolbarButton(ref bx, "Cancelar vinculo", 171))
            {
                linking = false; linkSource = null;
                Toast("Modo de conexao cancelado");
            }
        }
        else
        {
            if (ToolbarButton(ref bx, "Salvar Ctrl+S", 167)) Save();
            if (ToolbarButton(ref bx, "Reabrir mapa", 155)) Reload();
            DrawTextSmooth("O jogo carrega map.json apos salvar.", (int)bx + 18, 52, 17, Color.LightGray);
        }
        DrawTextSmooth(linking ? "LIGACAO: selecione origem e destino na cena"
                    : $"MODO: {(mode == 0 ? "MOVER" : mode == 1 ? "GIRAR" : "TAMANHO")}",
            15, 88, 18, linking ? Color.Yellow : Color.White);
        DrawMarquee();

        Raylib.DrawRectangle((int)right, 82, (int)PanelWidth, h - 114, panel);
        string[] categories = { "Objetos", "Portais", "Esferas" };
        for (int i = 0; i < 3; i++)
        {
            Rectangle r = new(right + 10 + i * 132, 94, 125, 28);
            if (Button(r, hierarchyTab == i ? "[" + categories[i] + "]" : categories[i]))
            {
                hierarchyTab = i;
                objectScroll = 0;
            }
        }

        int total = hierarchyTab switch
        {
            0 => document.Objects.Count,
            1 => document.PortalNodes.Count,
            _ => document.Collectibles.Count
        };
        DrawTextSmooth(total + " elemento(s)" +
            (selectedObjects.Count > 1 ? " | " + selectedObjects.Count + " selecionados" : ""),
            (int)right + 15, 130, 16, Color.LightGray);

        int listTop = 155;
        int maxItems = 8;
        objectScroll = Math.Clamp(objectScroll, 0, Math.Max(0, total - maxItems));
        for (int i = 0; i < maxItems && i + objectScroll < total; i++)
        {
            int index = i + objectScroll;
            string title;
            bool selectedRow;
            if (hierarchyTab == 0)
            {
                MapObject o = document.Objects[index];
                title = o.Name + " (" + o.Kind + ")";
                selectedRow = selectedObjects.Contains(o);
            }
            else if (hierarchyTab == 1)
            {
                MapPortalNode p = document.PortalNodes[index];
                MapPortalNode? destination = p.DestinationId is Guid destId
                    ? document.PortalNodes.Find(n => n.Id == destId) : null;
                title = p.Name + (destination == null ? " (sem ligacao)" : " > " + destination.Name);
                selectedRow = p == activePortal;
            }
            else
            {
                MapCollectible item = document.Collectibles[index];
                title = item.Name;
                selectedRow = item == activeItem;
            }
            Rectangle rect = new(right + 11, listTop + i * 27, PanelWidth - 22, 25);
            if (selectedRow)
                Raylib.DrawRectangleRec(rect, new Color(56, 104, 159, 255));
            if (Button(rect, title, true))
            {
                bool ctrl = Key(341) || Key(345);
                if (hierarchyTab == 0) SelectObject(document.Objects[index], ctrl);
                else if (hierarchyTab == 1) SelectPortal(document.PortalNodes[index]);
                else SelectItem(document.Collectibles[index]);
            }
        }
        Vector2 mouse = Raylib.GetMousePosition();
        if (mouse.X >= right && mouse.Y >= listTop && mouse.Y < listTop + 8 * 27)
            objectScroll -= (int)Raylib.GetMouseWheelMove() * 2;

        int tabY = listTop + maxItems * 27 + 10;
        Raylib.DrawLine((int)right + 10, tabY - 4, w - 10, tabY - 4, Color.Gray);
        string[] tabs = activePortal != null ?
            new[] { "Posicao", "Ligacao", "Orientacao" } :
            activeItem != null ? new[] { "Posicao", "Pontuacao", "Acoes" } :
            new[] { "Transformar", "Faces", "Objeto" };
        for (int i = 0; i < 3; i++)
        {
            if (Button(new Rectangle(right + 10 + i * 132, tabY + 4, 125, 28),
                inspectorTab == i ? "[" + tabs[i] + "]" : tabs[i]))
                inspectorTab = i;
        }
        int y = tabY + 45;
        if (activePortal != null) DrawPortalInspector(activePortal, (int)right + 15, ref y);
        else if (activeItem != null) DrawItemInspector(activeItem, (int)right + 15, ref y);
        else if (selected != null) DrawObjectInspector((int)right + 15, ref y);
        else DrawTextSmooth("Clique na cena para selecionar.", (int)right + 15, y, 18, Color.LightGray);

        Raylib.DrawRectangle(0, h - 32, w, 32, new Color(22, 25, 30, 255));
        DrawTextSmooth("Ctrl: selecionar varios | Arrastar no vazio: area | 1/2/3: ferramenta | Ctrl+S: salvar",
            16, h - 24, 16, Color.White);
        if (modified)
            DrawTextSmooth("* Alteracoes nao salvas", w - 260, h - 24, 16, Color.Yellow);
        DrawToast();
    }

    private static bool ToolbarButton(ref float x, string text, float width)
    {
        bool hit = Button(new Rectangle(x, 45, width, 30), text);
        x += width + 7;
        return hit;
    }

    private static void DrawObjectInspector(int x, ref int y)
    {
        if (selected == null) return;
        DrawTextSmooth(selectedObjects.Count > 1 ? selectedObjects.Count + " objetos selecionados" :
            selected.Name, x + 4, y, 19, Color.White);
        y += 30;
        if (inspectorTab == 0)
        {
            if (Button(new Rectangle(x, y, 118, 26), mode == 0 ? "[Mover]" : "Mover"))
                ChangeMode(0);
            if (Button(new Rectangle(x + 124, y, 118, 26), mode == 1 ? "[Girar]" : "Girar"))
                ChangeMode(1);
            if (Button(new Rectangle(x + 248, y, 118, 26), mode == 2 ? "[Tamanho]" : "Tamanho"))
                ChangeMode(2);
            y += 36;
            NumericRow(selected, x, ref y, "PX", 0, 0);
            NumericRow(selected, x, ref y, "PY", 0, 1);
            NumericRow(selected, x, ref y, "PZ", 0, 2);
            y += 8;
            NumericRow(selected, x, ref y, "RX", 1, 0);
            NumericRow(selected, x, ref y, "RY", 1, 1);
            NumericRow(selected, x, ref y, "RZ", 1, 2);
            y += 8;
            NumericRow(selected, x, ref y, "SX", 2, 0);
            NumericRow(selected, x, ref y, "SY", 2, 1);
            NumericRow(selected, x, ref y, "SZ", 2, 2);
        }
        else if (inspectorTab == 1)
        {
            DrawTextSmooth("Redimensionar faces individuais", x + 3, y, 18, Color.LightGray);
            y += 34;
            for (int axis = 0; axis < 3; axis++)
            {
                string axisName = axis == 0 ? "X" : axis == 1 ? "Y" : "Z";
                for (int side = -1; side <= 1; side += 2)
                {
                    if (Button(new Rectangle(x + 3, y, 158, 29), axisName + (side < 0 ? " negativo  -" : " positivo  -")))
                        foreach (MapObject obj in selectedObjects) ChangeFace(obj, axis, side, -.20f);
                    if (Button(new Rectangle(x + 170, y, 158, 29), axisName + (side < 0 ? " negativo  +" : " positivo  +")))
                        foreach (MapObject obj in selectedObjects) ChangeFace(obj, axis, side, .20f);
                    y += 38;
                }
            }
            if (Button(new Rectangle(x + 3, y, 187, 27), uniform ? "[Escala uniforme]" : "Escala uniforme"))
                uniform = !uniform;
        }
        else
        {
            if (Button(new Rectangle(x, y, 175, 31), "Duplicar selecionados")) DuplicateSelectedEntities();
            if (Button(new Rectangle(x + 183, y, 175, 31), "Excluir selecionados")) DeleteSelectedEntities();
            y += 42;
            if (Button(new Rectangle(x, y, 173, 28), selected.Visible ? "Visivel: SIM" : "Visivel: NAO"))
            {
                foreach (MapObject obj in selectedObjects) obj.Visible = !obj.Visible;
                Changed();
            }
            if (Button(new Rectangle(x + 184, y, 173, 28), selected.Solid ? "Colisao: SIM" : "Colisao: NAO"))
            {
                foreach (MapObject obj in selectedObjects) obj.Solid = !obj.Solid;
                Changed();
            }
        }
    }

    private static void NodeRow(int x, ref int y, string title, string editKey,
        float value, Action<float> assign, float step = .25f)
    {
        DrawTextSmooth(title, x + 5, y + 4, 17, Color.LightGray);
        if (Button(new Rectangle(x + 84, y, 32, 26), "-")) assign(value - step);
        Rectangle field = new(x + 124, y, 164, 26);
        bool active = editing == "node:" + editKey;
        Raylib.DrawRectangleRec(field, active ? new Color(67, 97, 122, 255)
            : new Color(48, 52, 62, 255));
        DrawTextSmooth(active ? editBuffer : value.ToString("0.###"),
            (int)field.X + 9, y + 5, 17, Color.White);
        if (MousePress(0) && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), field))
        {
            editing = "node:" + editKey;
            editBuffer = value.ToString("0.###");
            nodeNumberSetter = assign;
        }
        if (Button(new Rectangle(x + 298, y, 32, 26), "+")) assign(value + step);
        y += 32;
    }

    private static void HandleNodeTextEdit()
    {
        if (editing == null || !editing.StartsWith("node:")) return;
        int cp;
        while ((cp = Raylib.GetCharPressed()) > 0)
        {
            if (((cp >= '0' && cp <= '9') || cp == '-' || cp == '.' || cp == ',') &&
                editBuffer.Length < 17) editBuffer += (char)cp;
        }
        if (Press(259) && editBuffer.Length > 0) editBuffer = editBuffer[..^1];
        if (Press(256)) { editing = null; nodeNumberSetter = null; return; }
        if (!(Press(257) || Press(335))) return;
        if (float.TryParse(editBuffer.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float value) &&
            float.IsFinite(value))
            nodeNumberSetter?.Invoke(value);
        editing = null;
        nodeNumberSetter = null;
    }

    private static void DrawPortalInspector(MapPortalNode portal, int x, ref int y)
    {
        DrawTextSmooth(portal.Name, x, y, 20, Color.White);
        y += 32;
        if (inspectorTab == 0)
        {
            NodeRow(x, ref y, "X", "PX", portal.Center.X, v => { portal.Center = new Vector3(v, portal.Center.Y, portal.Center.Z); Changed(); });
            NodeRow(x, ref y, "Y", "PY", portal.Center.Y, v => { portal.Center = new Vector3(portal.Center.X, v, portal.Center.Z); Changed(); });
            NodeRow(x, ref y, "Z", "PZ", portal.Center.Z, v => { portal.Center = new Vector3(portal.Center.X, portal.Center.Y, v); Changed(); });
            y += 12;
            NodeRow(x, ref y, "Raio", "PR", portal.Radius, v => { if (v >= .5f) { portal.Radius = v; Changed(); } }, .2f);
        }
        else if (inspectorTab == 1)
        {
            MapPortalNode? destination = portal.DestinationId is Guid targetId ?
                document.PortalNodes.Find(x => x.Id == targetId) : null;
            DrawTextSmooth(destination == null ? "Destino: sem ligacao" :
                "Destino: " + destination.Name, x, y, 17, Color.LightGray);
            y += 40;
            if (Button(new Rectangle(x, y, 350, 30), "Selecionar destino na cena")) BeginLink();
            y += 40;
            if (Button(new Rectangle(x, y, 350, 30), "Inverter origem e destino")) ReverseSelectedLink();
            y += 40;
            if (Button(new Rectangle(x, y, 350, 30), "Remover ligacao"))
            {
                portal.DestinationId = null; Changed(); Toast("Ligacao removida");
            }
            y += 40;
            if (Button(new Rectangle(x, y, 350, 30), portal.OneWay ? "Sentido: apenas origem > destino" :
                "Sentido: bidirecional"))
            {
                portal.OneWay = !portal.OneWay;
                Changed(); Toast(portal.OneWay ? "Portal unidirecional" : "Portal bidirecional");
            }
        }
        else
        {
            DrawTextSmooth("Orientacao da abertura", x, y, 18, Color.LightGray);
            y += 32;
            Vector3[] vectors = { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY,
                -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ };
            string[] labels = { "+X", "-X", "+Y (chao)", "-Y", "+Z", "-Z" };
            for (int i = 0; i < 6; i++)
            {
                int j = i;
                int col = i % 2, line = i / 2;
                if (Button(new Rectangle(x + col * 180, y + line * 38, 171, 32), labels[i]))
                {
                    portal.Normal = vectors[j];
                    Changed();
                    Toast("Orientacao do portal atualizada");
                }
            }
        }
    }

    private static void DrawItemInspector(MapCollectible item, int x, ref int y)
    {
        DrawTextSmooth(item.Name, x, y, 20, Color.White); y += 32;
        if (inspectorTab == 0)
        {
            NodeRow(x, ref y, "X", "IX", item.Position.X,
                v => { item.Position = new Vector3(v, item.Position.Y, item.Position.Z); Changed(); });
            NodeRow(x, ref y, "Y", "IY", item.Position.Y,
                v => { item.Position = new Vector3(item.Position.X, v, item.Position.Z); Changed(); });
            NodeRow(x, ref y, "Z", "IZ", item.Position.Z,
                v => { item.Position = new Vector3(item.Position.X, item.Position.Y, v); Changed(); });
        }
        else if (inspectorTab == 1)
        {
            DrawTextSmooth("Liberar coletavel depois do portal:", x, y, 17, Color.LightGray);
            y += 37;
            NodeRow(x, ref y, "Par", "IP", item.RequiredPortal,
                v => { item.RequiredPortal = Math.Clamp((int)v, -1, document.PortalNodes.Count); Changed(); }, 1f);
            DrawTextSmooth("-1 = disponivel desde o inicio", x, y + 4, 16, Color.LightGray);
        }
        else
        {
            if (Button(new Rectangle(x, y, 350, 33), "Duplicar esfera")) DuplicateSelectedEntities();
            y += 41;
            if (Button(new Rectangle(x, y, 350, 33), item.Visible ? "Visivel: SIM" : "Visivel: NAO"))
            { item.Visible = !item.Visible; Changed(); }
            y += 41;
            if (Button(new Rectangle(x, y, 350, 33), "Excluir esfera")) DeleteSelectedEntities();
        }
    }
}
