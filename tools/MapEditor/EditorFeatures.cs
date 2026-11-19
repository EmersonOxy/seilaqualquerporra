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
