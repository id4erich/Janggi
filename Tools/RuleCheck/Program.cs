// 무작위 대국을 두면서 국면마다 RuleValidator.GetLegalMoves와 Fairy-Stockfish(janggicasual)의 "go perft 1" 결과를 비교한다.
// 한수 쉼(궁이 제자리로 가는 수)은 앱에서 별도 버튼이라 비교에서 뺀다.
using System; using System.Collections.Generic; using System.Diagnostics; using System.Linq; using Janggi;
namespace UnityEngine { }
static class P {
  static Process eng;
  static void Send(string s){ eng.StandardInput.WriteLine(s); eng.StandardInput.Flush(); }
  static string Sq(BoardPos p) => $"{(char)('a'+p.x)}{p.y+1}";
  static int Main(string[] a){
    eng = Process.Start(new ProcessStartInfo(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../FairyStockfish/fairy-stockfish-largeboard_x86-64-modern.exe")){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,CreateNoWindow=true});
    Send("uci"); Send("setoption name UCI_Variant value janggicasual"); Send("isready");
    string l; while((l=eng.StandardOutput.ReadLine())!="readyok"){}
    var rng = new Random(int.Parse(a[0])); int games=int.Parse(a[1]); int positions=0, mismatches=0;
    string[] setups = {"rnba1anbr","rbna1abnr","rnba1abnr","rbna1anbr"};
    for(int g=0; g<games; g++){
      string cho=setups[rng.Next(4)], han=setups[rng.Next(4)];
      string fen=$"{han}/4k4/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/4K4/{cho.ToUpper()} w - - 0 1";
      var board=new PieceData[9,10]; LoadFen(board, fen); Team side=Team.Cho; var moves=new List<string>();
      for(int ply=0; ply<150; ply++){
        Send($"position fen {fen}"+(moves.Count>0?" moves "+string.Join(" ",moves):"")); Send("go perft 1");
        var engSet=new HashSet<string>();
        while((l=eng.StandardOutput.ReadLine())!=null && !l.StartsWith("Nodes searched")){ int c=l.IndexOf(':'); if(c>0) engSet.Add(l.Substring(0,c).Trim()); }
        var ours=new HashSet<string>();
        for(int x=0;x<9;x++)for(int y=0;y<10;y++){ var pc=board[x,y]; if(pc!=null&&pc.Team==side) foreach(var t in RuleValidator.GetLegalMoves(pc,board)) ours.Add(Sq(pc.Position)+Sq(t)); }
        var engNoPass=new HashSet<string>(engSet.Where(m=>{ int i=1; while(char.IsDigit(m[i]))i++; return m.Substring(0,i)!=m.Substring(i); }));
        positions++;
        if(!engNoPass.SetEquals(ours)){ mismatches++; if(mismatches<=8){ Console.WriteLine($"MISMATCH g{g} ply{ply} side={side} fen={fen} moves={string.Join(" ",moves)}"); Console.WriteLine("  engine only: "+string.Join(",",engNoPass.Except(ours))); Console.WriteLine("  ours only:   "+string.Join(",",ours.Except(engNoPass))); } }
        var list=engNoPass.ToList(); if(list.Count==0) break; // 외통 또는 둘 수 없음
        string mv=list[rng.Next(list.Count)]; moves.Add(mv); Apply(board, mv); side= side==Team.Cho?Team.Han:Team.Cho;
      }
    }
    Console.WriteLine($"positions={positions} mismatches={mismatches}"); Send("quit"); return mismatches==0?0:1;
  }
  static void LoadFen(PieceData[,] b, string fen){ var rows=fen.Split(' ')[0].Split('/'); for(int r=0;r<10;r++){ int y=9-r,x=0; foreach(char ch in rows[r]){ if(char.IsDigit(ch)){x+=ch-'0';continue;} var t=char.ToLower(ch) switch{'k'=>PieceType.King,'a'=>PieceType.Guard,'r'=>PieceType.Car,'c'=>PieceType.Cannon,'n'=>PieceType.Horse,'b'=>PieceType.Elephant,_=>PieceType.Pawn}; b[x,y]=new PieceData(t,char.IsUpper(ch)?Team.Cho:Team.Han,new BoardPos(x,y)); x++; } } }
  static void Apply(PieceData[,] b, string m){ int i=1; while(char.IsDigit(m[i]))i++; var f=new BoardPos(m[0]-'a',int.Parse(m.Substring(1,i-1))-1); var t=new BoardPos(m[i]-'a',int.Parse(m.Substring(i+1))-1); var p=b[f.x,f.y]; b[t.x,t.y]=p; b[f.x,f.y]=null; p.Position=t; }
}
