using System;
using System.Collections.Generic;

namespace TabletopReversi
{
    /// <summary>One square on the board; (-1, -1) represents no available move.</summary>
    public struct Move : IEquatable<Move>
    {
        public int Row;
        public int Col;
        public Move(int row, int col) { Row = row; Col = col; }
        public bool Equals(Move other) { return Row == other.Row && Col == other.Col; }
        public override bool Equals(object obj) { return obj is Move && Equals((Move)obj); }
        public override int GetHashCode() { return Row * 8 + Col; }
        public override string ToString() { return Row < 0 ? "Pass" : ((char)('A' + Col)).ToString() + (Row + 1); }
    }

    /// <summary>Unity-independent rules, automatic passes, undo and bounded CPU search.</summary>
    public sealed class ReversiRules
    {
        public const int Black = 1;
        public const int White = -1;
        public const int Empty = 0;
        private static readonly int[] Directions = { -1, 0, 1 };
        private static readonly int[,] Weights = {
            {120,-28,18,8,8,18,-28,120}, {-28,-45,-5,-5,-5,-5,-45,-28},
            {18,-5,12,3,3,12,-5,18}, {8,-5,3,2,2,3,-5,8},
            {8,-5,3,2,2,3,-5,8}, {18,-5,12,3,3,12,-5,18},
            {-28,-45,-5,-5,-5,-5,-45,-28}, {120,-28,18,8,8,18,-28,120}
        };
        private readonly Stack<Snapshot> history = new Stack<Snapshot>();
        private readonly Random random = new Random();

        public int[,] Board { get; private set; }
        public int CurrentPlayer { get; private set; }
        public bool IsFinished { get; private set; }
        public int LastRow { get; private set; }
        public int LastCol { get; private set; }
        public int MoveCount { get; private set; }
        public int LastPassedPlayer { get; private set; }
        public int UndoCount { get { return history.Count; } }

        public ReversiRules() { Reset(); }

        /// <summary>Copies a position and normalizes a forced pass or completed game.</summary>
        public ReversiRules(int[,] board, int currentPlayer)
        {
            if (board == null || board.GetLength(0) != 8 || board.GetLength(1) != 8)
                throw new ArgumentException("A Reversi board must have 8 rows and 8 columns.", "board");
            ValidatePlayer(currentPlayer);
            foreach (int value in board)
                if (value != Empty && value != Black && value != White)
                    throw new ArgumentException("Invalid disc value.", "board");
            Board = (int[,])board.Clone();
            CurrentPlayer = currentPlayer;
            LastRow = LastCol = -1;
            NormalizeTurn();
        }

        public void Reset()
        {
            Board = new int[8, 8];
            Board[3, 3] = Board[4, 4] = White;
            Board[3, 4] = Board[4, 3] = Black;
            CurrentPlayer = Black;
            IsFinished = false;
            LastRow = LastCol = -1;
            LastPassedPlayer = Empty;
            MoveCount = 0;
            history.Clear();
        }

        public List<Move> LegalMoves() { return LegalMoves(CurrentPlayer); }

        public List<Move> LegalMoves(int player)
        {
            ValidatePlayer(player);
            var moves = new List<Move>();
            for (int row = 0; row < 8; row++)
                for (int col = 0; col < 8; col++)
                    if (IsLegal(row, col, player)) moves.Add(new Move(row, col));
            return moves;
        }

        public List<Move> GetFlips(int row, int col, int player)
        {
            ValidatePlayer(player);
            var flips = new List<Move>();
            if (!Inside(row, col) || Board[row, col] != Empty) return flips;
            foreach (int dr in Directions)
                foreach (int dc in Directions)
                {
                    if (dr == 0 && dc == 0) continue;
                    int r = row + dr, c = col + dc, length = 0;
                    while (Inside(r, c) && Board[r, c] == -player)
                    { length++; r += dr; c += dc; }
                    if (length == 0 || !Inside(r, c) || Board[r, c] != player) continue;
                    for (int i = 1; i <= length; i++) flips.Add(new Move(row + dr * i, col + dc * i));
                }
            return flips;
        }

        public bool TryMove(int row, int col)
        {
            if (IsFinished) return false;
            var flips = GetFlips(row, col, CurrentPlayer);
            if (flips.Count == 0) return false;
            history.Push(Capture());
            Apply(row, col, flips);
            return true;
        }

        public int Count(int player)
        {
            if (player != Empty) ValidatePlayer(player);
            int count = 0;
            foreach (int value in Board) if (value == player) count++;
            return count;
        }

        public bool Undo()
        {
            if (history.Count == 0) return false;
            Restore(history.Pop());
            return true;
        }

        public Move BestMove(int difficulty) { return BestMove(CurrentPlayer, difficulty); }

        /// <summary>0: random legal move; 1: two plies; 2: four plies (six late in the game).</summary>
        public Move BestMove(int player, int difficulty)
        {
            ValidatePlayer(player);
            if (IsFinished) return new Move(-1, -1);
            var moves = LegalMoves(player);
            if (moves.Count == 0) return new Move(-1, -1);
            if (difficulty <= 0) return moves[random.Next(moves.Count)];

            // Search copies never carry gameplay history, and the live position stays untouched.
            var search = new ReversiRules(Board, player);
            search.OrderMoves(moves);
            int depth = difficulty == 1 ? 2 : (Count(Empty) <= 12 ? 6 : 4);
            int nodes = 0, budget = difficulty == 1 ? 3000 : 18000;
            int best = int.MinValue, alpha = -1000000;
            Move chosen = moves[0];
            foreach (Move move in moves)
            {
                Snapshot previous = search.Capture();
                search.Apply(move.Row, move.Col, search.GetFlips(move.Row, move.Col, player));
                int score = search.Search(depth - 1, player, alpha, 1000000, ref nodes, budget);
                search.Restore(previous);
                if (score > best) { best = score; chosen = move; }
                alpha = Math.Max(alpha, best);
                if (nodes >= budget) break;
            }
            return chosen;
        }

        private int Search(int depth, int rootPlayer, int alpha, int beta, ref int nodes, int budget)
        {
            nodes++;
            if (IsFinished || depth <= 0 || nodes >= budget) return Evaluate(rootPlayer);
            var moves = LegalMoves();
            OrderMoves(moves);
            bool maximizing = CurrentPlayer == rootPlayer;
            int best = maximizing ? -1000000 : 1000000;
            foreach (Move move in moves)
            {
                Snapshot previous = Capture();
                Apply(move.Row, move.Col, GetFlips(move.Row, move.Col, CurrentPlayer));
                int score = Search(depth - 1, rootPlayer, alpha, beta, ref nodes, budget);
                Restore(previous);
                if (maximizing) { best = Math.Max(best, score); alpha = Math.Max(alpha, best); }
                else { best = Math.Min(best, score); beta = Math.Min(beta, best); }
                if (beta <= alpha || nodes >= budget) break;
            }
            return best;
        }

        private int Evaluate(int player)
        {
            int difference = Count(player) - Count(-player);
            if (IsFinished) return difference == 0 ? 0 : Math.Sign(difference) * 100000 + difference * 100;
            int positional = 0, frontier = 0;
            for (int row = 0; row < 8; row++)
                for (int col = 0; col < 8; col++)
                {
                    if (Board[row, col] == Empty) continue;
                    int sign = Board[row, col] * player;
                    positional += Weights[row, col] * sign;
                    bool exposed = false;
                    foreach (int dr in Directions)
                        foreach (int dc in Directions)
                            if ((dr != 0 || dc != 0) && Inside(row + dr, col + dc) && Board[row + dr, col + dc] == Empty)
                                exposed = true;
                    if (exposed) frontier += sign;
                }
            int mobility = LegalMoves(player).Count - LegalMoves(-player).Count;
            return positional * 3 + mobility * 12 - frontier * 5 + difference * (Count(Empty) < 16 ? 8 : 1);
        }

        private void Apply(int row, int col, List<Move> flips)
        {
            Board[row, col] = CurrentPlayer;
            foreach (Move flip in flips) Board[flip.Row, flip.Col] = CurrentPlayer;
            LastRow = row; LastCol = col; MoveCount++;
            CurrentPlayer = -CurrentPlayer;
            LastPassedPlayer = Empty;
            NormalizeTurn();
        }

        private void NormalizeTurn()
        {
            IsFinished = false;
            if (HasMove(CurrentPlayer)) return;
            if (HasMove(-CurrentPlayer))
            {
                LastPassedPlayer = CurrentPlayer;
                CurrentPlayer = -CurrentPlayer;
            }
            else IsFinished = true;
        }

        private bool HasMove(int player)
        {
            for (int row = 0; row < 8; row++)
                for (int col = 0; col < 8; col++)
                    if (IsLegal(row, col, player)) return true;
            return false;
        }

        private bool IsLegal(int row, int col, int player)
        {
            if (Board[row, col] != Empty) return false;
            foreach (int dr in Directions)
                foreach (int dc in Directions)
                {
                    if (dr == 0 && dc == 0) continue;
                    int r = row + dr, c = col + dc;
                    if (!Inside(r, c) || Board[r, c] != -player) continue;
                    do { r += dr; c += dc; } while (Inside(r, c) && Board[r, c] == -player);
                    if (Inside(r, c) && Board[r, c] == player) return true;
                }
            return false;
        }

        private void OrderMoves(List<Move> moves)
        {
            moves.Sort((a, b) => Weights[b.Row, b.Col].CompareTo(Weights[a.Row, a.Col]));
        }

        private static bool Inside(int row, int col) { return row >= 0 && row < 8 && col >= 0 && col < 8; }
        private static void ValidatePlayer(int player)
        {
            if (player != Black && player != White) throw new ArgumentOutOfRangeException("player");
        }

        private struct Snapshot
        {
            public int[,] Board;
            public int Player, Row, Col, Moves, Passed;
            public bool Finished;
        }
        private Snapshot Capture()
        {
            return new Snapshot { Board = (int[,])Board.Clone(), Player = CurrentPlayer, Row = LastRow,
                Col = LastCol, Moves = MoveCount, Passed = LastPassedPlayer, Finished = IsFinished };
        }
        private void Restore(Snapshot snapshot)
        {
            Board = snapshot.Board; CurrentPlayer = snapshot.Player; LastRow = snapshot.Row;
            LastCol = snapshot.Col; MoveCount = snapshot.Moves; LastPassedPlayer = snapshot.Passed;
            IsFinished = snapshot.Finished;
        }
    }
}
