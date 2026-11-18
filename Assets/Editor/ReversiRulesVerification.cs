using System;
using System.Diagnostics;
using TabletopReversi;

/// <summary>Portable assertions callable through the editor or a standalone C# runner.</summary>
public static class ReversiRulesVerification
{
    public static string Run()
    {
        int assertions = 0;
        Action<bool, string> check = (condition, message) => {
            assertions++;
            if (!condition) throw new InvalidOperationException("Reversi verification failed: " + message);
        };
        var timer = Stopwatch.StartNew();
        var game = new ReversiRules();
        check(game.Count(1) == 2 && game.Count(-1) == 2 && game.Count(0) == 60, "initial disc counts");
        var moves = game.LegalMoves();
        check(moves.Count == 4 && moves.Contains(new Move(2, 3)) && moves.Contains(new Move(3, 2)) &&
            moves.Contains(new Move(4, 5)) && moves.Contains(new Move(5, 4)), "four standard opening moves");
        check(!game.TryMove(-1, 2) && !game.TryMove(0, 0) && !game.TryMove(3, 3), "illegal moves rejected");
        check(game.MoveCount == 0 && game.CurrentPlayer == 1 && game.UndoCount == 0, "illegal move leaves state intact");
        check(game.TryMove(2, 3), "legal opening accepted");
        check(game.Count(1) == 4 && game.Count(-1) == 1 && game.CurrentPlayer == -1, "opening flips disc and changes turn");
        check(game.LastRow == 2 && game.LastCol == 3 && game.MoveCount == 1, "last-move metadata");
        check(game.Undo() && game.Count(1) == 2 && game.Count(-1) == 2 && game.CurrentPlayer == 1 && game.MoveCount == 0,
            "undo restores board and turn");
        check(!game.Undo(), "empty history undo returns false");

        var radial = new int[8, 8];
        for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
                if (dr != 0 || dc != 0) { radial[3 + dr, 3 + dc] = -1; radial[3 + dr * 2, 3 + dc * 2] = 1; }
        game = new ReversiRules(radial, 1);
        check(game.GetFlips(3, 3, 1).Count == 8, "all eight directions detected");
        check(game.TryMove(3, 3) && game.Count(1) == 17 && game.Count(-1) == 0 && game.IsFinished,
            "all eight directions flipped and elimination finishes game");
        check(game.Undo() && !game.IsFinished && game.Count(-1) == 8, "undo restores finished game");

        var passBoard = Filled(1);
        passBoard[0, 0] = passBoard[0, 4] = 0;
        passBoard[0, 1] = passBoard[0, 3] = -1;
        game = new ReversiRules(passBoard, 1);
        check(game.TryMove(0, 0) && game.LastPassedPlayer == -1 && game.CurrentPlayer == 1 && !game.IsFinished,
            "forced white pass keeps black turn");
        check(game.TryMove(0, 4) && game.IsFinished && game.Count(1) == 64, "second move finishes full board");
        check(!game.TryMove(0, 0), "finished game rejects moves");
        check(game.Undo() && game.CurrentPlayer == 1 && game.LastPassedPlayer == -1 && game.MoveCount == 1,
            "undo restores pass metadata");
        game = new ReversiRules(passBoard, -1);
        check(game.CurrentPlayer == 1 && game.LastPassedPlayer == -1, "loaded position normalizes forced pass");
        check(passBoard[0, 0] == 0, "constructor isolates external board");

        var drawBoard = Filled(1);
        for (int row = 0; row < 4; row++) for (int col = 0; col < 8; col++) drawBoard[row, col] = -1;
        game = new ReversiRules(drawBoard, 1);
        check(game.IsFinished && game.Count(1) == 32 && game.Count(-1) == 32, "full board draw");
        check(game.BestMove(2).Row == -1, "finished CPU returns no move");
        var sparseEnd = new int[8, 8]; sparseEnd[0, 0] = 1;
        check(new ReversiRules(sparseEnd, 1).IsFinished, "no legal moves finishes before board is full");

        game = new ReversiRules();
        for (int difficulty = 0; difficulty <= 2; difficulty++)
        {
            Move best = game.BestMove(difficulty);
            check(game.LegalMoves().Contains(best), "CPU level " + difficulty + " returns legal move");
            check(game.MoveCount == 0 && game.Count(1) == 2 && game.UndoCount == 0, "CPU search preserves live state");
        }

        var random = new Random(451);
        int games = 30, observedPasses = 0;
        for (int round = 0; round < games; round++)
        {
            game = new ReversiRules();
            int played = 0;
            while (!game.IsFinished && played < 61)
            {
                moves = game.LegalMoves();
                check(moves.Count > 0, "active game always has legal current move");
                Move move = moves[random.Next(moves.Count)];
                int occupied = game.Count(1) + game.Count(-1);
                check(game.TryMove(move.Row, move.Col), "random legal move applies");
                check(game.Count(1) + game.Count(-1) == occupied + 1, "each move adds exactly one disc");
                if (game.LastPassedPlayer != 0) observedPasses++;
                played++;
            }
            check(game.IsFinished && played <= 60, "random game terminates within sixty moves");
            check(game.LegalMoves(1).Count == 0 && game.LegalMoves(-1).Count == 0, "finished position has no moves for either side");
        }
        check(observedPasses > 0, "random games exercise passes");

        // A complete CPU game catches search mistakes after automatic passes and near the endgame.
        game = new ReversiRules();
        while (!game.IsFinished && game.MoveCount < 61)
        {
            Move move = game.BestMove(game.MoveCount % 2 + 1);
            check(game.LegalMoves().Contains(move) && game.TryMove(move.Row, move.Col), "CPU full game move applies");
        }
        check(game.IsFinished && game.MoveCount <= 60, "CPU game terminates");
        timer.Stop();
        return "PASS: " + assertions + " assertions; 30 random games + 1 CPU game; forced passes, eight-way flips, endgame and undo; " + timer.ElapsedMilliseconds + " ms.";
    }

    private static int[,] Filled(int player)
    {
        var board = new int[8, 8];
        for (int row = 0; row < 8; row++) for (int col = 0; col < 8; col++) board[row, col] = player;
        return board;
    }
}
