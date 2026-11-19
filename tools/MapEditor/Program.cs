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
    private static MapDocument document = SceneMapIO.CreateDefault();
    private static SceneDefinition preview = document.ToScene();
    private static readonly string mapPath = SceneMapIO.DefaultPath;
    private static MapObject? selected;
    private static Model cube;
    private static Model cylinder;
    private static Camera3D camera;
    private static Vector3 eye = new(12f, 11f, 21f);
    private static float yaw = -.4f, pitch = -.35f;
    private static bool looking, modified, snap = true, uniform;
    private static int mode; // 0 move, 1 rotate, 2 size
    private static int objectScroll, draggedAxis = -1;
    private static bool dragging;
    private static string status = "Clique para selecionar. Salve com Ctrl+S.";
    private static string? editing;
    private static string editBuffer = "";
    private const float PanelWidth = 410f;
    private const float UiScale = 1f;
    private static float ViewRight => Raylib.GetScreenWidth() - PanelWidth;

    public static void Main()
    {
        Raylib.SetConfigFlags((ConfigFlags)4);
        Raylib.InitWindow(1500, 900, "Denffy - Editor de Mapas");
        Raylib.SetWindowMinSize(1100, 700);
        Raylib.SetTargetFPS(90);
        InitializeEditorFont();
        try
        {
            if (File.Exists(mapPath)) document = SceneMapIO.Load(mapPath);
            preview = document.ToScene();
        }
        catch (Exception ex) { status = "Erro abrindo mapa: " + ex.Message; }

        cube = Raylib.LoadModelFromMesh(Raylib.GenMeshCube(1f, 1f, 1f));
        cylinder = Raylib.LoadModelFromMesh(Raylib.GenMeshCylinder(.5f, 1f, 20));
        while (!Down(Raylib.WindowShouldClose()))
        {
            float dt = Math.Clamp(Raylib.GetFrameTime(), .001f, .05f);
            Update(dt);
            toastRemaining = MathF.Max(0f, toastRemaining - dt);
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(31, 33, 38, 255));
            DrawScene();
            DrawModernInterface();
            Raylib.EndDrawing();
        }
        Raylib.UnloadModel(cube);
        Raylib.UnloadModel(cylinder);
        if (smoothFont) Raylib.UnloadFont(uiFont);
        Raylib.CloseWindow();
    }

    private static bool Down(CBool b) => (sbyte)b != 0;
    private static bool Key(int k) => Down(Raylib.IsKeyDown((KeyboardKey)k));
    private static bool Press(int k) => Down(Raylib.IsKeyPressed((KeyboardKey)k));
    private static bool MouseDown(int k) => Down(Raylib.IsMouseButtonDown((MouseButton)k));
    private static bool MousePress(int k) => Down(Raylib.IsMouseButtonPressed((MouseButton)k));

    private static void Update(float dt)
    {
        Vector2 mouse = Raylib.GetMousePosition();
        bool overView = mouse.X < ViewRight && mouse.Y > 105;
        bool ctrl = Key(341) || Key(345);
        bool shift = Key(340) || Key(344);

        if (ctrl && Press(83)) Save();
        if (ctrl && Press(68) && (selected != null || activeItem != null)) DuplicateSelectedEntities();
        if (editing == null && (selected != null || activeItem != null || activePortal != null) && Press(261)) DeleteSelectedEntities();
        if (editing == null && Press(49)) ChangeMode(0);
        if (editing == null && Press(50)) ChangeMode(1);
        if (editing == null && Press(51)) ChangeMode(2);
        if (editing == null && Press(70) && SelectedPivot() is Vector3 target)
            eye = target + new Vector3(6f, 5f, 9f);

        if (!Down(Raylib.IsWindowFocused())) looking = false;
        if (overView && MousePress(1) && !looking)
        {
            looking = true;
            Raylib.DisableCursor();
        }
        if (looking && !MouseDown(1))
        {
            looking = false;
            Raylib.EnableCursor();
        }
        if (looking)
        {
            Vector2 d = Raylib.GetMouseDelta();
            yaw += d.X * .0023f;
            pitch = Math.Clamp(pitch - d.Y * .0023f, -1.48f, 1.48f);
            Vector3 f = Forward();
            Vector3 right = Vector3.Normalize(Vector3.Cross(f, Vector3.UnitY));
            Vector3 lateral = new(right.X, 0f, right.Z);
            Vector3 flat = Vector3.Normalize(new Vector3(f.X, 0f, f.Z));
            Vector3 move = Vector3.Zero;
            if (Key(87)) move += flat;
            if (Key(83)) move -= flat;
            if (Key(68)) move += lateral;
            if (Key(65)) move -= lateral;
            if (Key(69)) move += Vector3.UnitY;
            if (Key(81)) move -= Vector3.UnitY;
            if (move.LengthSquared() > 0f)
                eye += Vector3.Normalize(move) * dt * (shift ? 18f : 8f);
        }
        camera = new Camera3D(eye, eye + Forward(), Vector3.UnitY, 65f, (CameraProjection)0);

        if (overView && !looking && editing == null)
        {
            if (MousePress(0))
            {
                draggedAxis = !ctrl && !linking && SelectedPivot().HasValue
                    ? PickNewGizmo(mouse) : -1;
                dragging = draggedAxis >= 0;
                if (!dragging) ViewClick(mouse, ctrl);
            }
            if (dragging && MouseDown(0) && SelectedPivot() is Vector3 pivot)
            {
                Vector2 delta = Raylib.GetMouseDelta();
                Vector3 axis = Axis(draggedAxis);
                Vector2 a = Raylib.GetWorldToScreen(pivot, camera);
                Vector2 b = Raylib.GetWorldToScreen(pivot + axis * 2f, camera);
                Vector2 ab = b - a;
                if (ab.LengthSquared() > 1f || mode == 1)
                {
                    float change = mode == 1 ? (delta.X - delta.Y) * .012f :
                        Vector2.Dot(delta, Vector2.Normalize(ab)) * (shift ? .005f : .024f);
                    if (MathF.Abs(change) > .00001f)
                        TransformSelection(draggedAxis, change);
                }
            }
        }
        UpdateMarquee(mouse);
        if (!MouseDown(0)) { dragging = false; draggedAxis = -1; }
        if (editing != null)
        {
            if (editing.StartsWith("node:")) HandleNodeTextEdit();
            else HandleTextEdit();
        }
    }

    private static Vector3 Forward() =>
        Vector3.Normalize(new Vector3(MathF.Sin(yaw) * MathF.Cos(pitch),
            MathF.Sin(pitch), -MathF.Cos(yaw) * MathF.Cos(pitch)));
    private static Vector3 Axis(int i) =>
        i == 0 ? Vector3.UnitX : i == 1 ? Vector3.UnitY : Vector3.UnitZ;

    private static MapObject? Pick(Vector2 mouse)
    {
        Ray ray = Raylib.GetMouseRay(mouse, camera);
        MapObject? answer = null;
        float nearest = float.PositiveInfinity;
        foreach (MapObject o in document.Objects)
        {
            if (!o.Visible) continue;
            float dist = Intersect(ray, o);
            if (dist >= 0f && dist < nearest)
            {
                nearest = dist;
                answer = o;
            }
        }
        return answer;
    }

    private static float Intersect(Ray ray, MapObject o)
    {
        Quaternion rotation = SceneTransform.Orientation(o.Rotation);
        Quaternion inverse = Quaternion.Inverse(rotation);
        Vector3 start = Vector3.Transform(ray.Position - o.Position, inverse);
        Vector3 direction = Vector3.Transform(ray.Direction, inverse);
        Vector3 half = o.Size * .5f;
        float near = 0f, far = 10000f;
        for (int i = 0; i < 3; i++)
        {
            float origin = i == 0 ? start.X : i == 1 ? start.Y : start.Z;
            float dir = i == 0 ? direction.X : i == 1 ? direction.Y : direction.Z;
            float extent = i == 0 ? half.X : i == 1 ? half.Y : half.Z;
            if (MathF.Abs(dir) < .000001f)
            {
                if (MathF.Abs(origin) > extent) return -1f;
                continue;
            }
            float t1 = (-extent - origin) / dir;
            float t2 = (extent - origin) / dir;
            if (t1 > t2) (t1, t2) = (t2, t1);
            near = MathF.Max(near, t1);
            far = MathF.Min(far, t2);
            if (near > far) return -1f;
        }
        return near;
    }

    private static int PickGizmo(Vector2 mouse)
    {
        if (selected == null) return -1;
        for (int i = 0; i < 3; i++)
        {
            Vector2 a = Raylib.GetWorldToScreen(selected.Position, camera);
            Vector2 b = Raylib.GetWorldToScreen(selected.Position + Axis(i) * 2f, camera);
            Vector2 line = b - a;
            float t = line.LengthSquared() > .01f ?
                Math.Clamp(Vector2.Dot(mouse - a, line) / line.LengthSquared(), .1f, 1.2f) : 0f;
            if (Vector2.Distance(mouse, a + line * t) < 12f) return i;
        }
        return -1;
    }

    private static void DrawScene()
    {
        Raylib.BeginMode3D(camera);
        Raylib.DrawPlane(new Vector3(0f, -.026f, 0f),
            new Vector2(document.BaseSize, document.BaseSize), new Color(83, 88, 93, 255));
        Raylib.DrawGrid((int)(document.BaseSize / 2), 2f);

        foreach (SceneBox b in preview.Boxes)
            DrawModelOriented(cube, b.Center, b.RotationDegrees, b.Size,
                b.Surface == Surface.Steel ? new Color(115, 123, 137, 255) :
                b.Surface == Surface.Edge ? new Color(180, 151, 99, 255) :
                new Color(121, 127, 136, 255));

        foreach (SceneRamp r in preview.Ramps)
        {
            Quaternion q = Quaternion.CreateFromAxisAngle(r.RotationAxis, r.AngleDegrees * MathF.PI / 180f);
            SceneTransform.ToAxisAngle(q, out Vector3 ax, out float angle);
            Raylib.DrawModelEx(cube, r.Center, ax, angle, r.BoxSize, new Color(190, 160, 107, 255));
        }

        foreach (SceneCylinder c in preview.Cylinders)
        {
            Quaternion q = SceneTransform.Orientation(c.RotationDegrees);
            Vector3 bottom = c.Center - Vector3.Transform(Vector3.UnitY * (c.Size.Y * .5f), q);
            DrawModelOriented(cylinder, bottom, c.RotationDegrees, c.Size,
                new Color(115, 162, 169, 255));
        }

        foreach (SceneLadder l in preview.Ladders) DrawLadder(l);

        foreach (ScenePortalPair p in preview.PortalPairs)
        {
            Raylib.DrawSphereWires(p.First.Center, .18f, 5, 7, Color.SkyBlue);
            Raylib.DrawSphereWires(p.Second.Center, .18f, 5, 7, Color.SkyBlue);
        }

        DrawWorldMarkers();
        Raylib.EndMode3D();
    }

    private static void DrawModelOriented(Model model, Vector3 center, Vector3 angles,
        Vector3 size, Color color)
    {
        SceneTransform.ToAxisAngle(SceneTransform.Orientation(angles),
            out Vector3 axis, out float degrees);
        Raylib.DrawModelEx(model, center, axis, degrees, size, color);
    }

    private static void DrawLadder(SceneLadder ladder)
    {
        Quaternion q = SceneTransform.Orientation(ladder.RotationDegrees);
        float h = ladder.Size.Y, w = ladder.Size.X;
        Vector3 left = Vector3.Transform(new Vector3(-w / 2f, 0, 0), q);
        Vector3 right = Vector3.Transform(new Vector3(w / 2f, 0, 0), q);
        Raylib.DrawLine3D(ladder.Center + left - Vector3.UnitY * h * .5f,
            ladder.Center + left + Vector3.UnitY * h * .5f, Color.Yellow);
        Raylib.DrawLine3D(ladder.Center + right - Vector3.UnitY * h * .5f,
            ladder.Center + right + Vector3.UnitY * h * .5f, Color.Yellow);
        for (float y = -h * .5f + .25f; y < h * .5f; y += .32f)
            Raylib.DrawLine3D(ladder.Center + left + Vector3.UnitY * y,
                ladder.Center + right + Vector3.UnitY * y, Color.Orange);
    }

    private static void DrawInterface()
    {
        float right = ViewRight;
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), 48,
            new Color(22, 24, 28, 255));
        float x = 12f;
        if (Button(new Rectangle(x, 8, 86, 31), "SALVAR")) Save(); x += 92f;
        if (Button(new Rectangle(x, 8, 86, 31), "REABRIR")) Reload(); x += 98f;
        if (Button(new Rectangle(x, 8, 76, 31), "CUBO")) Add(MapObjectKind.Box); x += 80f;
        if (Button(new Rectangle(x, 8, 82, 31), "RAMPA")) Add(MapObjectKind.Ramp); x += 86f;
        if (Button(new Rectangle(x, 8, 102, 31), "CILINDRO")) Add(MapObjectKind.Cylinder); x += 106f;
        if (Button(new Rectangle(x, 8, 88, 31), "ESCADA")) Add(MapObjectKind.Stairs); x += 92f;
        if (Button(new Rectangle(x, 8, 113, 31), "ESC. VERT.")) Add(MapObjectKind.Ladder);

        Raylib.DrawRectangle((int)right, 48, (int)PanelWidth,
            Raylib.GetScreenHeight(), new Color(38, 41, 48, 255));
        DrawTextSmooth("HIERARQUIA", (int)right + 14, 62, 20, Color.White);
        DrawTextSmooth($"{document.Objects.Count} objetos", (int)right + 280, 66, 15, Color.LightGray);
        int listTop = 95;
        int visible = 9;
        objectScroll = Math.Clamp(objectScroll, 0, Math.Max(0, document.Objects.Count - visible));
        for (int i = 0; i < visible && i + objectScroll < document.Objects.Count; i++)
        {
            MapObject o = document.Objects[i + objectScroll];
            Rectangle rect = new(right + 10, listTop + i * 26, PanelWidth - 20, 24);
            if (selected == o)
                Raylib.DrawRectangleRec(rect, new Color(65, 100, 145, 255));
            if (Button(rect, o.Name + " [" + o.Kind + "]", true)) selected = o;
        }
        Vector2 m = Raylib.GetMousePosition();
        if (m.X >= right && m.Y >= listTop && m.Y < listTop + visible * 26)
            objectScroll -= (int)Raylib.GetMouseWheelMove() * 2;

        int y = listTop + visible * 26 + 12;
        Raylib.DrawLine((int)right + 12, y, Raylib.GetScreenWidth() - 12, y, Color.Gray);
        y += 13;
        if (selected == null)
            DrawTextSmooth("Selecione um objeto para editar.", (int)right + 18, y, 18, Color.LightGray);
        else
            DrawInspector(selected, (int)right + 12, ref y);

        Raylib.DrawRectangle(0, Raylib.GetScreenHeight() - 30, Raylib.GetScreenWidth(), 30,
            new Color(22, 24, 28, 255));
        string info = $"1 mover  2 girar  3 dimensoes  |  Botao direito + WASD navegar  |  Ctrl+S salvar  |  { (modified ? "* NAO SALVO * " : "Salvo") }";
        DrawTextSmooth(info, 13, Raylib.GetScreenHeight() - 22, 14, Color.White);
        if (status.Length > 0)
            DrawTextSmooth(status.Length > 58 ? status[..58] : status,
                Raylib.GetScreenWidth() - 490, Raylib.GetScreenHeight() - 22, 13, Color.LightGray);
    }

    private static void DrawInspector(MapObject o, int x, ref int y)
    {
        DrawTextSmooth(o.Name, x + 5, y, 19, Color.White); y += 32;
        int modeY = y;
        if (Button(new Rectangle(x, y, 103, 27), mode == 0 ? "[MOVER]" : "Mover")) mode = 0;
        if (Button(new Rectangle(x + 111, y, 103, 27), mode == 1 ? "[GIRAR]" : "Girar")) mode = 1;
        if (Button(new Rectangle(x + 222, y, 111, 27), mode == 2 ? "[TAMANHO]" : "Tamanho")) mode = 2;
        y += 37;
        NumericRow(o, x, ref y, "PX", 0, 0);
        NumericRow(o, x, ref y, "PY", 0, 1);
        NumericRow(o, x, ref y, "PZ", 0, 2);
        y += 4;
        NumericRow(o, x, ref y, "RX", 1, 0);
        NumericRow(o, x, ref y, "RY", 1, 1);
        NumericRow(o, x, ref y, "RZ", 1, 2);
        y += 4;
        NumericRow(o, x, ref y, "SX", 2, 0);
        NumericRow(o, x, ref y, "SY", 2, 1);
        NumericRow(o, x, ref y, "SZ", 2, 2);
        y += 2;
        if (Button(new Rectangle(x + 3, y, 126, 26), uniform ? "[ESCALA UNICA]" : "Escala unica"))
            uniform = !uniform;
        if (Button(new Rectangle(x + 134, y, 71, 26), "X 0.9")) { o.Size *= .9f; Changed(); }
        if (Button(new Rectangle(x + 210, y, 71, 26), "X 1.1")) { o.Size *= 1.1f; Changed(); }
        y += 37;
        DrawTextSmooth("FACE INDIVIDUAL (expandir/retrair)", x + 3, y, 15, Color.LightGray);
        y += 23;
        for (int axis = 0; axis < 3; axis++)
        {
            string axisName = axis == 0 ? "X" : axis == 1 ? "Y" : "Z";
            int left = x + 3;
            if (Button(new Rectangle(left, y, 91, 24), axisName + "- | -"))
                ChangeFace(o, axis, -1, -.2f);
            if (Button(new Rectangle(left + 97, y, 91, 24), axisName + "- | +"))
                ChangeFace(o, axis, -1, .2f);
            if (Button(new Rectangle(left + 194, y, 91, 24), axisName + "+ | -"))
                ChangeFace(o, axis, +1, -.2f);
            if (Button(new Rectangle(left + 291, y, 91, 24), axisName + "+ | +"))
                ChangeFace(o, axis, +1, .2f);
            y += 27;
        }
        y += 11;
        if (Button(new Rectangle(x + 3, y, 119, 29), "DUPLICAR")) Duplicate();
        if (Button(new Rectangle(x + 128, y, 119, 29), "EXCLUIR")) Delete();
        if (Button(new Rectangle(x + 253, y, 119, 29), o.Visible ? "VISIVEL" : "OCULTO"))
        {
            o.Visible = !o.Visible;
            Changed();
        }
    }

    private static void NumericRow(MapObject o, int x, ref int y, string label, int property, int axis)
    {
        Vector3 current = property switch { 0 => o.Position, 1 => o.Rotation, _ => o.Size };
        float value = axis == 0 ? current.X : axis == 1 ? current.Y : current.Z;
        DrawTextSmooth(label, x + 4, y + 6, 17, Color.LightGray);
        float step = property == 1 ? 5f : .25f;
        if (Button(new Rectangle(x + 76, y, 32, 26), "-")) Set(value - step);
        Rectangle field = new(x + 114, y, 169, 26);
        bool active = editing == label;
        Raylib.DrawRectangleRec(field, active ? new Color(76, 92, 110, 255) : new Color(50, 54, 62, 255));
        DrawTextSmooth(active ? editBuffer : value.ToString("0.###"),
            (int)field.X + 9, y + 6, 16, Color.White);
        if (MousePress(0) && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), field))
        {
            editing = label;
            editBuffer = value.ToString("0.###");
        }
        if (Button(new Rectangle(x + 289, y, 32, 26), "+")) Set(value + step);
        void Set(float newValue) => SetSelectedNumeric(o, property, axis, newValue);
        y += 29;
    }

    private static void SetSelectedNumeric(MapObject anchor, int property, int axis, float value)
    {
        Vector3 a = property switch { 0 => anchor.Position, 1 => anchor.Rotation, _ => anchor.Size };
        float oldValue = axis == 0 ? a.X : axis == 1 ? a.Y : a.Z;
        float change = value - oldValue;
        if (!float.IsFinite(value) || (property == 2 &&
            selectedObjects.Any(obj =>
            {
                Vector3 v = obj.Size;
                return (axis == 0 ? v.X : axis == 1 ? v.Y : v.Z) + change < .05f;
            })))
            return;
        if (selectedObjects.Count == 0) selectedObjects.Add(anchor);
        foreach (MapObject obj in selectedObjects)
        {
            Vector3 field = property switch { 0 => obj.Position, 1 => obj.Rotation, _ => obj.Size };
            if (axis == 0) field.X += change;
            else if (axis == 1) field.Y += change;
            else field.Z += change;
            if (property == 0) obj.Position = field;
            else if (property == 1) obj.Rotation = field;
            else obj.Size = field;
        }
        Changed();
    }

    private static void HandleTextEdit()
    {
        if (editing == null || selected == null) return;
        int c;
        while ((c = Raylib.GetCharPressed()) > 0)
        {
            if ((c >= '0' && c <= '9') || c == '.' || c == '-' || c == ',')
                if (editBuffer.Length < 16) editBuffer += (char)c;
        }
        if (Press(259) && editBuffer.Length > 0)
            editBuffer = editBuffer[..^1];
        if (Press(256)) { editing = null; return; }
        if (!Press(257) && !Press(335)) return;
        if (float.TryParse(editBuffer.Replace(',', '.'), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float v) && float.IsFinite(v))
        {
            int property = editing[0] == 'P' ? 0 : editing[0] == 'R' ? 1 : 2;
            int axis = editing[1] == 'X' ? 0 : editing[1] == 'Y' ? 1 : 2;
            SetSelectedNumeric(selected, property, axis, v);
        }
        editing = null;
    }

    private static bool Button(Rectangle rect, string text, bool compact = false)
    {
        bool hovered = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), rect);
        Raylib.DrawRectangleRec(rect, hovered ? new Color(74, 88, 111, 255) :
            new Color(56, 62, 74, 255));
        Raylib.DrawRectangleLinesEx(rect, 1f, new Color(88, 96, 107, 255));
        DrawTextSmooth(text.Length > 42 ? text[..42] : text, (int)rect.X + 6,
            (int)rect.Y + (compact ? 5 : 7), compact ? 14 : 15, Color.White);
        return hovered && MousePress(0);
    }

    private static void Add(MapObjectKind kind)
    {
        Vector3 where = eye + Forward() * 6f;
        where.Y = kind == MapObjectKind.Stairs ? 1.4f : kind == MapObjectKind.Ladder ? 2f :
            MathF.Max(.8f, where.Y);
        MapObject o = new()
        {
            Name = kind + " " + (document.Objects.Count + 1),
            Kind = kind, Position = where,
            Size = kind switch
            {
                MapObjectKind.Ramp => new Vector3(3f, 2f, 7f),
                MapObjectKind.Stairs => new Vector3(3f, 2.4f, 7f),
                MapObjectKind.Ladder => new Vector3(1.4f, 4f, .25f),
                MapObjectKind.Cylinder => new Vector3(2f, 3f, 2f),
                _ => new Vector3(2f, 1f, 2f)
            }
        };
        document.Objects.Add(o);
        SelectObject(o, false);
        objectScroll = Math.Max(0, document.Objects.Count - 8);
        Changed();
        Toast("Objeto adicionado ao mapa");
    }

    private static void Duplicate()
    {
        if (selected == null) return;
        MapObject o = new()
        {
            Name = selected.Name + " copia", Kind = selected.Kind,
            Position = selected.Position + new Vector3(1f, 0f, 1f),
            Size = selected.Size, Rotation = selected.Rotation, Solid = selected.Solid,
            Surface = selected.Surface, Visible = selected.Visible
        };
        document.Objects.Add(o);
        selected = o; Changed();
    }

    private static void Delete()
    {
        if (selected == null) return;
        document.Objects.Remove(selected);
        selected = null;
        Changed();
    }

    private static void ResizeAxis(MapObject o, int axis, float delta, bool all)
    {
        if (all)
        {
            o.Size = Vector3.Max(new Vector3(.05f), o.Size + new Vector3(delta));
        }
        else
        {
            Vector3 v = o.Size;
            if (axis == 0) v.X = MathF.Max(.05f, v.X + delta);
            if (axis == 1) v.Y = MathF.Max(.05f, v.Y + delta);
            if (axis == 2) v.Z = MathF.Max(.05f, v.Z + delta);
            o.Size = v;
        }
    }

    private static void ChangeFace(MapObject o, int axis, int side, float increment)
    {
        Vector3 size = o.Size;
        float original = axis == 0 ? size.X : axis == 1 ? size.Y : size.Z;
        float changed = MathF.Max(.05f, original + increment);
        float actual = changed - original;
        Vector3 direction = Vector3.Transform(Axis(axis), SceneTransform.Orientation(o.Rotation));
        o.Position += direction * (side * actual * .5f);
        if (axis == 0) size.X = changed;
        if (axis == 1) size.Y = changed;
        if (axis == 2) size.Z = changed;
        o.Size = size;
        Changed();
    }

    private static void Changed()
    {
        modified = true;
        preview = document.ToScene();
    }

    private static void Save()
    {
        try
        {
            SceneMapIO.Save(document, mapPath);
            modified = false;
            status = "Mapa salvo. O jogo carrega automaticamente.";
            Toast("Mapa salvo! O jogo foi atualizado.");
        }
        catch (Exception ex) { status = "Falha ao salvar: " + ex.Message; Toast(status, true); }
    }

    private static void Reload()
    {
        try
        {
            document = SceneMapIO.LoadOrDefault(mapPath);
            preview = document.ToScene();
            ClearSelection(); modified = false;
            status = "Mapa recarregado.";
            Toast("Mapa reaberto com sucesso");
        }
        catch (Exception ex) { status = "Falha ao abrir: " + ex.Message; Toast(status, true); }
    }
}
