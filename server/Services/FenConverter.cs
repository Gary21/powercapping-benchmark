namespace server.Services;

using System;

public static class FenConverter
{
    public static string MovesToFen(string moves)
    {
        char[,] board = CreateInitialBoard();

        bool whiteToMove = true;

        bool whiteCastleKingSide = true;
        bool whiteCastleQueenSide = true;
        bool blackCastleKingSide = true;
        bool blackCastleQueenSide = true;

        string? enPassant = null;

        int halfmoveClock = 0;
        int fullmoveNumber = 1;

        string[] moveList = moves.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        foreach (string move in moveList)
        {
            ApplyMove(
                board,
                move,
                ref whiteToMove,
                ref whiteCastleKingSide,
                ref whiteCastleQueenSide,
                ref blackCastleKingSide,
                ref blackCastleQueenSide,
                ref enPassant,
                ref halfmoveClock,
                ref fullmoveNumber);
        }

        return BuildFen(
            board,
            whiteToMove,
            whiteCastleKingSide,
            whiteCastleQueenSide,
            blackCastleKingSide,
            blackCastleQueenSide,
            enPassant,
            halfmoveClock,
            fullmoveNumber);
    }

    private static void ApplyMove(
        char[,] board,
        string move,
        ref bool whiteToMove,
        ref bool whiteCastleKingSide,
        ref bool whiteCastleQueenSide,
        ref bool blackCastleKingSide,
        ref bool blackCastleQueenSide,
        ref string? enPassant,
        ref int halfmoveClock,
        ref int fullmoveNumber)
    {
        if (move.Length < 4 || move.Length > 5)
            throw new ArgumentException($"Invalid UCI move: {move}");

        int fromFile = move[0] - 'a';
        int fromRank = move[1] - '1';
        int toFile = move[2] - 'a';
        int toRank = move[3] - '1';

        if (!IsValidSquare(fromFile, fromRank) ||
            !IsValidSquare(toFile, toRank))
        {
            throw new ArgumentException($"Invalid UCI move: {move}");
        }

        char piece = board[fromFile, fromRank];

        if (piece == '.')
        {
            throw new ArgumentException(
                $"No piece on {move[..2]} for move {move}");
        }

        bool isWhitePiece = char.IsUpper(piece);

        if (isWhitePiece != whiteToMove)
        {
            throw new ArgumentException(
                $"Wrong side to move: {move}");
        }

        bool isPawn = char.ToLowerInvariant(piece) == 'p';
        bool isCapture = board[toFile, toRank] != '.';

        // En passant capture
        if (isPawn &&
            fromFile != toFile &&
            board[toFile, toRank] == '.' &&
            enPassant == SquareName(toFile, toRank))
        {
            isCapture = true;

            int capturedPawnRank =
                whiteToMove ? toRank - 1 : toRank + 1;

            if (capturedPawnRank >= 0 &&
                capturedPawnRank < 8)
            {
                board[toFile, capturedPawnRank] = '.';
            }
        }

        // En passant target is valid only for the immediately
        // preceding move, so clear it before processing the move.
        enPassant = null;

        // ---------------------------------------------------------
        // Castling
        // ---------------------------------------------------------

        bool isCastling =
            char.ToLowerInvariant(piece) == 'k' &&
            Math.Abs(toFile - fromFile) == 2;

        if (isCastling)
        {
            if (whiteToMove)
            {
                if (fromFile != 4 || fromRank != 0)
                    throw new ArgumentException(
                        $"Invalid white castling move: {move}");

                if (toFile == 6) // O-O
                {
                    board[4, 0] = '.';
                    board[7, 0] = '.';

                    board[6, 0] = 'K';
                    board[5, 0] = 'R';
                }
                else if (toFile == 2) // O-O-O
                {
                    board[4, 0] = '.';
                    board[0, 0] = '.';

                    board[2, 0] = 'K';
                    board[3, 0] = 'R';
                }
                else
                {
                    throw new ArgumentException(
                        $"Invalid castling move: {move}");
                }

                whiteCastleKingSide = false;
                whiteCastleQueenSide = false;

                halfmoveClock++;
            }
            else
            {
                if (fromFile != 4 || fromRank != 7)
                    throw new ArgumentException(
                        $"Invalid black castling move: {move}");

                if (toFile == 6) // O-O
                {
                    board[4, 7] = '.';
                    board[7, 7] = '.';

                    board[6, 7] = 'k';
                    board[5, 7] = 'r';
                }
                else if (toFile == 2) // O-O-O
                {
                    board[4, 7] = '.';
                    board[0, 7] = '.';

                    board[2, 7] = 'k';
                    board[3, 7] = 'r';
                }
                else
                {
                    throw new ArgumentException(
                        $"Invalid castling move: {move}");
                }

                blackCastleKingSide = false;
                blackCastleQueenSide = false;

                halfmoveClock++;
            }
        }
        else
        {
            // ---------------------------------------------------------
            // Castling rights before moving the piece
            // ---------------------------------------------------------

            if (piece == 'K')
            {
                whiteCastleKingSide = false;
                whiteCastleQueenSide = false;
            }
            else if (piece == 'k')
            {
                blackCastleKingSide = false;
                blackCastleQueenSide = false;
            }
            else if (piece == 'R')
            {
                if (fromFile == 0 && fromRank == 0)
                    whiteCastleQueenSide = false;

                if (fromFile == 7 && fromRank == 0)
                    whiteCastleKingSide = false;
            }
            else if (piece == 'r')
            {
                if (fromFile == 0 && fromRank == 7)
                    blackCastleQueenSide = false;

                if (fromFile == 7 && fromRank == 7)
                    blackCastleKingSide = false;
            }

            // ---------------------------------------------------------
            // Castling rights when a rook is captured
            // ---------------------------------------------------------

            char capturedPiece = board[toFile, toRank];

            if (capturedPiece == 'R')
            {
                if (toFile == 0 && toRank == 0)
                    whiteCastleQueenSide = false;

                if (toFile == 7 && toRank == 0)
                    whiteCastleKingSide = false;
            }
            else if (capturedPiece == 'r')
            {
                if (toFile == 0 && toRank == 7)
                    blackCastleQueenSide = false;

                if (toFile == 7 && toRank == 7)
                    blackCastleKingSide = false;
            }

            // ---------------------------------------------------------
            // Move the piece
            // ---------------------------------------------------------

            board[fromFile, fromRank] = '.';

            char newPiece = piece;

            // Promotion: e7e8q, e7e8r, e7e8b, e7e8n
            if (move.Length == 5)
            {
                if (!isPawn)
                {
                    throw new ArgumentException(
                        $"Only pawns can promote: {move}");
                }

                char promotion =
                    char.ToLowerInvariant(move[4]);

                if (promotion != 'q' &&
                    promotion != 'r' &&
                    promotion != 'b' &&
                    promotion != 'n')
                {
                    throw new ArgumentException(
                        $"Invalid promotion piece: {move}");
                }

                newPiece = whiteToMove
                    ? char.ToUpperInvariant(promotion)
                    : promotion;
            }

            board[toFile, toRank] = newPiece;

            // ---------------------------------------------------------
            // Pawn double move -> en passant target square
            // ---------------------------------------------------------

            if (isPawn && Math.Abs(toRank - fromRank) == 2)
            {
                int epRank = (fromRank + toRank) / 2;

                enPassant = SquareName(
                    toFile,
                    epRank);
            }

            // ---------------------------------------------------------
            // 50-move rule counter
            // ---------------------------------------------------------

            if (isPawn || isCapture)
                halfmoveClock = 0;
            else
                halfmoveClock++;
        }

        // Fullmove number increments after Black's move.
        if (!whiteToMove)
            fullmoveNumber++;

        whiteToMove = !whiteToMove;
    }

    private static char[,] CreateInitialBoard()
    {
        var board = new char[8, 8];

        // Empty board
        for (int file = 0; file < 8; file++)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                board[file, rank] = '.';
            }
        }

        // White
        const string whiteBackRank = "RNBQKBNR";

        for (int file = 0; file < 8; file++)
        {
            board[file, 0] = whiteBackRank[file];
            board[file, 1] = 'P';
        }

        // Black
        const string blackBackRank = "rnbqkbnr";

        for (int file = 0; file < 8; file++)
        {
            board[file, 7] = blackBackRank[file];
            board[file, 6] = 'p';
        }

        return board;
    }

    private static string BuildFen(
        char[,] board,
        bool whiteToMove,
        bool whiteCastleKingSide,
        bool whiteCastleQueenSide,
        bool blackCastleKingSide,
        bool blackCastleQueenSide,
        string? enPassant,
        int halfmoveClock,
        int fullmoveNumber)
    {
        var fen = new System.Text.StringBuilder();

        // ---------------------------------------------------------
        // Board
        // ---------------------------------------------------------

        // FEN starts from rank 8 and ends on rank 1.
        for (int rank = 7; rank >= 0; rank--)
        {
            int emptySquares = 0;

            for (int file = 0; file < 8; file++)
            {
                char piece = board[file, rank];

                if (piece == '.')
                {
                    emptySquares++;
                }
                else
                {
                    if (emptySquares > 0)
                    {
                        fen.Append(emptySquares);
                        emptySquares = 0;
                    }

                    fen.Append(piece);
                }
            }

            if (emptySquares > 0)
                fen.Append(emptySquares);

            if (rank > 0)
                fen.Append('/');
        }

        // ---------------------------------------------------------
        // Side to move
        // ---------------------------------------------------------

        fen.Append(whiteToMove ? " w " : " b ");

        // ---------------------------------------------------------
        // Castling rights
        // ---------------------------------------------------------

        string castling = "";

        if (whiteCastleKingSide)
            castling += "K";

        if (whiteCastleQueenSide)
            castling += "Q";

        if (blackCastleKingSide)
            castling += "k";

        if (blackCastleQueenSide)
            castling += "q";

        fen.Append(
            string.IsNullOrEmpty(castling)
                ? "-"
                : castling);

        // ---------------------------------------------------------
        // En passant
        // ---------------------------------------------------------

        fen.Append(' ');
        fen.Append(enPassant ?? "-");

        // ---------------------------------------------------------
        // Halfmove clock
        // ---------------------------------------------------------

        fen.Append(' ');
        fen.Append(halfmoveClock);

        // ---------------------------------------------------------
        // Fullmove number
        // ---------------------------------------------------------

        fen.Append(' ');
        fen.Append(fullmoveNumber);

        return fen.ToString();
    }

    private static bool IsValidSquare(int file, int rank)
    {
        return file >= 0 &&
               file < 8 &&
               rank >= 0 &&
               rank < 8;
    }

    private static string SquareName(int file, int rank)
    {
        return $"{(char)('a' + file)}{(char)('1' + rank)}";
    }
}
