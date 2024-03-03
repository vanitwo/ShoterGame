using System.Text;

class Program
{
    private const int ScreenWidth = 180;
    private const int ScreenHeight = 120;

    private const int MapWidth = 32;
    private const int MapHeight = 32;

    private const double Fov = Math.PI / 3;
    private const double Depth = 16;

    private static double _playerX = 5;
    private static double _playerY = 5;
    private static double _playerA = 0;

    private static readonly StringBuilder Map = new StringBuilder();

    private static readonly char[] Screen = new char[ScreenWidth * ScreenHeight];

    static async Task Main(string[] args)
    {
        Console.SetWindowSize(ScreenWidth, ScreenHeight);
        Console.SetBufferSize(ScreenWidth, ScreenHeight);
        Console.CursorVisible = false;

        InitMap();

        var dateTimeFrom = DateTime.Now;

        while (true)
        {
            var dateTimeTo = DateTime.Now;
            var elapsedTime = (dateTimeTo - dateTimeFrom).TotalSeconds;
            dateTimeFrom = DateTime.Now;

            if (Console.KeyAvailable)
            {
                var consoleKey = Console.ReadKey(true).Key;
                GetDirection(elapsedTime, consoleKey);
                InitMap();
            }

            //Ray Casting
            await DoRays();

            GetStatsAndMap(elapsedTime);

            Console.SetCursorPosition(0, 0);
            Console.Write(Screen);
        }
    }

    private static async Task DoRays()
    {
        var rayCastinTask = new List<Task<Dictionary<int, char>>>();
        for (int x = 0; x < ScreenWidth; x++)
        {
            int x1 = x;
            rayCastinTask.Add(Task.Run(() => CasrRay(x1)));
        }
        var rays = await Task.WhenAll(rayCastinTask);

        foreach (var dictionary in rays)
        {
            foreach (int key in dictionary.Keys)
            {
                Screen[key] = dictionary[key];
            }
        }
    }

    private static void GetDirection(double elapsedTime, ConsoleKey consoleKey)
    {
        switch (consoleKey)
        {
            case ConsoleKey.A:
                _playerA += 10 * elapsedTime;
                break;
            case ConsoleKey.D:
                _playerA -= 10 * elapsedTime;
                break;
            case ConsoleKey.W:
                {
                    MoveForward(elapsedTime);
                }
                break;
            case ConsoleKey.S:
                {
                    MoveBackward(elapsedTime);
                }
                break;
        }
    }

    private static void MoveBackward(double elapsedTime)
    {
        _playerX -= Math.Sin(_playerA) * 25 * elapsedTime;
        _playerY -= Math.Cos(_playerA) * 25 * elapsedTime;

        if (Map[(int)_playerY * MapWidth + (int)_playerX] == '#')
        {
            _playerX += Math.Sin(_playerA) * 25 * elapsedTime;
            _playerY += Math.Cos(_playerA) * 25 * elapsedTime;
        }
    }

    private static void MoveForward(double elapsedTime)
    {
        _playerX += Math.Sin(_playerA) * 25 * elapsedTime;
        _playerY += Math.Cos(_playerA) * 25 * elapsedTime;

        if (Map[(int)_playerY * MapWidth + (int)_playerX] == '#')
        {
            _playerX -= Math.Sin(_playerA) * 25 * elapsedTime;
            _playerY -= Math.Cos(_playerA) * 25 * elapsedTime;
        }
    }

    private static void GetStatsAndMap(double elapsedTime)
    {
        //stats
        char[] stats = $"X: {_playerX}, Y: {_playerY}, A: {_playerA}, FPS: {(int)(1 / elapsedTime)}".ToCharArray();
        stats.CopyTo(Screen, 0);
        //map
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                Screen[(y + 1) * ScreenWidth + x] = Map[y * MapWidth + x];
            }
        }
        //player
        Screen[(int)(_playerY + 1) * ScreenWidth + (int)_playerX] = 'P';
    }

    public static Dictionary<int, char> CasrRay(int x)
    {
        var result = new Dictionary<int, char>();

        double rayAngle = _playerA + Fov / 2 - x * Fov / ScreenWidth;
        double rayX = Math.Sin(rayAngle);
        double rayY = Math.Cos(rayAngle);

        double distanceToWall = 0;

        bool hitWall = false;
        bool isBound = false;
        GetDistance(rayX, rayY, ref distanceToWall, ref hitWall, ref isBound);

        int ceiling = (int)(ScreenHeight / 2d - ScreenHeight * Fov / distanceToWall);
        int floor = ScreenHeight - ceiling;

        for (int y = 0; y < ScreenHeight; y++)
        {
            if (y <= ceiling)
                result[y * ScreenWidth + x] = ' ';
            else if (y > ceiling && y <= floor)
                result[y * ScreenWidth + x] = GetWallShade(distanceToWall, isBound);
            else
                result[y * ScreenWidth + x] = GetFloorShade(y);
        }
        return result;
    }

    private static void GetDistance(double rayX, double rayY, ref double distanceToWall, ref bool hitWall, ref bool isBound)
    {
        while (!hitWall && distanceToWall < Depth)
        {
            distanceToWall += 0.1;
            int testX = (int)(_playerX + rayX * distanceToWall);
            int testY = (int)(_playerY + rayY * distanceToWall);

            if (testX < 0 || testX >= Depth + _playerX || testY < 0 || testY >= Depth + _playerY)
            {
                hitWall = true;
                distanceToWall = Depth;
            }
            else
            {
                char testCell = Map[testY * MapWidth + testX];
                if (testCell == '#')
                {
                    hitWall = true;
                    isBound = GetIsBound(rayX, rayY, distanceToWall, isBound, testX, testY);
                }
                else
                    Map[testY * MapWidth + testX] = '*';
            }
        }
    }

    private static bool GetIsBound(double rayX, double rayY, double distanceToWall, bool isBound, int testX, int testY)
    {
        var boundsVectorList = new List<(double module, double cos)>();
        for (int tx = 0; tx < 2; tx++)
        {
            for (int ty = 0; ty < 2; ty++)
            {
                double vx = testX + tx - _playerX;
                double vy = testY + ty - _playerY;
                double vectorModule = Math.Sqrt(vx * vx + vy * vy);
                double cosAngle = rayX * vx / vectorModule + rayY * vy / vectorModule;

                boundsVectorList.Add((vectorModule, cosAngle));
            }
        }
        boundsVectorList = boundsVectorList.OrderBy(v => v.module).ToList();

        double boundAngle = 0.03 / distanceToWall;

        if (Math.Acos(boundsVectorList[0].cos) < boundAngle ||
            Math.Acos(boundsVectorList[1].cos) < boundAngle)
            isBound = true;
        return isBound;
    }

    private static char GetFloorShade(int y)
    {
        char floorShade;
        double b = 1 - (y - ScreenHeight / 2d) / (ScreenHeight / 2d);

        if (b < 0.25)
            floorShade = '#';
        else if (b < 0.5)
            floorShade = 'x';
        else if (b < 0.75)
            floorShade = '-';
        else if (b < 0.9)
            floorShade = '.';
        else
            floorShade = ' ';
        return floorShade;
    }

    private static char GetWallShade(double distanceToWall, bool isBound)
    {
        char wallShade;
        if (isBound)
            wallShade = '|';
        else if (distanceToWall < Depth / 4d)
            wallShade = '\u2588';
        else if (distanceToWall < Depth / 3d)
            wallShade = '\u2593';
        else if (distanceToWall < Depth / 2d)
            wallShade = '\u2592';
        else if (distanceToWall < Depth)
            wallShade = '\u2591';
        else
            wallShade = ' ';
        return wallShade;
    }

    private static void InitMap()
    {
        Map.Clear();
        Map.Append("################################");
        Map.Append("#.....................#........#");
        Map.Append("#.....................#........#");
        Map.Append("#.....................#........#");
        Map.Append("#.....................#........#");
        Map.Append("#.....................#........#");
        Map.Append("#.....................#........#");
        Map.Append("#.....................#........#");
        Map.Append("#......######.........#........#");
        Map.Append("#..............................#");
        Map.Append("#..............................#");
        Map.Append("#..............................#");
        Map.Append("#.......................########");
        Map.Append("#.......#......................#");
        Map.Append("#..............................#");
        Map.Append("#..............................#");
        Map.Append("#..............................#");
        Map.Append("#..............................#");
        Map.Append("#..............................#");
        Map.Append("#..............................#");
        Map.Append("#..............................#");
        Map.Append("#..............................#");
        Map.Append("###########....................#");
        Map.Append("#..............................#");
        Map.Append("#.........#....................#");
        Map.Append("#.........#....................#");
        Map.Append("#.........#....................#");
        Map.Append("#.........#....................#");
        Map.Append("#.........#....................#");
        Map.Append("#.........#....................#");
        Map.Append("#.........#....................#");
        Map.Append("################################");
    }
}