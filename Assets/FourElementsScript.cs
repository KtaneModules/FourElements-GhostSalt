using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;

public class FourElementsScript : MonoBehaviour
{
    static int _moduleIdCounter = 1;
    int _moduleID = 0;

    public KMBombModule Module;
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMSelectable[] Tiles;
    public KMSelectable Reset;
    public KMSelectable Undo;
    public Sprite[] Symbols;
    public Sprite[] ResetSprites;
    public Sprite[] UndoSprites;
    public SpriteRenderer TooBad;
    public MeshRenderer StatusLight;

    private static readonly List<Tetromino> Tetrominos = new List<Tetromino>()
    {
        new Tetromino(new Tetromino.TetraCol(0, 3), new Tetromino.TetraCol(0, 1)), // L
        new Tetromino(new Tetromino.TetraCol(0, 2), new Tetromino.TetraCol(1, 1), new Tetromino.TetraCol(1, 1)),
        new Tetromino(new Tetromino.TetraCol(2, 1), new Tetromino.TetraCol(0, 3)),
        new Tetromino(new Tetromino.TetraCol(0, 1), new Tetromino.TetraCol(0, 1), new Tetromino.TetraCol(0, 2)),

        new Tetromino(new Tetromino.TetraCol(0, 1), new Tetromino.TetraCol(0, 3)), // J
        new Tetromino(new Tetromino.TetraCol(0, 2), new Tetromino.TetraCol(0, 1), new Tetromino.TetraCol(0, 1)),
        new Tetromino(new Tetromino.TetraCol(0, 3), new Tetromino.TetraCol(2, 1)),
        new Tetromino(new Tetromino.TetraCol(1, 1), new Tetromino.TetraCol(1, 1), new Tetromino.TetraCol(0, 2)),

        new Tetromino(new Tetromino.TetraCol(0, 4)), // I
        new Tetromino(new Tetromino.TetraCol(0, 1), new Tetromino.TetraCol(0, 1), new Tetromino.TetraCol(0, 1), new Tetromino.TetraCol(0, 1)),

        new Tetromino(new Tetromino.TetraCol(1, 1), new Tetromino.TetraCol(0, 2), new Tetromino.TetraCol(1, 1)), // T
        new Tetromino(new Tetromino.TetraCol(1, 1), new Tetromino.TetraCol(0, 3)),
        new Tetromino(new Tetromino.TetraCol(0, 1), new Tetromino.TetraCol(0, 2), new Tetromino.TetraCol(0, 1)),
        new Tetromino(new Tetromino.TetraCol(0, 3), new Tetromino.TetraCol(1, 1)),

        new Tetromino(new Tetromino.TetraCol(0, 1), new Tetromino.TetraCol(0, 2), new Tetromino.TetraCol(1, 1)), // S
        new Tetromino(new Tetromino.TetraCol(1, 2), new Tetromino.TetraCol(0, 2)),

        new Tetromino(new Tetromino.TetraCol(1, 1), new Tetromino.TetraCol(0, 2), new Tetromino.TetraCol(0, 1)), // Z
        new Tetromino(new Tetromino.TetraCol(0, 2), new Tetromino.TetraCol(1, 2)),

        new Tetromino(new Tetromino.TetraCol(0, 2), new Tetromino.TetraCol(0, 2)) // O
    };
    private List<SpriteRenderer> TileHighlights = new List<SpriteRenderer>();
    private List<Option> Solution = new List<Option>();
    private int[][] Grid = new int[][] { new int[8], new int[8], new int[8], new int[8], new int[8], new int[8], new int[8], new int[8] };
    private List<int[][]> Steps = new List<int[][]>();
    private bool[][] Selected = new bool[][] { new bool[8], new bool[8], new bool[8], new bool[8], new bool[8], new bool[8], new bool[8], new bool[8] };
    private bool CannotPress, Failure;

    private struct FallInfo
    {
        public int Index;
        public bool Lands;

        public FallInfo(int index, bool stopsFalling)
        {
            Index = index;
            Lands = stopsFalling;
        }
    }

    private class Option
    {
        public Tetromino Tetromino;
        public int X;
        public int Y;

        public Option(Tetromino tetromino, int x, int y)
        {
            Tetromino = tetromino;
            X = x;
            Y = y;
        }

        public override string ToString()
        {
            List<string> entries = new List<string>();
            for (int i = 0; i < Tetromino.GetWidth(); i++)
                for (int j = 0; j < Tetromino.Columns[i].Num; j++)
                    entries.Add("ABCDEFGH"[i + X].ToString() + (8 - (Tetromino.Columns[i].YOffset + Y + j)).ToString());
            return entries.Join(", ");
        }

        public List<int> Positions()
        {
            List<int> entries = new List<int>();
            for (int i = 0; i < Tetromino.GetWidth(); i++)
                for (int j = 0; j < Tetromino.Columns[i].Num; j++)
                    entries.Add((7 - (Tetromino.Columns[i].YOffset + Y + j)) * 8 + i + X);
            return entries;
        }
    }

    private class Tetromino
    {
        public struct TetraCol
        {
            public int YOffset;
            public int Num;

            public TetraCol(int yOffset, int num)
            {
                YOffset = yOffset;
                Num = num;
            }
        }

        public List<TetraCol> Columns;

        public Tetromino(params TetraCol[] columns)
        {
            Columns = columns.ToList();
        }

        public int GetWidth()
        {
            return Columns.Count;
        }

        public int GetHeight()
        {
            return Columns.Max(x => x.YOffset + x.Num);
        }
    }

    private bool IsTileFloating(int ix)
    {
        if (ix / 8 < 7 && Grid[ix / 8][ix % 8] > -1 && Grid.Where((x, xix) => xix > ix / 8).Where(x => x[ix % 8] == -1).Count() > 0)
            return true;
        return false;
    }

    private bool IsTileUnsupported(int ix)
    {
        if (ix / 8 < 7 && Grid[(ix / 8) + 1][ix % 8] == -1)
            return true;
        return false;
    }

    private bool WillTileLand(int ix) //Stacks of tiles playing the thud sound multiple times is intentional, as higher stacks weigh more and will make louder thuds
    {
        if (ix / 8 < 6 && Grid[ix / 8][ix % 8] > -1 && Grid.Where((x, xix) => xix > (ix / 8) + 1).Where(x => x[ix % 8] == -1).Count() > 0)
            return false;
        return true;
    }

    private bool AreThereFloatingTiles()
    {
        for (int i = 0; i < 56; i++)
            if (IsTileFloating(i))
                return true;
        return false;
    }

    private bool IsTetrominoFloating(Option opt)
    {
        for (int i = 0; i < opt.Tetromino.GetWidth(); i++)
            if (IsTileUnsupported((7 - (opt.Tetromino.Columns[i].YOffset + opt.Y)) * 8 + opt.X + i))
                return true;
        return false;
    }

    private bool IsTetrominoValid(Option opt)
    {
        if (IsTetrominoFloating(opt))
            return false;
        int[] colVacancies = Enumerable.Range(0, 8).Select(x => VacanciesInColumn(x)).ToArray();
        for (int i = 0; i < opt.Tetromino.GetWidth(); i++)
            colVacancies[i + opt.X] -= opt.Tetromino.Columns[i].Num;
        if (colVacancies.Any(x => x < 0))
            return false;
        int counter = 0;
        for (int i = 0; i < 8; i++)
        {
            if (colVacancies[i] > 0)
                counter += colVacancies[i];
            else if (counter % 4 != 0)
                return false;
            else
                counter = 0;
        }
        return true;
    }

    private int VacanciesInColumn(int pos)
    {
        return Grid.Count(x => x[pos] == -1);
    }

    private bool AdjacencyCheck(int pos)
    {
        if ((pos / 8 > 0 && Selected[(pos / 8) - 1][pos % 8]) || (pos % 8 < 7 && Selected[pos / 8][(pos % 8) + 1]) || (pos / 8 < 7 && Selected[(pos / 8) + 1][pos % 8]) || (pos % 8 > 0 && Selected[pos / 8][(pos % 8) - 1]))
            return true;
        return false;
    }

    private bool SquareArrangement()
    {
        int colCount = 0;
        for (int i = 0; i < 8; i++)
            if (Selected.Where(x => x[i]).Count() > 0)
                colCount++;
        if (Selected.Select(x => x.Where(y => y).Count()).Sum() == 4 && Selected.Where(x => x.Where(y => y).Count() > 0).Count() == 2 && colCount == 2)
            return true;
        return false;
    }

    private int AdjacencyCount(int pos)
    {
        var ans = 0;
        if (pos / 8 > 0 && Selected[(pos / 8) - 1][pos % 8])
            ans++;
        if (pos % 8 < 7 && Selected[pos / 8][(pos % 8) + 1])
            ans++;
        if (pos / 8 < 7 && Selected[(pos / 8) + 1][pos % 8])
            ans++;
        if (pos % 8 > 0 && Selected[pos / 8][(pos % 8) - 1])
            ans++;
        return ans;
    }

    private Option GenOption()
    {
        List<Option> optionList = new List<Option>();
        foreach (Tetromino tet in Tetrominos)
            for (int i = 0; i <= 8 - tet.GetWidth(); i++)
                for (int j = 0; j <= 8 - tet.GetHeight(); j++)
                {
                    Option opt = new Option(tet, i, j);
                    if (IsTetrominoValid(opt))
                        optionList.Add(opt);
                }
        return optionList.PickRandom();
    }

    private bool CanSelectTile(int pos)
    {
        return ((Selected.Where((x, xix) => x.Where((y, yix) => y && Grid[xix][yix] == Grid[pos / 8][pos % 8]).Count() > 0).Count() == 0 && (AdjacencyCheck(pos) || Selected.Select(x => x.Where(y => y).Count()).Where(x => x > 0).Count() == 0))
            || Selected[pos / 8][pos % 8] && (AdjacencyCount(pos) < 2 || SquareArrangement()));
    }

    private bool OptionHasFourElements(Option opt)
    {
        List<int> elements = new List<int>();
        for (int i = 0; i < opt.Tetromino.GetWidth(); i++)
            for (int j = 0; j < opt.Tetromino.Columns[i].Num; j++)
                elements.Add(Grid[7 - (opt.Tetromino.Columns[i].YOffset + opt.Y + j)][opt.X + i]);
        elements.Sort();
        for (int i = 0; i < 4; i++)
            if (elements[i] != i)
                return false;
        return true;
    }

    private bool CanSelectFourTiles()
    {
        foreach (Tetromino tet in Tetrominos)
            for (int i = 0; i <= 8 - tet.GetWidth(); i++)
                for (int j = 0; j <= 8 - tet.GetHeight(); j++)
                    if (OptionHasFourElements(new Option(tet, i, j)))
                        return true;
        return false;
    }

    void Awake()
    {
        _moduleID = _moduleIdCounter++;
        TooBad.color = Color.clear;
        for (int i = 0; i < Tiles.Length; i++)
        {
            int x = i;
            Tiles[x].OnInteract += delegate { TilePress(x); return false; };
            TileHighlights.Add(Tiles[x].GetComponentsInChildren<SpriteRenderer>().Where(y => y.name == "Highlight").First());
            TileHighlights[x].color = Color.clear;
            Tiles[x].OnHighlight += delegate { TileHighlights[x].color = new Color32(255, 69, 0, 255); };
            Tiles[x].OnHighlightEnded += delegate { TileHighlights[x].color = Color.clear; };
        }
        Reset.OnInteract += delegate
        {
            if (!CannotPress && Steps.Count() > 1)
            {
                TooBad.color = Color.clear;
                Failure = false;
                for (int i = 0; i < 8; i++)
                    for (int j = 0; j < 8; j++)
                        Grid[i][j] = Steps[0][i][j];
                Reset.AddInteractionPunch();
                Audio.PlaySoundAtTransform("reset", Reset.transform);
                Selected = new bool[][] { new bool[8], new bool[8], new bool[8], new bool[8], new bool[8], new bool[8], new bool[8], new bool[8] };
                Steps = new List<int[][]>();
                var temp2 = new int[][] { new int[8], new int[8], new int[8], new int[8], new int[8], new int[8], new int[8], new int[8] };
                for (int i = 0; i < 8; i++)
                    for (int j = 0; j < 8; j++)
                        temp2[i][j] = Grid[i][j];
                Steps.Add(temp2);
                OrganiseTilePositions();
            }
            return false;
        };
        Reset.OnHighlight += delegate { Reset.GetComponent<SpriteRenderer>().sprite = ResetSprites[1]; };
        Reset.OnHighlightEnded += delegate { Reset.GetComponent<SpriteRenderer>().sprite = ResetSprites[0]; };
        Undo.OnInteract += delegate
        {
            if (!CannotPress && Steps.Count() > 1)
            {
                TooBad.color = Color.clear;
                Failure = false;
                Steps.RemoveAt(Steps.Count() - 1);
                for (int i = 0; i < 8; i++)
                    for (int j = 0; j < 8; j++)
                        Grid[i][j] = Steps.Last()[i][j];
                Undo.AddInteractionPunch();
                Audio.PlaySoundAtTransform("undo", Undo.transform);
                Selected = new bool[][] { new bool[8], new bool[8], new bool[8], new bool[8], new bool[8], new bool[8], new bool[8], new bool[8] };
                OrganiseTilePositions();
            }
            return false;
        };
        Undo.OnHighlight += delegate { Undo.GetComponent<SpriteRenderer>().sprite = UndoSprites[1]; };
        Undo.OnHighlightEnded += delegate { Undo.GetComponent<SpriteRenderer>().sprite = UndoSprites[0]; };
        Calculate();
    }

    // Use this for initialization
    void Start()
    {

    }

    void Calculate()
    {
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
                Grid[i][j] = -1;
        while (Grid.Any(x => x.Any(y => y == -1)))
        {
            Option opt = GenOption();
            InsertTetromino(opt);
            Solution.Add(opt);
        }
        Solution.Reverse();
        Debug.LogFormat("[Four Elements #{0}] The grid is as follows:\n{1}", _moduleID, Grid.Select(x => x.Select(y => new[] { " ", "F", "W", "E", "R" }[y + 1]).Join(" ")).Join("\n"));
        Debug.LogFormat("[Four Elements #{0}] Selecting the following sets of tiles will solve the module: {1}.", _moduleID, Solution.Select(x => x.ToString()).Join(" | "));
        var temp = new int[][] { new int[8], new int[8], new int[8], new int[8], new int[8], new int[8], new int[8], new int[8] };
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
                temp[i][j] = Grid[i][j];
        Steps.Add(temp);
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
            {
                Tiles[i * 8 + j].GetComponent<SpriteRenderer>().color = new[] { new Color32(203, 79, 79, 255), new Color32(79, 79, 203, 255), new Color32(79, 203, 79, 255), new Color32(203, 203, 79, 255) }[Grid[i][j]];
                Tiles[i * 8 + j].GetComponentsInChildren<SpriteRenderer>().Where(y => y.name == "Symbol").First().sprite = Symbols[Grid[i][j]];
                Tiles[i * 8 + j].GetComponentsInChildren<SpriteRenderer>().Where(y => y.name == "Symbol").First().color = Color.black;
            }
    }

    void InsertTetromino(Option opt)
    {
        Queue<int> elements = new Queue<int>(Enumerable.Range(0, 4).ToList().Shuffle());
        for (int i = 0; i < opt.Tetromino.GetWidth(); i++)
            for (int j = 0; j < opt.Tetromino.Columns[i].Num; j++)
            {
                MoveCellUp((7 - (opt.Y + opt.Tetromino.Columns[i].YOffset + j)) * 8 + opt.X + i);
                Grid[7 - (opt.Y + opt.Tetromino.Columns[i].YOffset + j)][opt.X + i] = elements.Dequeue();
            }
    }

    void MoveCellUp(int ix)
    {
        if (Grid[ix / 8][ix % 8] == -1)
            return;
        if (Grid[(ix / 8) - 1][ix % 8] > -1)
            MoveCellUp(ix - 8);
        Grid[(ix / 8) - 1][ix % 8] = Grid[ix / 8][ix % 8];
        Grid[ix / 8][ix % 8] = -1;
    }

    void TilePress(int pos)
    {
        if (CanSelectTile(pos) && !CannotPress && !Failure)
        {
            Selected[pos / 8][pos % 8] = !Selected[pos / 8][pos % 8];
            Tiles[pos].GetComponent<SpriteRenderer>().color = new[] { new Color32(203, 79, 79, 255), new Color32(79, 79, 203, 255), new Color32(79, 203, 79, 255), new Color32(203, 203, 79, 255), new Color32(246, 33, 33, 255), new Color32(33, 33, 246, 255), new Color32(33, 246, 33, 255), new Color32(246, 246, 33, 255) }[Grid[pos / 8][pos % 8] + (!Selected[pos / 8][pos % 8] ? 0 : 4)];
            var value = 1 - Tiles[pos].GetComponentsInChildren<SpriteRenderer>().Where(y => y.name == "Symbol").First().color.r;
            Tiles[pos].GetComponentsInChildren<SpriteRenderer>().Where(y => y.name == "Symbol").First().color = new Color(value, value, value, 1);
            Audio.PlaySoundAtTransform("select", Tiles[pos].transform);
            Tiles[pos].AddInteractionPunch(0.5f);
            if (Selected.Select(x => x.Where(y => y).Count()).Sum() == 4)
                StartCoroutine(ClearFour());
        }
    }

    void OrganiseTilePositions()
    {
        for (int i = 0; i < 64; i++)
        {
            Tiles[i].transform.localPosition = new Vector3(i % 8, 0, -i / 8);
            if (Grid[i / 8][i % 8] == -1)
                Tiles[i].transform.localScale = Vector3.zero;
            else
            {
                Tiles[i].transform.localScale = Vector3.one;
                Tiles[i].GetComponent<SpriteRenderer>().color = new[] { new Color32(203, 79, 79, 255), new Color32(79, 79, 203, 255), new Color32(79, 203, 79, 255), new Color32(203, 203, 79, 255) }[Grid[i / 8][i % 8]];
                Tiles[i].GetComponentsInChildren<SpriteRenderer>().Where(y => y.name == "Symbol").First().sprite = Symbols[Grid[i / 8][i % 8]];
                Tiles[i].GetComponentsInChildren<SpriteRenderer>().Where(y => y.name == "Symbol").First().color = Color.black;
            }
        }
    }

    private IEnumerator ClearFour()
    {
        CannotPress = true;
        float timer = 0;
        while (timer < 0.125f)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        Audio.PlaySoundAtTransform("clear", Module.transform);
        foreach (var ix in Enumerable.Range(0, 64).Where(x => Selected[x / 8][x % 8]))
        {
            Tiles[ix].transform.localScale = Vector3.zero;
            Grid[ix / 8][ix % 8] = -1;
        }
        StartCoroutine(ApplyGravity());
    }

    private IEnumerator ApplyGravity(float fallStep = 0.05f)
    {
        var tileFallOrder = new List<List<FallInfo>>();
        var gridStart = new int[][] { new int[8], new int[8], new int[8], new int[8], new int[8], new int[8], new int[8], new int[8] };
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
                gridStart[i][j] = Grid[i][j];
        do
        {
            var currentOrder = new List<FallInfo>();
            for (int i = 55; i > -1; i--)
                if (IsTileFloating(i))
                {
                    currentOrder.Add(new FallInfo(i, WillTileLand(i)));
                    Grid[(i / 8) + 1][i % 8] = Grid[i / 8][i % 8];
                    Grid[i / 8][i % 8] = -1;
                }
            tileFallOrder.Add(currentOrder);
        }
        while (AreThereFloatingTiles());
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
                Grid[i][j] = gridStart[i][j];
        foreach (var step in tileFallOrder)
        {
            var initPositions = new List<Vector3>();
            foreach (var info in step)
                initPositions.Add(Tiles[info.Index].transform.localPosition);
            float timer = 0;
            while (timer < fallStep)
            {
                yield return null;
                timer += Time.deltaTime;
                int i = 0;
                foreach (var info in step)
                {
                    Tiles[info.Index].transform.localPosition = Vector3.Lerp(initPositions[i], initPositions[i] - Vector3.forward, timer / fallStep);
                    i++;
                }
            }
            int j = 0;
            foreach (var info in step)
            {
                Tiles[info.Index].transform.localPosition = initPositions[j] - Vector3.forward;
                if (info.Lands)
                    Audio.PlaySoundAtTransform("drop", Tiles[info.Index].transform);
                j++;
            }
            for (int i = 55; i > -1; i--)
                if (IsTileFloating(i))
                {
                    Grid[(i / 8) + 1][i % 8] = Grid[i / 8][i % 8];
                    Grid[i / 8][i % 8] = -1;
                }
            OrganiseTilePositions();
        }
        Selected = new bool[][] { new bool[8], new bool[8], new bool[8], new bool[8], new bool[8], new bool[8], new bool[8], new bool[8] };
        var temp = new int[][] { new int[8], new int[8], new int[8], new int[8], new int[8], new int[8], new int[8], new int[8] };
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
                temp[i][j] = Grid[i][j];
        Steps.Add(temp);
        if (Grid.Where(x => x.Where(y => y == -1).Count() == 8).Count() == 8)
        {
            float timer = 0;
            while (timer < 0.25f)
            {
                yield return null;
                timer += Time.deltaTime;
            }
            Module.HandlePass();
            StartCoroutine(IlluminateStatus());
            Audio.PlaySoundAtTransform("solve", Module.transform);
            Debug.LogFormat("[Four Elements #{0}] Module solved!", _moduleID);
            yield return "solve";
        }
        else
        {
            if (!CanSelectFourTiles())
            {
                float timer = 0;
                while (timer < 0.25f)
                {
                    yield return null;
                    timer += Time.deltaTime;
                }
                TooBad.color = Color.white;
                Audio.PlaySoundAtTransform("failure", Module.transform);
                Failure = true;
            }
            CannotPress = false;
        }
        yield return null;
    }

    private IEnumerator IlluminateStatus(float duration = 0.5f)
    {
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            StatusLight.material.color = new Color(0, 1, 1, StatusLight.material.color.a) * Color.Lerp(Color.black, Color.white, timer / duration);
        }
        StatusLight.material.color = new Color(0, 1, 1, StatusLight.material.color.a);
    }

#pragma warning disable 414
    private string TwitchHelpMessage = "Use '!{0} a1 a2 b2 u r' to select cells A1, A2 and B2, then press the undo button, then press the reset button.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant();
        var commandArray = command.Split(' ');
        var validCommands = new[] { "a1", "b1", "c1", "d1", "e1", "f1", "g1", "h1", "a2", "b2", "c2", "d2", "e2", "f2", "g2", "h2", "a3", "b3", "c3", "d3", "e3", "f3", "g3", "h3", "a4", "b4", "c4", "d4", "e4", "f4", "g4", "h4", "a5", "b5", "c5", "d5", "e5", "f5", "g5", "h5", "a6", "b6", "c6", "d6", "e6", "f6", "g6", "h6", "a7", "b7", "c7", "d7", "e7", "f7", "g7", "h7", "a8", "b8", "c8", "d8", "e8", "f8", "g8", "h8", "r", "u" };
        var presses = new List<int>();
        foreach (var part in commandArray)
        {
            if (validCommands.Contains(part))
                presses.Add(Array.IndexOf(validCommands, part));
            else
            {
                yield return "sendtochaterror Invalid command.";
                yield break;
            }
        }
        yield return null;
        foreach (var but in presses)
        {
            if (but == 64)
                Reset.OnInteract();
            else if (but == 65)
                Undo.OnInteract();
            else
                Tiles[but].OnInteract();
            float timer = 0;
            while (timer < 0.1f)
            {
                yield return null;
                timer += Time.deltaTime;
            }
            while (CannotPress)
                yield return null;
        }
    }
    IEnumerator TwitchHandleForcedSolve()
    {
        if (Steps.Count() > 1)
        {
            Reset.OnInteract();
            yield return true;
        }
        foreach (Option opt in Solution)
        {
            while (CannotPress)
                yield return true;
            var presses = new List<int>();
            var buts = opt.Positions();
            Debug.Log(opt.ToString());
            while (presses.Count() < 4)
            {
                bool infiniteLoop = true;
                for (int i = 0; i < 4; i++)
                    if (CanSelectTile(buts[i]) && !presses.Contains(buts[i]))
                    {
                        Selected[buts[i] / 8][buts[i] % 8] = true;
                        presses.Add(buts[i]);
                        infiniteLoop = false;
                    }
                if (infiniteLoop)
                {
                    yield return "sendtochaterror Sorry, the module got caught in an infinite loop.";
                    CannotPress = true;
                    Module.HandlePass();
                    StartCoroutine(IlluminateStatus());
                    Audio.PlaySoundAtTransform("solve", Module.transform);
                    Debug.LogFormat("[Four Elements #{0}] Module solved!", _moduleID);
                    yield return "solve";
                    yield break;
                }
            }
            Selected = new bool[][] { new bool[8], new bool[8], new bool[8], new bool[8], new bool[8], new bool[8], new bool[8], new bool[8] };
            foreach (int ix in presses)
                Tiles[ix].OnInteract();
        }
    }
}
