using System;
using System.Collections.Generic;

namespace TabletopReversi
{
    /// <summary>
    /// A deterministic legal match edited into a short rules film. This is also
    /// the source of truth for the captions and chapter seeking in the player.
    /// </summary>
    public static class ReversiReplayTimeline
    {
        public const double Duration = 32;
        public const double SelectionDuration = 10;
        const double FlipDuration = .5;
        static readonly Position[] positions;
        static readonly Key[] keys;
        static readonly IReadOnlyList<Chapter> chapters;

        public sealed class Chapter
        {
            public readonly double Start, End;
            public readonly string Title, Caption;
            public Chapter(double start, double end, string title, string caption)
            {
                Start = start; End = end; Title = title; Caption = caption;
            }
        }

        public sealed class Frame
        {
            public int[,] Board;
            public int CurrentPlayer, LastRow, LastCol, MoveCount;
            public bool IsFinished;
            public int AnimatedRow, AnimatedCol, PlacedPlayer;
            public List<Move> Flips;
            public float MoveProgress;
            public bool IsCut;
            public string Caption;
        }

        sealed class Position
        {
            public int[,] Board;
            public int CurrentPlayer, LastRow, LastCol, MoveCount, PlacedPlayer;
            public bool IsFinished;
            public List<Move> Flips;
        }

        struct Key
        {
            public double At;
            public int Position;
            public bool Animate, Cut;
            public Key(double at, int position, bool animate = true, bool cut = false)
            {
                At = at; Position = position; Animate = animate; Cut = cut;
            }
        }

        static ReversiReplayTimeline()
        {
            positions = BuildMatch();
            int final = positions.Length - 1;
            int middle = FindMiddleStart(positions);
            keys = new[] {
                new Key(0, 0, false),
                new Key(1, 1), new Key(3, 2), new Key(5, 3), new Key(7, 4), new Key(9, 5),
                new Key(10, middle - 1, false, true),
                new Key(11, middle), new Key(13.2, middle + 1),
                new Key(15.4, middle + 2), new Key(17.6, middle + 3),
                new Key(20, final - 4, false, true),
                new Key(21.6, final - 3), new Key(23.7, final - 2),
                new Key(25.8, final - 1), new Key(27.9, final),
                new Key(29, final, false)
            };
            int black = Count(positions[final].Board, ReversiRules.Black);
            int white = Count(positions[final].Board, ReversiRules.White);
            string winner = black == white ? "引き分けです。" : black > white ? "黒の勝ちです。" : "白の勝ちです。";
            chapters = Array.AsReadOnly(new[] {
                new Chapter(0, 3, "はじめかた", "黒から先に、交互にコマを1つずつ置きます。"),
                new Chapter(3, 10, "はさんで返す", "相手のコマをはさむと、間のコマが自分の色に返ります。"),
                new Chapter(10, 11, "", ""),
                new Chapter(11, 20, "タテ・ヨコ・ナナメ", "タテ・ヨコ・ナナメにはさめます。一度に何枚も返せます。"),
                new Chapter(20, 21, "", ""),
                new Chapter(21, 29, "最後まで", "置ける場所に交互に置き、最後までコマを残しましょう。"),
                new Chapter(29, Duration, "対局終了", "最後にコマが多い色の勝ち。黒 " + black + "枚、白 " + white + "枚。" + winner)
            });
        }

        public static IReadOnlyList<Chapter> Chapters { get { return chapters; } }
        public static float TransitionOpacity(double time)
        {
            double distance = Math.Min(Math.Abs(time - 10), Math.Abs(time - 20));
            // Hold black briefly at the edit, then reveal the next position.
            return (float)Math.Min(1, Math.Max(0, (.35 - distance) / .28));
        }
        public static int TotalGameMoves { get { return positions.Length - 1; } }
        public static int FinalBlackCount { get { return Count(positions[positions.Length - 1].Board, ReversiRules.Black); } }
        public static int FinalWhiteCount { get { return Count(positions[positions.Length - 1].Board, ReversiRules.White); } }

        public static Chapter ChapterAt(double time)
        {
            time = ClampTime(time);
            for (int i = chapters.Count - 1; i >= 0; i--)
                if (time >= chapters[i].Start) return chapters[i];
            return chapters[0];
        }

        /// <summary>
        /// Returns independent board and flip-list copies, so rendering cannot
        /// change the cached match. Only the current move animates for 0.5 seconds.
        /// </summary>
        public static Frame Sample(double time)
        {
            time = ClampTime(time);
            int keyIndex = keys.Length - 1;
            while (keyIndex > 0 && time < keys[keyIndex].At) keyIndex--;
            Key key = keys[keyIndex];
            Position position = positions[key.Position];
            bool animate = key.Animate && time - key.At < FlipDuration;
            double elapsed = Math.Max(0, Math.Min(FlipDuration, time - key.At));
            return new Frame {
                Board = (int[,])position.Board.Clone(),
                CurrentPlayer = position.CurrentPlayer,
                LastRow = position.LastRow,
                LastCol = position.LastCol,
                MoveCount = position.MoveCount,
                IsFinished = position.IsFinished,
                AnimatedRow = animate ? position.LastRow : -1,
                AnimatedCol = animate ? position.LastCol : -1,
                PlacedPlayer = animate ? position.PlacedPlayer : ReversiRules.Empty,
                Flips = animate ? new List<Move>(position.Flips) : new List<Move>(),
                MoveProgress = animate ? (float)(elapsed / FlipDuration) : 1f,
                IsCut = key.Cut,
                Caption = ChapterAt(time).Caption
            };
        }

        static double ClampTime(double time)
        {
            if (double.IsNaN(time) || time < 0) return 0;
            return time > Duration ? Duration : time;
        }

        static Position[] BuildMatch()
        {
            // Row-major legal moves plus a small portable PRNG make this film
            // identical on the editor, native player and offline video renderer.
            Position[] best = null;
            for (uint seed = 81; seed < 113; seed++)
            {
                uint random = seed;
                var game = new ReversiRules();
                var result = new List<Position> { Capture(game, 0, new List<Move>()) };
                while (!game.IsFinished && result.Count <= 60)
                {
                    List<Move> legal = game.LegalMoves();
                    if (legal.Count == 0) throw new InvalidOperationException("Replay encountered an unnormalised turn.");
                    Move move = legal[(int)(Next(ref random) % (uint)legal.Count)];
                    int player = game.CurrentPlayer;
                    List<Move> flips = game.GetFlips(move.Row, move.Col, player);
                    if (!game.TryMove(move.Row, move.Col)) throw new InvalidOperationException("Replay contains an illegal move.");
                    result.Add(Capture(game, player, flips));
                }
                if (!game.IsFinished) throw new InvalidOperationException("Replay did not finish within sixty moves.");
                Position[] candidate = result.ToArray();
                if (best == null || candidate.Length > best.Length) best = candidate;
                int middle = FindMiddleStart(candidate);
                if (game.Count(ReversiRules.Empty) == 0 && candidate[middle + 1].Flips.Count >= 4)
                    return candidate;
            }
            if (best == null || best.Length < 16) throw new InvalidOperationException("Could not build a useful replay.");
            return best;
        }

        static Position Capture(ReversiRules game, int player, List<Move> flips)
        {
            return new Position {
                Board = (int[,])game.Board.Clone(), CurrentPlayer = game.CurrentPlayer,
                LastRow = game.LastRow, LastCol = game.LastCol, MoveCount = game.MoveCount,
                IsFinished = game.IsFinished, PlacedPlayer = player, Flips = new List<Move>(flips)
            };
        }

        static int FindMiddleStart(Position[] match)
        {
            int strongest = Math.Min(20, match.Length - 8), best = int.MinValue;
            for (int i = 16; i <= Math.Min(40, match.Length - 8); i++)
            {
                Position move = match[i];
                int directions = DirectionCount(move);
                int score = move.Flips.Count * 3 + directions * 7 - Math.Abs(i - 28) / 3;
                if (score > best) { best = score; strongest = i; }
            }
            // Show one setup move, followed by the multi-direction capture.
            return Math.Max(6, strongest - 1);
        }

        static int DirectionCount(Position move)
        {
            var directions = new HashSet<int>();
            foreach (Move flip in move.Flips)
                directions.Add((Math.Sign(flip.Row - move.LastRow) + 1) * 3 + Math.Sign(flip.Col - move.LastCol) + 1);
            return directions.Count;
        }

        static int Count(int[,] board, int player)
        {
            int count = 0;
            foreach (int value in board) if (value == player) count++;
            return count;
        }

        static uint Next(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
    }
}
