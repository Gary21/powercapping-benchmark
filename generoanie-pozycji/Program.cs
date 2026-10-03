using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public static class UhoPgnExtractor
{
    /// <summary>
    /// Wczytuje PGN UHO i zwraca losowe linie w formacie:
    ///
    /// e2e4 c7c5 g1f3 ...
    ///
    /// Każda linia:
    /// - zaczyna się od ruchu białych,
    /// - ma 16 półruchów,
    /// - kończy się ruchem czarnych,
    /// - pozycja wynikowa ma White to move.
    /// </summary>
    public static List<string> Extract(
        string pgnPath,
        int count,
        int seed = 12345)
    {
        if (!File.Exists(pgnPath))
            throw new FileNotFoundException(
                "Nie znaleziono pliku PGN.",
                pgnPath);

        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        string pgn = File.ReadAllText(pgnPath);

        var games = ParseGames(pgn);

        if (games.Count == 0)
            throw new InvalidOperationException(
                "Nie znaleziono partii/pozycji w PGN.");

        // UHO zawiera pozycje po 16 ply.
        // Dodatkowo filtrujemy wszystko, co nie ma parzystej liczby ruchów.
        var valid = games
            .Where(x => x.Count >= 16)
            .Select(x => x.Take(16).ToList())
            .Where(x => x.Count % 2 == 0)
            .Select(x => string.Join(" ", x))
            .Distinct()
            .ToList();

        if (valid.Count == 0)
            throw new InvalidOperationException(
                "Nie znaleziono poprawnych 16-ply pozycji.");

        var random = new Random(seed);

        return valid
            .OrderBy(_ => random.Next())
            .Take(Math.Min(count, valid.Count))
            .ToList();
    }

    /// <summary>
    /// Zapisuje wynik bezpośrednio do pliku TXT.
    /// </summary>
    public static void ExtractToFile(
        string pgnPath,
        string outputPath,
        int count,
        int seed = 12345)
    {
        var positions = Extract(
            pgnPath,
            count,
            seed);

        File.WriteAllLines(
            outputPath,
            positions);
    }

    /// <summary>
    /// Parsuje partie z PGN.
    /// </summary>
    private static List<List<string>> ParseGames(string pgn)
    {
        var result = new List<List<string>>();

        // Partie są rozdzielone pustymi liniami.
        // Szukamy bloków zaczynających się od [Event ...
        var blocks = Regex.Split(
                pgn,
                @"(?=\[Event\s)")
            .Where(x => !string.IsNullOrWhiteSpace(x));

        foreach (string block in blocks)
        {
            var moves = ExtractMoves(block);

            if (moves.Count > 0)
                result.Add(moves);
        }

        return result;
    }

    /// <summary>
    /// Wyciąga ruchy UCI z jednego wpisu PGN.
    ///
    /// Uwaga:
    /// UHO PGN może zawierać SAN, np.
    /// 1. e4 c5 2. Nf3 ...
    ///
    /// Dlatego ten parser nie może po prostu usunąć numerów ruchów.
    ///
    /// Do konwersji SAN -> UCI używamy własnej planszy.
    /// </summary>
    private static List<string> ExtractMoves(string block)
    {
        // Oddziel nagłówki [Tag "..."] od części ruchowej.
        string movesSection = Regex.Replace(
            block,
            @"(?m)^\[[^\]]*\]\s*$",
            "");

        movesSection = movesSection.Trim();

        if (string.IsNullOrWhiteSpace(movesSection))
            return new List<string>();

        // Usuń komentarze { ... }
        movesSection = Regex.Replace(
            movesSection,
            @"\{[^}]*\}",
            "");

        // Usuń warianty (...)
        movesSection = RemoveParentheses(movesSection);

        // Usuń numerację:
        // 1.
        // 1...
        // 12.
        movesSection = Regex.Replace(
            movesSection,
            @"\d+\.(\.\.)?",
            "");

        // Usuń rezultat.
        movesSection = Regex.Replace(
            movesSection,
            @"\s+(1-0|0-1|1/2-1/2|\*)\s*$",
            "");

        string[] tokens = movesSection
            .Split(
                new[] { ' ', '\r', '\n', '\t' },
                StringSplitOptions.RemoveEmptyEntries);

        // Jeśli PGN zawiera już UCI — obsłużymy go bezpośrednio.
        if (tokens.Length > 0 &&
            IsUciMove(tokens[0]))
        {
            return tokens
                .Where(IsUciMove)
                .ToList();
        }

        // Standardowy PGN jest w SAN.
        return SanToUci(tokens);
    }

    private static string RemoveParentheses(string text)
    {
        while (true)
        {
            string newText = Regex.Replace(
                text,
                @"\([^()]*\)",
                "");

            if (newText == text)
                return text;

            text = newText;
        }
    }

    private static bool IsUciMove(string move)
    {
        return Regex.IsMatch(
            move,
            @"^[a-h][1-8][a-h][1-8][qrbn]?$",
            RegexOptions.IgnoreCase);
    }

    // ============================================================
    // SAN -> UCI
    // ============================================================

    private static List<string> SanToUci(string[] sanMoves)
    {
        var board = new Board();

        var result = new List<string>();

        foreach (string raw in sanMoves)
        {
            string san = raw.Trim();

            if (string.IsNullOrWhiteSpace(san))
                continue;

            if (san == "1-0" ||
                san == "0-1" ||
                san == "1/2-1/2" ||
                san == "*")
            {
                break;
            }

            string uci = board.ConvertSanToUci(san);

            result.Add(uci);

            board.MakeMove(uci);
        }

        return result;
    }

    // ============================================================
    // Minimalna implementacja planszy do SAN -> UCI
    // ============================================================

    private sealed class Board
    {
        private readonly char[,] _board = new char[8, 8];

        private bool _whiteToMove = true;

        public Board()
        {
            Initialize();
        }

        private void Initialize()
        {
            for (int file = 0; file < 8; file++)
            {
                for (int rank = 0; rank < 8; rank++)
                    _board[file, rank] = '.';
            }

            const string white = "RNBQKBNR";
            const string black = "rnbqkbnr";

            for (int file = 0; file < 8; file++)
            {
                _board[file, 0] = white[file];
                _board[file, 1] = 'P';

                _board[file, 6] = 'p';
                _board[file, 7] = black[file];
            }
        }

        public string ConvertSanToUci(string san)
        {
            san = san.Trim();

            // Roszada
            if (san == "O-O" || san == "0-0")
            {
                string uci = _whiteToMove
                    ? "e1g1"
                    : "e8g8";

                return uci;
            }

            if (san == "O-O-O" || san == "0-0-0")
            {
                string uci = _whiteToMove
                    ? "e1c1"
                    : "e8c8";

                return uci;
            }

            // Usuń check / mate.
            san = san.TrimEnd('+', '#');

            // Promotion.
            char promotion = '\0';

            int equals = san.IndexOf('=');

            if (equals >= 0)
            {
                promotion =
                    char.ToLowerInvariant(
                        san[equals + 1]);

                san = san[..equals];
            }

            // Capture.
            bool capture = san.Contains('x');

            san = san.Replace("x", "");

            // Piece type.
            char pieceType = 'P';

            if ("KQRBN".Contains(san[0]))
            {
                pieceType = san[0];
                san = san[1..];
            }

            // Ostateczny square docelowy.
            if (san.Length < 2)
                throw new InvalidOperationException(
                    $"Nieprawidłowy SAN: {san}");

            string target =
                san[^2..];

            int toFile = target[0] - 'a';
            int toRank = target[1] - '1';

            // Pozostała część = disambiguacja.
            string disambiguation =
                san[..^2];

            char boardPiece =
                _whiteToMove
                    ? pieceType
                    : char.ToLowerInvariant(pieceType);

            var candidates = new List<(int File, int Rank)>();

            for (int file = 0; file < 8; file++)
            {
                for (int rank = 0; rank < 8; rank++)
                {
                    if (_board[file, rank] != boardPiece)
                        continue;

                    if (!CanMove(
                            file,
                            rank,
                            toFile,
                            toRank,
                            boardPiece,
                            capture))
                    {
                        continue;
                    }

                    // SAN disambiguation:
                    // Nbd2
                    // R1e2
                    if (!string.IsNullOrEmpty(disambiguation))
                    {
                        bool matches = true;

                        foreach (char c in disambiguation)
                        {
                            if (c >= 'a' && c <= 'h')
                            {
                                if (file != c - 'a')
                                    matches = false;
                            }
                            else if (c >= '1' && c <= '8')
                            {
                                if (rank != c - '1')
                                    matches = false;
                            }
                        }

                        if (!matches)
                            continue;
                    }

                    candidates.Add((file, rank));
                }
            }

            if (candidates.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Nie można jednoznacznie przekształcić SAN '{san}'." +
                    $" Kandydaci: {candidates.Count}");
            }

            var from = candidates[0];

            string uciMove =
                $"{(char)('a' + from.File)}" +
                $"{(char)('1' + from.Rank)}" +
                $"{(char)('a' + toFile)}" +
                $"{(char)('1' + toRank)}";

            if (promotion != '\0')
                uciMove += promotion;

            return uciMove;
        }

        private bool CanMove(
            int fromFile,
            int fromRank,
            int toFile,
            int toRank,
            char piece,
            bool capture)
        {
            int df = toFile - fromFile;
            int dr = toRank - fromRank;

            char destination =
                _board[toFile, toRank];

            // Nie można bić własnej figury.
            if (destination != '.' &&
                char.IsUpper(destination) ==
                char.IsUpper(piece))
            {
                return false;
            }

            switch (char.ToUpperInvariant(piece))
            {
                case 'P':
                {
                    int direction =
                        char.IsUpper(piece) ? 1 : -1;

                    // Zwykły ruch
                    if (!capture &&
                        df == 0 &&
                        dr == direction &&
                        destination == '.')
                    {
                        return true;
                    }

                    // Podwójny ruch
                    int startRank =
                        char.IsUpper(piece) ? 1 : 6;

                    if (!capture &&
                        fromRank == startRank &&
                        df == 0 &&
                        dr == 2 * direction &&
                        destination == '.' &&
                        _board[
                            fromFile,
                            fromRank + direction] == '.')
                    {
                        return true;
                    }

                    // Bicie
                    if (Math.Abs(df) == 1 &&
                        dr == direction)
                    {
                        return capture ||
                               destination != '.';
                    }

                    return false;
                }

                case 'N':
                    return
                        (Math.Abs(df) == 1 &&
                         Math.Abs(dr) == 2) ||
                        (Math.Abs(df) == 2 &&
                         Math.Abs(dr) == 1);

                case 'K':
                    return
                        Math.Abs(df) <= 1 &&
                        Math.Abs(dr) <= 1 &&
                        (df != 0 || dr != 0);

                case 'B':
                    return
                        Math.Abs(df) == Math.Abs(dr) &&
                        IsPathClear(
                            fromFile,
                            fromRank,
                            toFile,
                            toRank);

                case 'R':
                    return
                        (df == 0 || dr == 0) &&
                        (df != 0 || dr != 0) &&
                        IsPathClear(
                            fromFile,
                            fromRank,
                            toFile,
                            toRank);

                case 'Q':
                    return
                        (
                            Math.Abs(df) == Math.Abs(dr) ||
                            df == 0 ||
                            dr == 0
                        ) &&
                        (df != 0 || dr != 0) &&
                        IsPathClear(
                            fromFile,
                            fromRank,
                            toFile,
                            toRank);

                default:
                    return false;
            }
        }

        private bool IsPathClear(
            int fromFile,
            int fromRank,
            int toFile,
            int toRank)
        {
            int stepFile =
                Math.Sign(toFile - fromFile);

            int stepRank =
                Math.Sign(toRank - fromRank);

            int file = fromFile + stepFile;
            int rank = fromRank + stepRank;

            while (file != toFile || rank != toRank)
            {
                if (_board[file, rank] != '.')
                    return false;

                file += stepFile;
                rank += stepRank;
            }

            return true;
        }

        public void MakeMove(string uci)
        {
            int fromFile = uci[0] - 'a';
            int fromRank = uci[1] - '1';
            int toFile = uci[2] - 'a';
            int toRank = uci[3] - '1';

            char piece =
                _board[fromFile, fromRank];

            _board[fromFile, fromRank] = '.';

            char newPiece = piece;

            if (uci.Length == 5)
            {
                char promotion = uci[4];

                newPiece = _whiteToMove
                    ? char.ToUpperInvariant(promotion)
                    : char.ToLowerInvariant(promotion);
            }

            // En passant.
            if (char.ToLowerInvariant(piece) == 'p' &&
                fromFile != toFile &&
                _board[toFile, toRank] == '.')
            {
                int capturedRank =
                    _whiteToMove
                        ? toRank - 1
                        : toRank + 1;

                if (capturedRank >= 0 &&
                    capturedRank < 8)
                {
                    _board[toFile, capturedRank] = '.';
                }
            }

            // Castling.
            if (char.ToLowerInvariant(piece) == 'k' &&
                Math.Abs(toFile - fromFile) == 2)
            {
                if (toFile == 6)
                {
                    _board[5, fromRank] =
                        _board[7, fromRank];

                    _board[7, fromRank] = '.';
                }
                else
                {
                    _board[3, fromRank] =
                        _board[0, fromRank];

                    _board[0, fromRank] = '.';
                }
            }

            _board[toFile, toRank] = newPiece;

            _whiteToMove = !_whiteToMove;
        }
    }

    public static void Main(string[] args)
    {
        string pgnPath = "UHO_XXL_+0.80_+1.09.pgn";
        int count = 100; // Liczba pozycji do przetworzenia

        try
        {
            var positions = Extract(pgnPath, count);

            foreach (var position in positions)
            {
                Console.WriteLine(position);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Wystąpił błąd: {ex.Message}");
        }
    }
}
