using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

class Program
{
    static void Main()
    {
        string[] lines = File.ReadAllLines("inputDay12.txt");

        // Shapes: index -> char[,]
        Dictionary<int, char[,]> shapes = new Dictionary<int, char[,]>();
        // Regions: (width, height, counts[])
        List<(long width, long height, long[] counts)> regions = new List<(long, long, long[])>();

        int i = 0;
        while (i < lines.Length)
        {
            string line = lines[i].Trim();

            // Shape-Definition (z. B. "0:")
            if (line.EndsWith(":") && !line.Contains("x"))
            {
                if (!int.TryParse(line.Split(':')[0], out int index))
                {
                    i++;
                    continue;
                }

                List<string> shapeLines = new List<string>();
                i++;
                while (i < lines.Length)
                {
                    string l = lines[i].Trim();
                    if (l.Length == 0) break;
                    if (l.All(ch => ch == '#' || ch == '.'))
                    {
                        shapeLines.Add(l);
                        i++;
                    }
                    else break;
                }

                if (shapeLines.Count > 0)
                {
                    int rows = shapeLines.Count;
                    int cols = shapeLines[0].Length;
                    char[,] shape = new char[rows, cols];
                    for (int y = 0; y < rows; y++)
                        for (int x = 0; x < cols; x++)
                            shape[y, x] = shapeLines[y][x];
                    shapes[index] = shape;
                }
            }
            // Region-Definition (z. B. "12x5: 1 0 1 0 3 2")
            else if (line.Contains("x") && line.Contains(":"))
            {
                string[] parts = line.Split(':');
                string[] size = parts[0].Split('x');
                if (size.Length == 2
                    && long.TryParse(size[0].Trim(), out long width)
                    && long.TryParse(size[1].Trim(), out long height))
                {
                    string countsPart = parts.Length > 1 ? parts[1].Trim() : "";
                    long[] counts = countsPart.Length == 0
                        ? new long[0]
                        : Array.ConvertAll(countsPart.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), long.Parse);

                    regions.Add((width, height, counts));
                }
                i++;
            }
            else
            {
                i++;
            }
        }

        int solvable = 0;

        foreach (var regionDef in regions)
        {
            // Array sizes in C# must be int
            int width = (int)regionDef.width;
            int height = (int)regionDef.height;

            // If region dimensions are invalid or too large for memory, skip (not counted as solvable)
            if (width <= 0 || height <= 0) continue;

            char[,] region = new char[height, width];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    region[y, x] = '.';

            // Build list of shapes to place according to counts
            List<char[,]> shapesToPlace = new List<char[,]>();
            List<int> shapeAreas = new List<int>();

            for (int s = 0; s < regionDef.counts.Length; s++)
            {
                long count = regionDef.counts[s];
                if (count <= 0) continue;

                if (!shapes.ContainsKey(s))
                {
                    // Missing shape definition -> cannot solve this region
                    shapesToPlace.Clear();
                    break;
                }

                char[,] baseShape = shapes[s];
                int area = CountShapeArea(baseShape);
                for (long c = 0; c < count; c++)
                {
                    shapesToPlace.Add(baseShape);
                    shapeAreas.Add(area);
                }
            }

            if (shapesToPlace.Count == 0 && regionDef.counts.Length > 0)
            {
                // Either no shapes required or missing shape definitions; if missing, treat as not solvable
                // If all counts were zero, shapesToPlace.Count == 0 and that's trivially solvable
                bool allZero = regionDef.counts.All(v => v == 0);
                if (!allZero) continue;
            }

            // Quick area check: if total required area > region area -> impossible
            long totalRequired = 0;
            foreach (int a in shapeAreas) totalRequired += a;
            long regionArea = (long)width * (long)height;
            if (totalRequired > regionArea) continue;

            // Sort shapes by area descending (place big shapes first) to speed up backtracking
            var indexed = shapesToPlace
                .Select((sh, idx) => new { Shape = sh, Area = shapeAreas[idx] })
                .OrderByDescending(x => x.Area)
                .ToList();

            List<char[,]> sortedShapes = indexed.Select(x => x.Shape).ToList();
            List<int> sortedAreas = indexed.Select(x => x.Area).ToList();

            // Solve with backtracking and simple pruning
            bool solved = SolveRegionWithPruning(region, sortedShapes, sortedAreas, 0);
            if (solved) solvable++;
        }

        // Final required output: only the number of solvable regions
        Console.WriteLine(solvable);
    }

    // Count number of '#' in a shape
    static int CountShapeArea(char[,] shape)
    {
        int area = 0;
        for (int y = 0; y < shape.GetLength(0); y++)
            for (int x = 0; x < shape.GetLength(1); x++)
                if (shape[y, x] == '#') area++;
        return area;
    }

    // Generate unique variants (rotations + flips) and avoid duplicates
    static List<char[,]> GenerateUniqueVariants(char[,] shape)
    {
        var set = new HashSet<string>();
        var variants = new List<char[,]>();

        char[,] current = shape;
        for (int r = 0; r < 4; r++)
        {
            AddIfNew(current, set, variants);
            AddIfNew(FlipHorizontal(current), set, variants);
            AddIfNew(FlipVertical(current), set, variants);
            current = Rotate90(current);
        }

        return variants;
    }

    static void AddIfNew(char[,] shape, HashSet<string> set, List<char[,]> variants)
    {
        string key = ShapeToKey(shape);
        if (!set.Contains(key))
        {
            set.Add(key);
            variants.Add(shape);
        }
    }

    static string ShapeToKey(char[,] shape)
    {
        int rows = shape.GetLength(0);
        int cols = shape.GetLength(1);
        char[] buf = new char[rows * (cols + 1)];
        int p = 0;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++) buf[p++] = shape[y, x];
            buf[p++] = '|';
        }
        return new string(buf);
    }

    static char[,] Rotate90(char[,] shape)
    {
        int rows = shape.GetLength(0);
        int cols = shape.GetLength(1);
        char[,] rotated = new char[cols, rows];
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
                rotated[x, rows - 1 - y] = shape[y, x];
        return rotated;
    }

    static char[,] FlipHorizontal(char[,] shape)
    {
        int rows = shape.GetLength(0);
        int cols = shape.GetLength(1);
        char[,] flipped = new char[rows, cols];
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
                flipped[y, cols - 1 - x] = shape[y, x];
        return flipped;
    }

    static char[,] FlipVertical(char[,] shape)
    {
        int rows = shape.GetLength(0);
        int cols = shape.GetLength(1);
        char[,] flipped = new char[rows, cols];
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
                flipped[rows - 1 - y, x] = shape[y, x];
        return flipped;
    }

    static bool CanPlace(char[,] region, char[,] shape, int startX, int startY)
    {
        int rRows = region.GetLength(0);
        int rCols = region.GetLength(1);
        int sRows = shape.GetLength(0);
        int sCols = shape.GetLength(1);

        for (int y = 0; y < sRows; y++)
        {
            for (int x = 0; x < sCols; x++)
            {
                if (shape[y, x] != '#') continue;
                int rx = startX + x;
                int ry = startY + y;
                if (rx < 0 || ry < 0 || rx >= rCols || ry >= rRows) return false;
                if (region[ry, rx] != '.') return false;
            }
        }
        return true;
    }

    static void Place(char[,] region, char[,] shape, int startX, int startY, char marker)
    {
        int sRows = shape.GetLength(0);
        int sCols = shape.GetLength(1);
        for (int y = 0; y < sRows; y++)
            for (int x = 0; x < sCols; x++)
                if (shape[y, x] == '#')
                    region[startY + y, startX + x] = marker;
    }

    static void Remove(char[,] region, char[,] shape, int startX, int startY)
    {
        int sRows = shape.GetLength(0);
        int sCols = shape.GetLength(1);
        for (int y = 0; y < sRows; y++)
            for (int x = 0; x < sCols; x++)
                if (shape[y, x] == '#')
                    region[startY + y, startX + x] = '.';
    }

    static int CountEmptyCells(char[,] region)
    {
        int cnt = 0;
        for (int y = 0; y < region.GetLength(0); y++)
            for (int x = 0; x < region.GetLength(1); x++)
                if (region[y, x] == '.') cnt++;
        return cnt;
    }

    // Backtracking with simple pruning: area check + place big shapes first + unique variants
    static bool SolveRegionWithPruning(char[,] region, List<char[,]> shapes, List<int> areas, int index)
    {
        if (index == shapes.Count) return true;

        // Prune by remaining empty cells vs required area
        int remainingEmpty = CountEmptyCells(region);
        int required = 0;
        for (int k = index; k < areas.Count; k++) required += areas[k];
        if (required > remainingEmpty) return false;

        char[,] shape = shapes[index];
        List<char[,]> variants = GenerateUniqueVariants(shape);

        int rows = region.GetLength(0);
        int cols = region.GetLength(1);

        // Try placements
        for (int v = 0; v < variants.Count; v++)
        {
            char[,] variant = variants[v];
            int vRows = variant.GetLength(0);
            int vCols = variant.GetLength(1);

            // iterate possible top-left positions
            for (int y = 0; y <= rows - vRows; y++)
            {
                for (int x = 0; x <= cols - vCols; x++)
                {
                    if (!CanPlace(region, variant, x, y)) continue;

                    Place(region, variant, x, y, (char)('A' + (index % 26)));

                    if (SolveRegionWithPruning(region, shapes, areas, index + 1))
                        return true;

                    Remove(region, variant, x, y);
                }
            }
        }

        return false;
    }
}