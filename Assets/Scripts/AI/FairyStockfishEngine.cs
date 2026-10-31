using System;
using System.Diagnostics;
using System.Text;

namespace Janggi
{
    // Fairy-Stockfish(GPL v3)를 별도 프로세스로 띄워 UCI로 수를 받아옴.
    // PC는 System.Diagnostics.Process, Android(arm64)는 Java Runtime.exec로 실행
    public class FairyStockfishEngine : IDisposable
    {
        // 앱 규칙(빅장·점수 판정 없음)과 가장 가까운 장기 변형
        private const string Variant = "janggicasual";

        private readonly object sync = new object();

#if UNITY_ANDROID && !UNITY_EDITOR
        private readonly UnityEngine.AndroidJavaObject process, input, output;

        // 설치 시 풀린 네이티브 라이브러리 폴더만 실행 권한이 있음 (앱 데이터 폴더는 Android 10+에서 실행 불가)
        public static string DefaultPath()
        {
            using (var info = UnityEngine.Android.AndroidApplication.currentActivity.Call<UnityEngine.AndroidJavaObject>("getApplicationInfo"))
                return info.Get<string>("nativeLibraryDir") + "/libfairystockfish.so";
        }

        public FairyStockfishEngine(string exePath, int skillLevel)
        {
            using (var runtime = new UnityEngine.AndroidJavaClass("java.lang.Runtime"))
            using (var rt = runtime.CallStatic<UnityEngine.AndroidJavaObject>("getRuntime"))
                process = rt.Call<UnityEngine.AndroidJavaObject>("exec", exePath);
            input = new UnityEngine.AndroidJavaObject("java.io.PrintStream", process.Call<UnityEngine.AndroidJavaObject>("getOutputStream"), true);
            output = new UnityEngine.AndroidJavaObject("java.io.BufferedReader",
                new UnityEngine.AndroidJavaObject("java.io.InputStreamReader", process.Call<UnityEngine.AndroidJavaObject>("getInputStream")));
            Init(skillLevel);
        }

        private void Send(string cmd) => input.Call("println", cmd);
        private string ReadLine() => output.Call<string>("readLine");

        public void Dispose()
        {
            try { Send("quit"); } catch (Exception) { /* 이미 종료된 프로세스 */ }
            process.Call("destroy");
            input.Dispose();
            output.Dispose();
            process.Dispose();
        }
#else
        private readonly Process process;

        public FairyStockfishEngine(string exePath, int skillLevel)
        {
            process = Process.Start(new ProcessStartInfo(exePath)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            Init(skillLevel);
        }

        private void Send(string cmd)
        {
            process.StandardInput.WriteLine(cmd);
            process.StandardInput.Flush();
        }

        private string ReadLine() => process.StandardOutput.ReadLine();

        public void Dispose()
        {
            try
            {
                if (!process.HasExited)
                {
                    Send("quit");
                    if (!process.WaitForExit(500)) process.Kill();
                }
            }
            catch (Exception) { /* 이미 종료된 프로세스 */ }
            process.Dispose();
        }
#endif

        private void Init(int skillLevel)
        {
            Send("uci");
            WaitFor("uciok");
            Send($"setoption name UCI_Variant value {Variant}");
            Send($"setoption name Skill Level value {skillLevel}");
            Send("isready");
            WaitFor("readyok");
        }

        // 백그라운드 스레드에서 호출 가능. 이전 요청이 끝날 때까지 기다린 뒤 실행
        public Move GetBestMove(PieceData[,] board, Team sideToMove, int moveTimeMs)
        {
            lock (sync)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                // 백그라운드 스레드에서 JNI를 쓰려면 스레드를 JVM에 붙여야 함
                UnityEngine.AndroidJNI.AttachCurrentThread();
                try
                {
#endif
                Send($"position fen {ToFen(board, sideToMove)}");
                Send($"go movetime {moveTimeMs}");
                string line = WaitFor("bestmove");
                return ParseMove(line.Split(' ')[1]);
#if UNITY_ANDROID && !UNITY_EDITOR
                }
                finally
                {
                    UnityEngine.AndroidJNI.DetachCurrentThread();
                }
#endif
            }
        }

        private string WaitFor(string prefix)
        {
            string line;
            while ((line = ReadLine()) != null)
            {
                if (line.StartsWith(prefix)) return line;
            }
            throw new InvalidOperationException("Fairy-Stockfish 프로세스가 종료되었습니다.");
        }

        // 초(Cho)=백(대문자, 1~4줄), 한(Han)=흑. 칸 이름: 가로 a~i = x, 세로 1~10 = y+1
        public static string ToFen(PieceData[,] board, Team sideToMove)
        {
            var sb = new StringBuilder();
            for (int y = 9; y >= 0; y--)
            {
                int empty = 0;
                for (int x = 0; x < 9; x++)
                {
                    PieceData p = board[x, y];
                    if (p == null || p.IsCaptured) { empty++; continue; }
                    if (empty > 0) { sb.Append(empty); empty = 0; }
                    char c = PieceChar(p.Type);
                    sb.Append(p.Team == Team.Cho ? char.ToUpper(c) : c);
                }
                if (empty > 0) sb.Append(empty);
                if (y > 0) sb.Append('/');
            }
            sb.Append(sideToMove == Team.Cho ? " w - - 0 1" : " b - - 0 1");
            return sb.ToString();
        }

        private static char PieceChar(PieceType type)
        {
            switch (type)
            {
                case PieceType.King: return 'k';
                case PieceType.Guard: return 'a';
                case PieceType.Car: return 'r';
                case PieceType.Cannon: return 'c';
                case PieceType.Horse: return 'n';
                case PieceType.Elephant: return 'b';
                default: return 'p';
            }
        }

        // "i4h4", "e10e9" 형식. 한수 쉼은 궁이 제자리로 가는 수(e2e2)라 From == To가 됨
        public static Move ParseMove(string uci)
        {
            int split = 1;
            while (char.IsDigit(uci[split])) split++;
            return new Move(ParseSquare(uci.Substring(0, split)), ParseSquare(uci.Substring(split)));
        }

        private static BoardPos ParseSquare(string sq) => new BoardPos(sq[0] - 'a', int.Parse(sq.Substring(1)) - 1);
    }
}
