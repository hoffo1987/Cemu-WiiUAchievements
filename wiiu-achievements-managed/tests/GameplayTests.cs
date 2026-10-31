using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WiiUAchievements;
static class GameplayTests
{
 static GameSnapshot Sample(GameplayState state,int frame,int rupees,string scene="sea",uint player=0x20001000)
 { return new GameSnapshot { Rupees=rupees,Health=12,MaxHealth=12,Utc=new DateTime(2026,10,30,0,0,0,DateTimeKind.Utc).AddMilliseconds(frame*100),Context=new GameplayContext(state,scene,player) }; }
 static byte[] Scene(string name){var b=new byte[26];Encoding.ASCII.GetBytes(name).CopyTo(b,0);Encoding.ASCII.GetBytes(name).CopyTo(b,12);return b;}
 public static void Run(Action<bool,string> check,string output)
 {
  var p=new byte[16];p[0]=p[8]=0x20;p[2]=p[10]=0x10;
  check(GameplayContext.Parse(Scene("sea"),p).State==GameplayState.Ready,"Live-derived scene/player context decodes a ready world");
  check(GameplayContext.Parse(Scene("Name"),p).State==GameplayState.Menu,"File select cannot become gameplay even with a stale player pointer");
  check(GameplayContext.Parse(Scene("sea_T"),p).State==GameplayState.Menu,"Title background actor cannot arm achievements");
  var b=Scene("sea");b[24]=1;
  check(GameplayContext.Parse(b,p).State==GameplayState.Loading,"Next-scene request blocks progression before save fields change");
  b[24]=2;check(GameplayContext.Parse(b,p).State==GameplayState.Unknown,"Unexpected transition encoding fails closed");
  check(GameplayContext.Parse(Scene("sea"),new byte[16]).State==GameplayState.Loading,"Absent Link suspends recording without disconnecting");
  var bad=(byte[])p.Clone();bad[8]=0x30;
  check(GameplayContext.Parse(Scene("sea"),bad).State==GameplayState.Unknown,"Disagreeing player copies cannot certify a snapshot");
  bad=(byte[])p.Clone();bad[3]=bad[11]=1;
  check(GameplayContext.Parse(Scene("sea"),bad).State==GameplayState.Unknown,"Unaligned player addresses are rejected");
  b=Scene("sea");b[6]=255;
  check(GameplayContext.Parse(b,p).State==GameplayState.Unknown,"Malformed stage names cannot become ready state");
  check(GameplayContext.Parse(new byte[1],p).State==GameplayState.Unknown,"Truncated context cannot certify a loaded game");
  var gate=new GameplayGate();
  check(!gate.Accept(Sample(GameplayState.Ready,0,0)) && !gate.Accept(Sample(GameplayState.Ready,7,0)) && gate.Accept(Sample(GameplayState.Ready,8,0)),"Automatic resume requires 750ms of continuous scene stability");
  check(!gate.Accept(Sample(GameplayState.Ready,9,0,"Omasao")),"Area changes establish a fresh guarded segment");
  check(!gate.Accept(Sample(GameplayState.Ready,30,0,"Omasao")),"Sampling gaps restart gameplay stabilization");
  check(!gate.Accept(Sample(GameplayState.Ready,29,0,"Omasao")),"Reversed timestamps cannot bypass stabilization");
  var rules=new List<Achievement>{new Achievement{Id="ten",Title="test",Description="test",Field="rupees",Threshold=10,Points=1}};
  var store=new ProgressStore(Path.Combine(output,"auto-load"),"isolated","synthetic");var engine=new AchievementEngine(rules,store);
  var menu=Sample(GameplayState.Menu,0,999,"Name",0);menu.MaxHealth=menu.Health=0;
  check(engine.Update(menu).Count==0&&engine.WaitingForGameplay,"Menus can suspend invalid save headers without a reader exception");
  for(int i=1;i<=15;i++)engine.Update(Sample(GameplayState.Ready,i,15));
  check(!store.Contains("ten")&&!engine.WaitingForGameplay,"Loaded already-advanced save resumes automatically without a retrospective award");
  for(int i=16;i<=18;i++)engine.Update(Sample(GameplayState.Ready,i,0));
  engine.Update(Sample(GameplayState.Ready,19,10));engine.Update(Sample(GameplayState.Ready,20,10));
  check(engine.Update(Sample(GameplayState.Ready,21,10)).Count==1,"A real crossing after automatic resume still awards normally");
  engine.Update(Sample(GameplayState.Menu,22,0,"Name",0));
  check(store.Contains("ten"),"Automatic suspension never removes earned progress");
  var second=new ProgressStore(Path.Combine(output,"auto-forward-load"),"isolated","synthetic");engine=new AchievementEngine(rules,second);
  for(int i=0;i<=12;i++)engine.Update(Sample(GameplayState.Ready,i,0));
  engine.Update(Sample(GameplayState.Loading,13,15));
  for(int i=14;i<=30;i++)engine.Update(Sample(GameplayState.Ready,i,15));
  check(!second.Contains("ten"),"Forward save loads are suppressed without requiring any permanent regression");
  engine.Update(Sample(GameplayState.Unknown,31,0));
  check(engine.WaitingForGameplay,"Unavailable memory clears pending acquisitions while keeping automatic retry possible");
  for(int i=32;i<=46;i++)engine.Update(Sample(GameplayState.Ready,i,0));
  check(!engine.WaitingForGameplay&&engine.Status("ten").Phase==AchievementPhase.Tracking,"Recovery from unavailable state re-arms a fresh stable baseline");
  engine.Update(Sample(GameplayState.Ready,47,10));engine.Update(Sample(GameplayState.Ready,48,10));
  engine.Update(Sample(GameplayState.Ready,49,10,"sea",0x20002000));
  check(!second.Contains("ten")&&engine.WaitingForGameplay,"Actor replacement cannot carry an almost-complete crossing into another loaded segment");
  var transitionStore=new ProgressStore(Path.Combine(output,"area-continuity"),"isolated","synthetic");engine=new AchievementEngine(rules,transitionStore);
  for(int i=0;i<=12;i++)engine.Update(Sample(GameplayState.Ready,i,0));
  var request=Sample(GameplayState.Loading,13,0);request.Context=new GameplayContext(GameplayState.Loading,"sea",0x20001000,true);engine.Update(request);
  for(int i=14;i<=30;i++)engine.Update(Sample(GameplayState.Loading,i,10));
  int awardCount=0;for(int i=31;i<=43;i++)awardCount+=engine.Update(Sample(GameplayState.Ready,i,10,"Omasao")).Count;
  check(awardCount==1&&transitionStore.Contains("ten"),"Observed in-world scene request preserves arming for progression committed during area exit");
  var boundaryStore=new ProgressStore(Path.Combine(output,"area-then-file-select"),"isolated","synthetic");engine=new AchievementEngine(rules,boundaryStore);
  for(int i=0;i<=12;i++)engine.Update(Sample(GameplayState.Ready,i,0));
  engine.Update(request);engine.Update(Sample(GameplayState.Menu,14,0,"Name",0));
  for(int i=15;i<=30;i++)engine.Update(Sample(GameplayState.Ready,i,15));
  check(!boundaryStore.Contains("ten"),"File select cancels preserved area arming before another save is loaded");
  var unknownStore=new ProgressStore(Path.Combine(output,"area-then-unavailable"),"isolated","synthetic");engine=new AchievementEngine(rules,unknownStore);
  for(int i=0;i<=12;i++)engine.Update(Sample(GameplayState.Ready,i,0));
  engine.Update(request);engine.Update(Sample(GameplayState.Unknown,14,0));
  for(int i=15;i<=30;i++)engine.Update(Sample(GameplayState.Ready,i,15));
  check(!unknownStore.Contains("ten"),"Unavailable memory cancels inherited arming across an uncertain boundary");
  check(gate.Accept(new GameSnapshot{Utc=DateTime.UtcNow}),"Legacy synthetic clients and demo do not invent a gameplay context");
 }
}
