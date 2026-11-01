using System;
using System.Diagnostics;
using System.Text;

namespace Janggi
{
    // Fairy-Stockfish(GPL v3)를 별도 프로세스로 띄워 UCI로 수를 받아옴.
    // PC는 System.Diagnostics.Process, Android(arm64)는 Java Runtime.exec로 실행
    public class FairyStockfishEngine : IDisposable
    {
        // 장기 변형은 대국 규칙(GameManager.EngineVariant)에 따라 요청마다 지정. 시작값은 빅장·점수 판정 없는 변형
        private string currentVariant = "janggicasual";

        private readonly object sync = new object();

        // 장기 신경망 평가(NNUE). 파일 이름이 변형 이름(janggi)으로 시작해야 엔진이 janggi·janggicasual·janggitraditional에 씀
        public const string NnueFileName = "janggi-9991472750de.nnue";
        private readonly string evalFile;
        private readonly int threads, hashMb;

#if UNITY_ANDROID && !UNITY_EDITOR
        private readonly UnityEngine.AndroidJavaObject process, input, output;

        // 설치 시 풀린 네이티브 라이브러리 폴더만 실행 권한이 있음 (앱 데이터 폴더는 Android 10+에서 실행 불가)
        public static string NativeLibraryDir()
        {
            using (var info = UnityEngine.Android.AndroidApplication.currentActivity.Call<UnityEngine.AndroidJavaObject>("getApplicationInfo"))
                return info.Get<string>("nativeLibraryDir");
        }

        public static string DefaultPath() => NativeLibraryDir() + "/libfairystockfish.so";

        // NNUE는 .so 이름으로 함께 풀리지만 엔진은 파일 이름으로 변형을 가리므로, 앱 데이터 폴더에 원래 이름으로 한 번 복사
        public static string PrepareNnue()
        {
            string src = NativeLibraryDir() + "/libjanggi_nnue.so";
            string dst = System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, NnueFileName);
            if (!System.IO.File.Exists(src)) return null;
            long size = new System.IO.FileInfo(src).Length;
            if (!System.IO.File.Exists(dst) || new System.IO.FileInfo(dst).Length != size)
            {
                string tmp = dst + ".tmp"; // 복사 중 앱이 꺼져도 반쯤 쓴 파일을 쓰지 않도록
                System.IO.File.Copy(src, tmp, true);
                if (System.IO.File.Exists(dst)) System.IO.File.Delete(dst);
                System.IO.File.Move(tmp, dst);
            }
            return dst;
        }

        public FairyStockfishEngine(string exePath, string evalFile = null, int threads = 1, int hashMb = 16)
        {
            this.evalFile = evalFile;
            this.threads = threads;
            this.hashMb = hashMb;
            using (var runtime = new UnityEngine.AndroidJavaClass("java.lang.Runtime"))
            using (var rt = runtime.CallStatic<UnityEngine.AndroidJavaObject>("getRuntime"))
                process = rt.Call<UnityEngine.AndroidJavaObject>("exec", exePath);
            input = new UnityEngine.AndroidJavaObject("java.io.PrintStream", process.Call<UnityEngine.AndroidJavaObject>("getOutputStream"), true);
            output = new UnityEngine.AndroidJavaObject("java.io.BufferedReader",
                new UnityEngine.AndroidJavaObject("java.io.InputStreamReader", process.Call<UnityEngine.AndroidJavaObject>("getInputStream")));
            try { Init(); }
            catch (Exception) { Dispose(); throw; } // 신경망을 못 읽어 끝난 프로세스 정리
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

        public FairyStockfishEngine(string exePath, string evalFile = null, int threads = 1, int hashMb = 16)
        {
            this.evalFile = evalFile;
            this.threads = threads;
            this.hashMb = hashMb;
            process = Process.Start(new ProcessStartInfo(exePath)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            try { Init(); }
            catch (Exception) { Dispose(); throw; } // 신경망을 못 읽어 끝난 프로세스 정리
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

        private void Init()
        {
            Send("uci");
            WaitFor("uciok");
            Send($"setoption name UCI_Variant value {currentVariant}");
            Send($"setoption name Threads value {threads}");
            Send($"setoption name Hash value {hashMb}");
            if (evalFile != null) Send($"setoption name EvalFile value {evalFile}");
            Send("isready");
            WaitFor("readyok");
            // 신경망을 못 읽으면 엔진이 스스로 종료하므로, 첫 수읽기까지 확인해 실패를 생성 시점에 드러냄
            Send("go depth 1");
            WaitFor("bestmove");
        }

        // 백그라운드 스레드에서 호출 가능. 이전 요청이 끝날 때까지 기다린 뒤 실행
        public Move GetBestMove(PieceData[,] board, Team sideToMove, string variant, int skillLevel, int moveTimeMs)
        {
            lock (sync)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                // 백그라운드 스레드에서 JNI를 쓰려면 스레드를 JVM에 붙여야 함
                UnityEngine.AndroidJNI.AttachCurrentThread();
                try
                {
#endif
                if (variant != currentVariant)
                {
                    currentVariant = variant;
                    Send($"setoption name UCI_Variant value {variant}");
                    Send("isready");
                    WaitFor("readyok");
                }
                Send($"setoption name Skill Level value {skillLevel}"); // 단 변경이 다음 수부터 바로 반영되도록 매번 지정
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
