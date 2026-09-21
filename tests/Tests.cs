using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace WardogsFastCalc {
 public static class Tests {
  static List<string> log=new List<string>();
  static int count;
  static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);count++;log.Add("PASS: "+name);}
  static void Near(double a,double b,string name){Check(Math.Abs(a-b)<0.00001,name);}
  static Coordinate Parse(string s){Coordinate c;if(!Calculator.TryCoordinate(s,out c))throw new Exception("Parse failed: "+s);return c;}
  static void Resize(Controller c,double width,double height){c.Window.Width=width;c.Window.Height=height;c.Window.UpdateLayout();c.UpdateLayout();c.Window.UpdateLayout();}
  public static int Run(string report){
   int result=0;report=Path.GetFullPath(report);Directory.CreateDirectory(Path.GetDirectoryName(report));
   string sessionDirectory=Path.Combine(Path.GetTempPath(),"WardogsFastCalc-Tests-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(sessionDirectory);
   try{
    string[] valid={"x98.43, y110.38","98.43,110.38","98.43 110.38","(98.43, 110.38)","98.43 / 110.38","X:98.43; Y:110.38","y110.38 x98.43","x98,43 y110,38","[98.43;110.38]"};
    foreach(string text in valid){var c=Parse(text);Near(c.X,98.43,"parse X: "+text);Near(c.Y,110.38,"parse Y: "+text);}
    string[] invalid={"","hello","98.43","1,2,3","x1 y2 x3","xNaN y2","NaN, 2","Infinity, 2","1e3, 2","-1,2","165,2","1,999999999999999999999999","x1 y2 z3","Player 22: x1 y2","1,2 trailing","98,43 110,38"};
    foreach(string text in invalid){Coordinate c;Check(!Calculator.TryCoordinate(text,out c),"reject: "+text);}
    Near(Parse("0,164").Y,164,"coordinate envelope boundary");
    var center=new Coordinate(100,100);
    double[][] directions={new[]{100d,101d,0d},new[]{101d,101d,45d},new[]{101d,100d,90d},new[]{101d,99d,135d},new[]{100d,99d,180d},new[]{99d,99d,225d},new[]{99d,100d,270d},new[]{99d,101d,315d}};
    foreach(var row in directions)Near(Calculator.Solve(center,new Coordinate(row[0],row[1]),null).Bearing,row[2],"compass "+row[2]);
    Near(Calculator.Solve(center,new Coordinate(103,104),null).Distance,500,"3-4-5 triangle and 100m scale");
    var sample=Calculator.Solve(Parse("98.43,110.38"),Parse("94.53,109.03"),null);
    Near(sample.Distance,Math.Sqrt(3.9*3.9+1.35*1.35)*100,"published coordinate example");
    Check(sample.Bearing>250&&sample.Bearing<252&&sample.Direction=="WSW","example is west southwest, not west northwest");
    Near(Calculator.EstimateMil(132).Value,850,"min table endpoint");Near(Calculator.EstimateMil(684).Value,150,"max table endpoint");
    foreach(double endpoint in new[]{132d,684d})foreach(int sign in new[]{-1,1}) {
     var edge=Calculator.Solve(center,new Coordinate(100+sign*endpoint/100,100),null);
     Check(edge.InRange&&edge.Mil.HasValue,"coordinate-derived range endpoint "+endpoint+" direction "+sign);
     Near(edge.Mil.Value,endpoint==132?850:150,"coordinate-derived endpoint MIL "+endpoint+" direction "+sign);
    }
    Check(!Calculator.Solve(center,new Coordinate(101.319999,100),null).InRange,"coordinate distance genuinely below minimum stays out of range");
    Check(!Calculator.Solve(center,new Coordinate(106.840001,100),null).InRange,"coordinate distance genuinely above maximum stays out of range");
    Near(Calculator.EstimateMil(300).Value,690,"exact table row");Near(Calculator.EstimateMil(305).Value,685,"interpolation");
    Check(!Calculator.EstimateMil(131.99).HasValue,"below min does not extrapolate");Check(!Calculator.EstimateMil(684.01).HasValue,"above max does not extrapolate");
    Check(!Calculator.EstimateMil(Double.NaN).HasValue,"NaN not a solution");
    var manual=Calculator.Solve(center,new Coordinate(103,104),450);Near(manual.Range,450,"override range");Near(manual.Distance,500,"override retains grid distance");Near(manual.Bearing,36.86989764584402,"override retains bearing");Check(manual.Overridden,"override marked active");
    bool threw=false;try{Calculator.Solve(center,center,500);}catch(ArgumentException){threw=true;}Check(threw,"coincident positions have no heading");
    foreach(double value in new[]{0d,-1d,25001d,Double.NaN,Double.PositiveInfinity}){threw=false;try{Calculator.Solve(center,new Coordinate(101,101),value);}catch(ArgumentException){threw=true;}Check(threw,"reject invalid distance "+value);}
    var culture=Thread.CurrentThread.CurrentCulture;Thread.CurrentThread.CurrentCulture=new CultureInfo("de-DE");Near(Parse("98.43 110.38").X,98.43,"locale-independent parsing");Check(sample.Callout.Contains("250.9"),"locale-independent copy");Thread.CurrentThread.CurrentCulture=culture;
    var screens=new[]{new Rect(0,0,1920,1040),new Rect(-1280,0,1280,984)};
    var fit=DesktopLayout.Fit(new Rect(-1100,60,900,700),screens);Near(fit.Left,-1100,"window remains on an available secondary monitor");
    fit=DesktopLayout.Fit(new Rect(-1100,60,900,700),new[]{screens[0]});Check(screens[0].Contains(fit),"window from a disconnected monitor moves fully onto an available screen");
    fit=DesktopLayout.Fit(new Rect(20,20,3000,2000),new[]{screens[0]});Check(screens[0].Contains(fit)&&fit.Size==screens[0].Size,"oversized window fits the desktop work area");
    var smallWorkArea=new Rect(0,0,853,480);fit=DesktopLayout.Fit(new Rect(0,0,1200,880),new[]{smallWorkArea});Check(smallWorkArea.Contains(fit),"small high-DPI work areas override the normal minimum height");
    var app=new Application();var c1=new Controller(null,()=>ModifierKeys.None);
    c1.Window.Loaded+=delegate{
     c1.Window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(delegate{
      try{
       Check(c1.Current==null,"empty UI has no solution");Check(!c1.Find<Button>("Copy").IsEnabled,"empty UI disables copy");
       Check(!c1.Scene.HasTarget&&!c1.Scene.ElevationVisual.HasValue,"empty scene has no target or elevation");
       c1.LoadExample();Check(c1.Current!=null&&c1.Current.InRange,"example renders valid result");Check(c1.Find<TextBlock>("Range").Text=="413","example RNG renders 413");
       Check(c1.Find<TextBlock>("Bearing").Text=="250.9°","example bearing renders 250.9");
       c1.RenderTo(Path.Combine(Path.GetDirectoryName(report),"app-example.png"));
       Near(c1.Scene.Bearing.Value,c1.Current.Bearing,"3D heading matches result");
       Check(c1.Scene.GeometryCount>100,"native scene geometry created");
       c1.HandleShortcut(Key.D4,ModifierKeys.Control);Check(c1.Find<Border>("SceneHost").IsKeyboardFocused,"Ctrl+4 focuses 3D view");
       double oldOrbit=c1.Scene.CameraOrbit,originalBearing=c1.Current.Bearing;
       var keyArgs=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(c1.Window),0,Key.Right){RoutedEvent=Keyboard.KeyDownEvent};
       c1.Find<Border>("SceneHost").RaiseEvent(keyArgs);Check(keyArgs.Handled&&c1.Scene.CameraOrbit!=oldOrbit,"routed arrow key orbits model");Near(c1.Current.Bearing,originalBearing,"camera orbit does not alter solution");
       c1.Scene.HandleKey(Key.Space);Check(c1.Scene.IsTopView,"Space toggles overhead camera");
       c1.RenderTo(Path.Combine(Path.GetDirectoryName(report),"app-top-view.png"));
       for(int i=0;i<40;i++)c1.Scene.Zoom(-1);Near(c1.Scene.CameraDistance,7,"zoom-in bounded");
       for(int i=0;i<40;i++)c1.Scene.Zoom(1);Near(c1.Scene.CameraDistance,14,"zoom-out bounded");
       c1.Scene.HandleKey(Key.Home);Check(!c1.Scene.IsTopView&&c1.Scene.CameraOrbit==32,"Home restores default camera");
       double beforeZoom=c1.Scene.CameraDistance;
       Check(c1.Scene.HandleKey(Key.OemPlus,ModifierKeys.Shift),"Shift+plus key is handled by the 3D view");
       Near(c1.Scene.CameraDistance,beforeZoom-.5,"main keyboard plus zooms in");
       Check(c1.Scene.HandleKey(Key.Subtract,ModifierKeys.None),"numpad minus is handled");Near(c1.Scene.CameraDistance,beforeZoom,"numpad minus zooms out");
       Check(c1.Scene.HandleKey(Key.Add,ModifierKeys.None),"numpad plus is handled");
       Check(c1.Scene.HandleKey(Key.OemMinus,ModifierKeys.None),"main keyboard minus is handled");Near(c1.Scene.CameraDistance,beforeZoom,"main keyboard minus zooms out");
       Check(!c1.Scene.HandleKey(Key.OemPlus,ModifierKeys.Control),"modified application shortcuts do not zoom the camera");
       Near(c1.Scene.CameraDistance,beforeZoom,"unhandled shortcut leaves camera unchanged");
       foreach(var row in directions){c1.SetInputs("100,100",row[0].ToString(CultureInfo.InvariantCulture)+","+row[1].ToString(CultureInfo.InvariantCulture),"300");Near(c1.Scene.Forward.X,Math.Sin(row[2]*Math.PI/180),"3D east axis "+row[2]);Near(c1.Scene.Forward.Z,-Math.Cos(row[2]*Math.PI/180),"3D north axis "+row[2]);}
       c1.Distance.Text="132";double shortTilt=c1.Scene.ElevationVisual.Value;c1.Distance.Text="684";Check(c1.Scene.ElevationVisual.Value<shortTilt,"longer range makes illustrative tube less steep");
       c1.LoadExample();
       c1.HandleShortcut(Key.D1,ModifierKeys.Control);Check(c1.Origin.IsKeyboardFocused,"Ctrl+1 focuses origin");Check(c1.Origin.SelectionLength==c1.Origin.Text.Length,"focus selects coordinate pair");
       c1.Origin.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));Check(c1.Target.IsKeyboardFocused,"Tab order origin to target");
       c1.Target.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));Check(c1.Distance.IsKeyboardFocused,"Tab order target to distance");
       c1.HandleShortcut(Key.D2,ModifierKeys.Control);Check(c1.Target.IsKeyboardFocused,"Ctrl+2 focuses target");c1.HandleShortcut(Key.D3,ModifierKeys.Control);Check(c1.Distance.IsKeyboardFocused,"Ctrl+3 focuses distance");
       c1.HandleShortcut(Key.Enter,ModifierKeys.None);Check(c1.Saved.Count==1,"Enter saves current setup");c1.SaveTarget();Check(c1.Saved.Count==1,"identical saves deduplicated");
       c1.Distance.Text="500";Near(c1.Current.Range,500,"typing override updates UI");Check(c1.Find<TextBlock>("InputStatus").Text.Contains("OVERRIDE"),"override visibly identified");c1.SaveTarget();
       c1.HandleShortcut(Key.N,ModifierKeys.Control);Check(c1.Current==null&&c1.Target.Text==""&&c1.Distance.Text==""&&c1.Origin.Text!="","new target clears override and retains mortar");
       c1.HandleShortcut(Key.H,ModifierKeys.Control);c1.HandleShortcut(Key.Enter,ModifierKeys.Shift);Check(c1.Distance.Text=="500"&&c1.Current!=null,"keyboard history restores override");
       c1.HandleShortcut(Key.H,ModifierKeys.Control);c1.History.SelectedIndex=0;c1.History.UpdateLayout();
       ((ListBoxItem)c1.History.ItemContainerGenerator.ContainerFromIndex(0)).Focus();
       c1.HandleShortcut(Key.Delete,ModifierKeys.None);
       Check(c1.History.IsKeyboardFocusWithin&&c1.History.SelectedIndex==0,"deleting a focused saved row keeps keyboard navigation in history");
       c1.HandleShortcut(Key.Delete,ModifierKeys.None);Check(c1.Saved.Count==0,"successive Delete keys remove remaining saved targets");
       c1.TargetName.Text="North bridge";c1.SaveTarget();var bridge=c1.Saved[0];c1.ToggleFavorite();Check(bridge.Name=="North bridge"&&bridge.IsFavorite,"saved target has a name and favorite state");
       c1.SetInputs("100,100","102,100","");c1.TargetName.Text="East depot";c1.SaveTarget();var depot=c1.Saved[0];Check(c1.History.Items[0]==bridge,"favorites appear before more recent targets");
       c1.HandleShortcut(Key.F,ModifierKeys.Control);Check(c1.HistorySearch.IsKeyboardFocused,"Ctrl+F focuses target search");c1.HistorySearch.Text="DEPOT";Check(c1.History.Items.Count==1&&c1.History.Items[0]==depot,"name search ignores case");
       c1.HandleShortcut(Key.Enter,ModifierKeys.None);Check(c1.History.IsKeyboardFocusWithin&&c1.Saved.Count==2,"Enter in search focuses results without saving another target");
       c1.Origin.Text="100,102";c1.Distance.Text="450";c1.HandleShortcut(Key.Enter,ModifierKeys.None);
       Check(c1.Origin.Text=="100,102"&&c1.Target.Text=="102,100"&&c1.Distance.Text==""&&c1.TargetName.Text=="East depot","Enter uses a saved target from the current mortar and clears the old override");
       Near(c1.Current.Range,Math.Sqrt(8)*100,"using saved target recalculates range from current position");
       c1.HistorySearch.Clear();c1.History.SelectedItem=depot;c1.RenameSelected("East gate");Check(depot.Name=="East gate"&&c1.Saved.Count==2,"rename changes the existing saved target");
       c1.History.SelectedItem=bridge;c1.HandleShortcut(Key.H,ModifierKeys.Control);c1.HandleShortcut(Key.Enter,ModifierKeys.Shift);
       Check(c1.Origin.Text==bridge.Origin&&c1.Distance.Text=="500"&&c1.TargetName.Text=="North bridge","Shift+Enter restores the complete named setup");
       c1.RenameSelected("North crossing");Check(c1.TargetName.Text=="North crossing","renaming the loaded setup keeps its editor name synchronized");
       c1.HandleShortcut(Key.H,ModifierKeys.Control);
       c1.Window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(delegate{
        var dialog=c1.Window.OwnedWindows[0];var editor=Keyboard.FocusedElement as TextBox;
        if(editor!=null)editor.Text="North crossing renamed";dialog.DialogResult=true;
       }));
       c1.HandleShortcut(Key.F2,ModifierKeys.None);Check(bridge.Name=="North crossing renamed"&&c1.History.IsKeyboardFocusWithin,"F2 opens the rename dialog and returns focus to saved targets after confirmation");
       c1.Window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(delegate{var dialog=c1.Window.OwnedWindows[0];var editor=Keyboard.FocusedElement as TextBox;if(editor!=null)editor.Text="Cancelled name";dialog.DialogResult=false;}));
       c1.HandleShortcut(Key.F2,ModifierKeys.None);Check(bridge.Name=="North crossing renamed","canceling rename leaves the target unchanged");
       c1.HistorySearch.Text="no match";Check(c1.History.Items.Count==0&&!c1.Find<Button>("UseTarget").IsEnabled,"empty search results disable saved-target actions");
       c1.HandleShortcut(Key.F,ModifierKeys.Control);c1.HandleShortcut(Key.Escape,ModifierKeys.None);Check(c1.History.Items.Count==2,"Esc clears the search filter");
       c1.HistorySearch.Text="102,100";Check(c1.History.Items.Count==1,"search also matches target coordinates");c1.HistorySearch.Clear();
       c1.RenderTo(Path.Combine(Path.GetDirectoryName(report),"app-saved-targets.png"));
       c1.Distance.Text="700";Check(c1.Current!=null&&!c1.Current.InRange&&c1.Find<TextBlock>("Mil").Text=="— MIL","out-of-range UI suppresses MIL");
       Check(c1.Scene.HasTarget&&!c1.Scene.ElevationVisual.HasValue,"unreachable target retains bearing without elevation claim");
       c1.RenderTo(Path.Combine(Path.GetDirectoryName(report),"app-out-of-range.png"));
       c1.Distance.Text="oops";Check(c1.Current==null&&c1.Find<TextBlock>("Range").Text=="—"&&!c1.Find<Button>("Copy").IsEnabled,"invalid edits clear stale solution and copy");
       Check(!c1.Scene.HasTarget&&!c1.Scene.ElevationVisual.HasValue,"invalid input clears 3D target too");
       c1.SetInputs("100,100","100,100","");Check(c1.Current==null,"same point invalidates UI");
       c1.HandleShortcut(Key.T,ModifierKeys.Control);Check(c1.Window.Topmost,"Ctrl+T enables keep on top");c1.HandleShortcut(Key.T,ModifierKeys.Control);Check(!c1.Window.Topmost,"Ctrl+T disables keep on top");
       c1.LoadExample();
       foreach(var size in new[]{new[]{560d,500d},new[]{760d,780d},new[]{999d,700d},new[]{1000d,700d},new[]{1200d,650d},new[]{1400d,1000d}}){
        Resize(c1,size[0],size[1]);var scroll=c1.Find<ScrollViewer>("BodyScroll");scroll.ScrollToTop();c1.Window.UpdateLayout();
        Check(c1.IsNarrowLayout==(c1.Window.ActualWidth<1000),"responsive layout breakpoint "+size[0]+"x"+size[1]);
        Check(scroll.ExtentWidth<=scroll.ViewportWidth+.5,"no horizontal overflow "+size[0]+"x"+size[1]);
        var results=c1.Find<Border>("ResultsCard");double before=results.TranslatePoint(new Point(0,0),c1.Window).Y;
        scroll.ScrollToEnd();c1.Window.UpdateLayout();Near(results.TranslatePoint(new Point(0,0),c1.Window).Y,before,"aiming results stay fixed while controls scroll "+size[0]+"x"+size[1]);
        Check(results.TranslatePoint(new Point(0,results.ActualHeight),c1.Window).Y<=scroll.TranslatePoint(new Point(0,0),c1.Window).Y,"controls cannot cover aiming values "+size[0]+"x"+size[1]);
       }
       Resize(c1,1200,650);c1.Find<ScrollViewer>("BodyScroll").ScrollToTop();c1.RenderTo(Path.Combine(Path.GetDirectoryName(report),"app-short-wide.png"));
       Resize(c1,560,500);c1.Find<ScrollViewer>("BodyScroll").ScrollToTop();c1.RenderTo(Path.Combine(Path.GetDirectoryName(report),"app-minimum-size.png"));
       c1.HandleShortcut(Key.H,ModifierKeys.Control);c1.RenderTo(Path.Combine(Path.GetDirectoryName(report),"app-narrow-history.png"));Check(c1.History.IsKeyboardFocusWithin,"saved targets stay keyboard accessible in the smallest layout");
       string session=Path.Combine(sessionDirectory,"session.xml");
       var p=new Controller(session);p.LoadExample();p.Distance.Text="450";p.TargetName.Text="Test crossing";p.SaveTarget();p.ToggleFavorite();p.Persist();var restored=new Controller(session);
       Check(restored.Current!=null&&restored.Current.Range==450&&restored.Saved.Count==1,"session persistence roundtrip");
       Check(restored.TargetName.Text=="Test crossing"&&restored.Saved[0].Name=="Test crossing"&&restored.Saved[0].IsFavorite,"names and favorites persist across restart");
       p.Distance.Text=" 450 ";p.SaveTarget();p.Persist();var spaced=new Controller(session);
       Check(spaced.Saved.Count==p.Saved.Count&&spaced.Current!=null&&spaced.Current.Range==450,"saved override with surrounding spaces survives restart");
       p.Distance.Text="   ";p.SaveTarget();p.Persist();var blankOverride=new Controller(session);
       Check(blankOverride.Saved.Count==p.Saved.Count&&blankOverride.Current!=null&&!blankOverride.Current.Overridden,"whitespace-only saved override survives restart");
       for(int i=0;i<25;i++){p.SetInputs("100,100","101,"+(100+i).ToString(CultureInfo.InvariantCulture),"");p.SaveTarget();}Check(p.Saved.Count==20,"history capped at 20");
       Check(p.Saved.Exists(t=>t.IsFavorite&&t.Distance=="450"),"history eviction preserves favorites");
       foreach(var t in p.Saved)t.IsFavorite=true;p.SetInputs("90,90","91,91","");p.SaveTarget();Check(p.Saved.Count==20&&p.Find<TextBlock>("HistoryHint").Text.Contains("All 20"),"full favorite list is protected from silent eviction");
       p.History.SelectedIndex=0;p.RemoveSelected();Check(p.Saved.Count==19,"delete saved target");
       string legacy=Path.Combine(sessionDirectory,"legacy.xml");File.WriteAllText(legacy,"<WardogsFastCalc><History><Setup origin='100,100' target='102,100' distance=' 300 ' summary='legacy'/></History></WardogsFastCalc>");var legacyState=new Controller(legacy);Check(legacyState.Saved.Count==1&&legacyState.Saved[0].DisplayName=="Target 102,100","older unnamed saved targets migrate without loss");
       string blocked=Path.Combine(sessionDirectory,"blocked");File.WriteAllText(blocked,"test");var blockedState=new Controller(Path.Combine(blocked,"session.xml"));blockedState.LoadExample();blockedState.SaveTarget();Check(blockedState.Saved.Count==1&&blockedState.Find<TextBlock>("HistoryHint").Text.Contains("Disk save failed"),"disk save failures are not reported as successful persistence");
       string placement=Path.Combine(sessionDirectory,"placement.xml");var placed=new Controller(placement);placed.Window.Show();placed.Window.Left=SystemParameters.WorkArea.Left+20;placed.Window.Top=SystemParameters.WorkArea.Top+20;Resize(placed,820,640);placed.Persist();placed.Window.Close();
       var reopened=new Controller(placement);reopened.Window.Show();reopened.Window.UpdateLayout();Near(reopened.Window.Width,820,"saved window width restored");Near(reopened.Window.Height,640,"saved window height restored");
       reopened.Window.WindowState=WindowState.Maximized;reopened.Window.WindowState=WindowState.Minimized;reopened.Persist();reopened.Window.Close();var maximized=new Controller(placement);maximized.Window.Show();Check(maximized.Window.WindowState==WindowState.Maximized,"maximized preference survives a minimized close");maximized.Window.Close();
       File.WriteAllText(placement,"<WardogsFastCalc><Window left='800000' top='800000' width='820' height='640'/></WardogsFastCalc>");var offscreen=new Controller(placement);offscreen.Window.Show();Check(offscreen.Window.Left<SystemParameters.VirtualScreenLeft+SystemParameters.VirtualScreenWidth&&offscreen.Window.Top<SystemParameters.VirtualScreenTop+SystemParameters.VirtualScreenHeight,"off-screen persisted window recovers on startup");offscreen.Window.Close();
       File.WriteAllText(session,"broken xml");var recovered=new Controller(session);Check(recovered.Current==null,"corrupt session recovers without crashing");
       File.WriteAllText(session,"<!DOCTYPE WardogsFastCalc [<!ENTITY test '100,100'>]><WardogsFastCalc><Origin>&test;</Origin></WardogsFastCalc>");var dtd=new Controller(session);Check(dtd.Origin.Text==""&&dtd.Find<TextBlock>("Footer").Text.Contains("could not be read"),"DTD entities rejected");
       string canary=Path.Combine(sessionDirectory,"canary.txt");File.WriteAllText(canary,"100,100");
       File.WriteAllText(session,"<!DOCTYPE WardogsFastCalc [<!ENTITY test SYSTEM '"+new Uri(canary).AbsoluteUri+"'>]><WardogsFastCalc><Origin>&test;</Origin></WardogsFastCalc>");var external=new Controller(session);Check(external.Origin.Text==""&&external.Find<TextBlock>("Footer").Text.Contains("could not be read"),"external entities rejected");
       File.WriteAllText(session,new string('x',131073));var oversized=new Controller(session);Check(oversized.Current==null,"oversized session recovers");
       File.WriteAllText(session,"<WardogsFastCalc><Origin>"+new string('1',513)+"</Origin></WardogsFastCalc>");var longField=new Controller(session);Check(longField.Origin.Text=="","oversized coordinate rejected before UI assignment");
       File.WriteAllText(session,"<Other><Origin>100,100</Origin></Other>");var wrongRoot=new Controller(session);Check(wrongRoot.Origin.Text=="","unexpected document root rejected");
       File.WriteAllText(session,"<WardogsFastCalc><History><Setup origin='100,100' target='101,101' distance='NaN' summary='invalid'/></History></WardogsFastCalc>");var badHistory=new Controller(session);Check(badHistory.Saved.Count==0,"malformed saved history omitted");
      }catch(Exception ex){log.Add(ex.ToString());result=1;}
      finally{c1.Window.Close();app.Shutdown();}
     }));
    };
    app.Run(c1.Window);
   }catch(Exception ex){log.Add(ex.ToString());result=1;}
   // All WPF windows have now closed, so their Closing handlers cannot recreate
   // session files in the repository or the distributable archive.
   if(Directory.Exists(sessionDirectory))Directory.Delete(sessionDirectory,true);
   log.Add(String.Format("{0}: {1} assertions passed.",result==0?"SUCCESS":"FAILED",count));File.WriteAllLines(report,log.ConvertAll(line=>line.TrimEnd()));return result;
  }
 }
}
